using Eggrack.Operations.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed class ProcurementMailDispatcher(DatabaseSessionFactory databases,LegacySmtpSender smtp,ProcurementScopePolicy scopePolicy,IConfiguration configuration,ILogger<ProcurementMailDispatcher> logger)
{
    private const string DatabaseName="Eggrack";
    private sealed class PendingMail
    {
        public uint Id { get; set; }
        public uint PlanId { get; set; }
        public string Recipient { get; set; }=string.Empty;
        public string TemplateCode { get; set; }=string.Empty;
        public uint InvoiceId { get; set; }
        public string PiNumber { get; set; }=string.Empty;
        public string ContactName { get; set; }=string.Empty;
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; }=string.Empty;
        public string RequestNumber { get; set; }=string.Empty;
        public DateTime? ValidUntil { get; set; }
    }
    private sealed record LegacyMailConfig(string SmtpHost,int SmtpPort,string SmtpUserName,string FromEmail,string SmtpPassword,string? FromName,string? Smtpinbox);
    private sealed class MailProduct
    {
        public decimal Quantity { get; set; }
        public string Unit { get; set; }=string.Empty;
    }

    public async Task<string> PreviewHtmlAsync(uint mailTaskId,CancellationToken token)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsureMailTaskAsync(db,mailTaskId,token);
        var rows=await db.QueryAsync<PendingMail>("""
        SELECT t.id Id,t.plan_id PlanId,t.recipient Recipient,t.template_code TemplateCode,
          i.id InvoiceId,i.pi_number PiNumber,i.contact_name ContactName,i.total_amount TotalAmount,i.currency Currency,
          r.request_number RequestNumber,FROM_UNIXTIME(i.valid_until) ValidUntil
        FROM procurement_mail_tasks t
        JOIN proforma_invoices i ON i.id=CAST(JSON_UNQUOTE(JSON_EXTRACT(t.payload_json,'$.proformaInvoiceId')) AS UNSIGNED)
          AND i.purchase_plan_id=t.plan_id AND i.status=3
        JOIN purchase_requests r ON r.id=i.request_id
        WHERE t.id=@MailTaskId LIMIT 1
        """,new{MailTaskId=mailTaskId},cancellationToken:token);
        var task=rows.SingleOrDefault()??throw new InvalidOperationException("邮件任务不存在或 PI 尚未签发。");
        var products=await LoadProductsAsync(db,task.InvoiceId,token);
        return BuildHtml(task,products);
    }

    public async Task<bool> DispatchAsync(uint mailTaskId,long staffId,CancellationToken token)
    {
        PendingMail? task;
        await using(var db=await databases.OpenMySqlAsync(DatabaseName,token))
        {
            await scopePolicy.EnsureMailTaskAsync(db,mailTaskId,token);
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var rows=await db.QueryAsync<PendingMail>("""
            SELECT t.id Id,t.plan_id PlanId,t.recipient Recipient,t.template_code TemplateCode,
              i.id InvoiceId,i.pi_number PiNumber,i.contact_name ContactName,i.total_amount TotalAmount,i.currency Currency,
              r.request_number RequestNumber,FROM_UNIXTIME(i.valid_until) ValidUntil
            FROM procurement_mail_tasks t
            JOIN proforma_invoices i ON i.id=CAST(JSON_UNQUOTE(JSON_EXTRACT(t.payload_json,'$.proformaInvoiceId')) AS UNSIGNED)
              AND i.purchase_plan_id=t.plan_id AND i.status=3
            JOIN purchase_requests r ON r.id=i.request_id
            WHERE t.id=@MailTaskId AND ((t.status IN ('pending','failed')
              OR (t.status='processing' AND t.locked_at<@StaleAt)) AND t.attempts<5)
            ORDER BY t.created_at,t.id LIMIT 1
            """,new{MailTaskId=mailTaskId,Now=now,StaleAt=now-600},cancellationToken:token);
            task=rows.SingleOrDefault();
            if(task is null)throw new InvalidOperationException("邮件任务不存在、PI 尚未签发或任务当前不可发送。");
            var claimed=await db.ExecuteAsync("UPDATE procurement_mail_tasks SET status='processing',attempts=attempts+1,locked_at=@Now,last_error=NULL,updated_at=@Now WHERE id=@Id AND (status IN ('pending','failed') OR (status='processing' AND locked_at<@StaleAt))",new{task.Id,Now=now,StaleAt=now-600},cancellationToken:token);
            if(claimed!=1)throw new InvalidOperationException("邮件任务已被其他操作处理，请刷新后重试。");
        }

        try
        {
            await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
            var configurations=await db.QueryAsync<LegacyMailConfig>("""
            SELECT SmtpHost,SmtpPort,SmtpUserName,FromEmail,SmtpPassword,FromName,Smtpinbox
            FROM emailcofig WHERE emailtype IN (@TemplateCode,'order_create')
            ORDER BY CASE WHEN emailtype=@TemplateCode THEN 0 ELSE 1 END LIMIT 1
            """,new{task.TemplateCode},cancellationToken:token);
            var config=configurations.SingleOrDefault()??throw new InvalidOperationException("PHP 邮件配置表中没有匹配配置。");
            if(string.IsNullOrWhiteSpace(config.SmtpHost)||string.IsNullOrWhiteSpace(config.FromEmail)||string.IsNullOrWhiteSpace(config.SmtpPassword)||string.IsNullOrWhiteSpace(config.SmtpUserName))throw new InvalidOperationException("PHP 邮件配置不完整。");
            var products=await LoadProductsAsync(db,task.InvoiceId,token);
            var settings=new LegacySmtpSettings(config.SmtpHost.Replace("ssl://",string.Empty,StringComparison.OrdinalIgnoreCase),config.SmtpPort>0?config.SmtpPort:465,config.FromEmail,config.SmtpPassword,config.SmtpUserName,string.IsNullOrWhiteSpace(config.FromName)?config.SmtpUserName:config.FromName,SplitRecipients(config.Smtpinbox));
            var subject=$"Your Quotation Is Ready: {task.PiNumber}";
            var body=BuildHtml(task,products);
            await smtp.SendHtmlAsync(settings,task.Recipient,subject,body,token);
            var sentAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("UPDATE procurement_mail_tasks SET status='sent',sent_at=@Now,locked_at=NULL,next_attempt_at=NULL,last_error=NULL,updated_at=@Now WHERE id=@Id AND status='processing'",new{task.Id,Now=sentAt},cancellationToken:token);
            await db.ExecuteAsync("INSERT procurement_workflow_events(plan_id,event_code,from_status,to_status,entity_type,entity_id,note,actor_id,created_at) VALUES(@PlanId,'mail.sent',8,8,'mail_task',@Id,@Recipient,@StaffId,@Now)",new{task.PlanId,task.Id,task.Recipient,StaffId=staffId,Now=sentAt},cancellationToken:token);
            logger.LogInformation("Procurement mail task {MailTaskId} sent for PI {PiNumber}",task.Id,task.PiNumber);
            return true;
        }
        catch(Exception error) when(error is not OperationCanceledException)
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await using var db=await databases.OpenMySqlAsync(DatabaseName,CancellationToken.None);
            var attempts=(await db.QueryAsync<byte>("SELECT attempts FROM procurement_mail_tasks WHERE id=@Id",new{task.Id},cancellationToken:CancellationToken.None)).SingleOrDefault();
            var delaySeconds=Math.Min(3600,60*(1<<Math.Min(5,(int)attempts)));
            var message=error.Message.Length>950?error.Message[..950]:error.Message;
            await db.ExecuteAsync("UPDATE procurement_mail_tasks SET status='failed',locked_at=NULL,next_attempt_at=@NextAttemptAt,last_error=@Message,updated_at=@Now WHERE id=@Id",new{task.Id,NextAttemptAt=now+delaySeconds,Message=message,Now=now},cancellationToken:CancellationToken.None);
            logger.LogWarning(error,"Procurement mail task {MailTaskId} failed",task.Id);
            return false;
        }
    }

    private static IReadOnlyList<string> SplitRecipients(string? value)=>string.IsNullOrWhiteSpace(value)?Array.Empty<string>():value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);

    private static Task<IReadOnlyList<MailProduct>> LoadProductsAsync(DatabaseSession db,uint invoiceId,CancellationToken token)
        =>db.QueryAsync<MailProduct>("SELECT quantity Quantity,quantity_unit Unit FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id",new{InvoiceId=invoiceId},cancellationToken:token);

    private string BuildHtml(PendingMail task,IReadOnlyList<MailProduct> products)
    {
        var baseUrl=configuration["FileCenter:PublicBaseUrl"];
        if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var publicBase)||(publicBase.Scheme!=Uri.UriSchemeHttp&&publicBase.Scheme!=Uri.UriSchemeHttps))
            throw new InvalidOperationException("FileCenter:PublicBaseUrl 必须配置为有效的 HTTP(S) 客户站点地址。");
        publicBase=new Uri(publicBase.AbsoluteUri.TrimEnd('/')+"/");
        var requestPath=$"wholesale/request/{Uri.EscapeDataString(task.RequestNumber)}";
        var requestUrl=new Uri(publicBase,requestPath).AbsoluteUri;
        var quotationUrl=new Uri(publicBase,requestPath+"/pi").AbsoluteUri;
        var logoUrl=new Uri(publicBase,"u_file/2302/photo/2195670476.png").AbsoluteUri;
        var model=new ProcurementQuotationMailModel(
            task.ContactName,
            task.PiNumber,
            task.RequestNumber,
            task.ValidUntil,
            task.TotalAmount,
            task.Currency,
            quotationUrl,
            requestUrl,
            logoUrl,
            products.Select(product=>new ProcurementQuotationMailProduct(product.Quantity,product.Unit)).ToArray());
        return ProcurementQuotationMailTemplate.Build(model);
    }
}
