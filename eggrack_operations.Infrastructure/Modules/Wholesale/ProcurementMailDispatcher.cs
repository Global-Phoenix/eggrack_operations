using System.Net;
using System.Text;
using Eggrack.Operations.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed class ProcurementMailDispatcher(DatabaseSessionFactory databases,LegacySmtpSender smtp,ILogger<ProcurementMailDispatcher> logger)
{
    private const string DatabaseName="Eggrack";
    private sealed record PendingMail(uint Id,uint PlanId,string Recipient,string TemplateCode,uint InvoiceId,string PiNumber,string ContactName,decimal TotalAmount,string Currency);
    private sealed record LegacyMailConfig(string SmtpHost,int SmtpPort,string SmtpUserName,string FromEmail,string SmtpPassword,string? FromName,string? Smtpinbox);
    private sealed record MailProduct(string ProductName,decimal Quantity,string Unit,decimal UnitPrice,decimal LineAmount);

    public async Task<bool> DispatchAsync(uint mailTaskId,CancellationToken token)
    {
        PendingMail? task;
        await using(var db=await databases.OpenMySqlAsync(DatabaseName,token))
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var rows=await db.QueryAsync<PendingMail>("""
            SELECT t.id Id,t.plan_id PlanId,t.recipient Recipient,t.template_code TemplateCode,
              i.id InvoiceId,i.pi_number PiNumber,i.contact_name ContactName,i.total_amount TotalAmount,i.currency Currency
            FROM procurement_mail_tasks t
            JOIN proforma_invoices i ON i.purchase_plan_id=t.plan_id AND i.status=3
            WHERE t.id=@MailTaskId AND ((t.status IN ('pending','failed')
              OR (t.status='processing' AND t.locked_at<@StaleAt)) AND t.attempts<5
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
            var products=await db.QueryAsync<MailProduct>("SELECT product_name ProductName,quantity Quantity,quantity_unit Unit,unit_price UnitPrice,line_amount LineAmount FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id",new{task.InvoiceId},cancellationToken:token);
            var settings=new LegacySmtpSettings(config.SmtpHost.Replace("ssl://",string.Empty,StringComparison.OrdinalIgnoreCase),config.SmtpPort>0?config.SmtpPort:465,config.FromEmail,config.SmtpPassword,config.SmtpUserName,string.IsNullOrWhiteSpace(config.FromName)?config.SmtpUserName:config.FromName,SplitRecipients(config.Smtpinbox));
            var subject=$"Proforma Invoice {task.PiNumber}";
            var body=BuildHtml(task,products);
            await smtp.SendHtmlAsync(settings,task.Recipient,subject,body,token);
            var sentAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("UPDATE procurement_mail_tasks SET status='sent',sent_at=@Now,locked_at=NULL,next_attempt_at=NULL,last_error=NULL,updated_at=@Now WHERE id=@Id AND status='processing'",new{task.Id,Now=sentAt},cancellationToken:token);
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

    private static string BuildHtml(PendingMail task,IReadOnlyList<MailProduct> products)
    {
        var html=new StringBuilder();
        html.Append("<div style=\"font-family:Arial,sans-serif;color:#1f2937\"><p>Dear ").Append(WebUtility.HtmlEncode(task.ContactName)).Append(",</p>")
          .Append("<p>Please find your proforma invoice <strong>").Append(WebUtility.HtmlEncode(task.PiNumber)).Append("</strong>.</p>")
          .Append("<table style=\"border-collapse:collapse;width:100%\"><thead><tr><th align=\"left\">Product</th><th>Quantity</th><th>Unit price</th><th>Amount</th></tr></thead><tbody>");
        foreach(var product in products)html.Append("<tr><td>").Append(WebUtility.HtmlEncode(product.ProductName)).Append("</td><td align=\"right\">").Append(product.Quantity.ToString("0.###")).Append(' ').Append(WebUtility.HtmlEncode(product.Unit)).Append("</td><td align=\"right\">").Append(product.UnitPrice.ToString("0.0000")).Append("</td><td align=\"right\">").Append(product.LineAmount.ToString("0.00")).Append("</td></tr>");
        html.Append("</tbody></table><p><strong>Total: ").Append(WebUtility.HtmlEncode(task.Currency)).Append(' ').Append(task.TotalAmount.ToString("0.00")).Append("</strong></p><p>Best regards,<br>EGGRACKS</p></div>");
        return html.ToString();
    }
}
