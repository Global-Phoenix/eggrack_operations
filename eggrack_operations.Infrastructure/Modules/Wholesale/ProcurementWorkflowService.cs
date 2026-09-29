using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Common.Models;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    public async Task<IReadOnlyList<SupplierListItem>> GetSuppliersAsync(string? keyword,CancellationToken token=default)
    {
        const string sql="SELECT id Id,supplier_name Name,supplier_code Code,status Status FROM procurement_suppliers WHERE (@Keyword IS NULL OR supplier_name LIKE CONCAT('%',@Keyword,'%') OR supplier_code LIKE CONCAT('%',@Keyword,'%')) ORDER BY supplier_name";
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<SupplierListItem>(sql,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()},cancellationToken:token);
    }

    public async Task<long> CreateSupplierAsync(CreateSupplierCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Name)) throw new InvalidOperationException("供应商名称不能为空。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await db.ExecuteAsync("INSERT procurement_suppliers(supplier_name,supplier_code,contact_json,status,created_at,updated_at) VALUES(@Name,@Code,@ContactJson,'active',@Now,@Now)",new{Name=command.Name.Trim(),command.Code,command.ContactJson,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<long> RecordInquiryAsync(RecordInquiryCommand command,CancellationToken token=default)
    {
        if(command.UnitPriceCny<0||command.Moq<0||command.LeadDays<0) throw new InvalidOperationException("询价数据不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        await db.ExecuteAsync("INSERT procurement_inquiries(plan_item_id,supplier_id,unit_price_cny,moq,lead_days,valid_until,terms,created_at) VALUES(@PlanItemId,@SupplierId,@UnitPriceCny,@Moq,@LeadDays,@ValidUntil,@Terms,@Now)",new{command.PlanItemId,command.SupplierId,command.UnitPriceCny,command.Moq,command.LeadDays,ValidUntil=command.ValidUntil?.ToString("yyyy-MM-dd"),command.Terms,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<long> RecordSampleAsync(RecordSampleCommand command,CancellationToken token=default)
    {
        if(command.CostCny<0) throw new InvalidOperationException("样品成本不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        await db.ExecuteAsync("INSERT procurement_samples(plan_item_id,supplier_id,status,cost_cny,tracking_number,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Status,@CostCny,@TrackingNumber,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }
    public async Task<QuoteDecisionResult> ApproveQuoteAsync(QuoteDecisionCommand command,string recipient,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(recipient)) throw new BusinessRuleException("批准报价时必须填写收件人。","procurement.quote.recipient-required");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.ExecuteInTransactionAsync<QuoteDecisionResult>(async transactionToken=>
        {
            var rows=await db.QueryAsync<PlanForApproval>("SELECT id Id,request_id RequestId,request_version_id RequestVersionId,total_cost_usd TotalCostUsd,profit_rate ProfitRate,status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken);
            var plan=rows.SingleOrDefault()??throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            if(plan.Status!=3) throw new BusinessRuleException("当前状态不能批准报价。","procurement.quote.approve-state");
            ProcurementPricing.ValidateFinalQuote(command.QuoteUsd,plan.TotalCostUsd??0);
            var margin=(command.QuoteUsd-plan.TotalCostUsd!.Value)/command.QuoteUsd;
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("INSERT procurement_quote_approvals(plan_id,status,quote_usd,profit_rate,submitted_by,decided_by,decision_note,submitted_at,decided_at) VALUES(@PlanId,'approved',@QuoteUsd,@Margin,@StaffId,@StaffId,@Note,@Now,@Now)",new{command.PlanId,command.QuoteUsd,Margin=margin,command.StaffId,command.Note,Now=now},cancellationToken:transactionToken);
            var approvalId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            await db.ExecuteAsync("UPDATE purchase_plans SET status=4,approved_quote_amount_usd=@QuoteUsd,approved_by=@StaffId,approved_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId",new{command.PlanId,command.QuoteUsd,command.StaffId,Now=now},cancellationToken:transactionToken);
            var invoice=await CreateApprovedInvoiceAsync(db,plan,command.QuoteUsd,command.Note,command.StaffId,now,transactionToken);
            await db.ExecuteAsync("INSERT procurement_mail_tasks(plan_id,approval_id,template_code,recipient,status,payload_json,created_at) VALUES(@PlanId,@ApprovalId,'wholesale.final-quote',@Recipient,'pending',@Payload,@Now)",new{command.PlanId,ApprovalId=approvalId,Recipient=recipient.Trim(),Payload=System.Text.Json.JsonSerializer.Serialize(new{command.PlanId,approvalId,command.QuoteUsd,proformaInvoiceId=invoice.Id,proformaInvoiceNumber=invoice.Number}),Now=now},cancellationToken:transactionToken);
            var taskId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            return new(approvalId,taskId,invoice.Id,invoice.Number,"Approved");
        },cancellationToken:token);
    }

    public async Task<QuoteDecisionResult> RejectQuoteAsync(QuoteDecisionCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Note)) throw new BusinessRuleException("退回时必须填写原因。","procurement.quote.reject-note-required");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.ExecuteInTransactionAsync<QuoteDecisionResult>(async transactionToken=>
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=2,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=3",new{command.PlanId,command.StaffId,Now=now},cancellationToken:transactionToken);
            if(changed!=1) throw new BusinessRuleException("当前状态不能退回报价。","procurement.quote.reject-state");
            await db.ExecuteAsync("INSERT procurement_quote_approvals(plan_id,status,quote_usd,profit_rate,submitted_by,decided_by,decision_note,submitted_at,decided_at) SELECT id,'rejected',@QuoteUsd,COALESCE(profit_rate,0),@StaffId,@StaffId,@Note,@Now,@Now FROM purchase_plans WHERE id=@PlanId",new{command.PlanId,command.QuoteUsd,command.StaffId,command.Note,Now=now},cancellationToken:transactionToken);
            var approvalId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            return new(approvalId,null,null,null,"Rejected");
        },cancellationToken:token);
    }

    public async Task<SourcingWorkspace> GetSourcingWorkspaceAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var planItems=await db.QueryAsync<ProcurementPlanItemOption>("SELECT id Id,product_name ProductName,quantity Quantity,quantity_unit Unit FROM purchase_plan_items WHERE plan_id=@PlanId ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:token);
        var suppliers=await db.QueryAsync<SupplierListItem>("SELECT id Id,supplier_name Name,supplier_code Code,status Status FROM procurement_suppliers ORDER BY supplier_name",cancellationToken:token);
        var candidates=await db.QueryAsync<CandidateProductItem>("SELECT c.id Id,c.plan_item_id PlanItemId,c.supplier_id SupplierId,c.product_name ProductName,s.supplier_name SupplierName,c.reference_url ReferenceUrl,c.specification_json SpecificationJson,c.status Status FROM procurement_candidate_products c JOIN purchase_plan_items i ON i.id=c.plan_item_id LEFT JOIN procurement_suppliers s ON s.id=c.supplier_id WHERE i.plan_id=@PlanId ORDER BY c.updated_at DESC,c.id DESC",new{PlanId=planId},cancellationToken:token);
        var inquiries=await db.QueryAsync<InquiryItem>("SELECT q.id Id,q.plan_item_id PlanItemId,q.supplier_id SupplierId,i.product_name ProductName,s.supplier_name SupplierName,q.currency Currency,q.unit_price_cny UnitPrice,q.moq Moq,q.lead_days LeadDays,q.valid_until ValidUntil,q.terms Terms,q.status Status,q.notes Notes FROM procurement_inquiries q JOIN purchase_plan_items i ON i.id=q.plan_item_id JOIN procurement_suppliers s ON s.id=q.supplier_id WHERE i.plan_id=@PlanId ORDER BY q.updated_at DESC,q.id DESC",new{PlanId=planId},cancellationToken:token);
        var samples=await db.QueryAsync<SampleItem>("SELECT x.id Id,x.plan_item_id PlanItemId,x.supplier_id SupplierId,i.product_name ProductName,s.supplier_name SupplierName,x.quantity Quantity,x.status Status,x.cost_cny CostCny,x.tracking_number TrackingNumber,x.notes Notes FROM procurement_samples x JOIN purchase_plan_items i ON i.id=x.plan_item_id LEFT JOIN procurement_suppliers s ON s.id=x.supplier_id WHERE i.plan_id=@PlanId ORDER BY x.updated_at DESC,x.id DESC",new{PlanId=planId},cancellationToken:token);
        var mails=await db.QueryAsync<MailTaskItem>("SELECT id Id,recipient Recipient,status Status,template_code TemplateCode,attempts Attempts,last_error LastError,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(sent_at) SentAtUtc FROM procurement_mail_tasks WHERE plan_id=@PlanId ORDER BY created_at DESC,id DESC",new{PlanId=planId},cancellationToken:token);
        var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT id Id,pi_number Number,CASE status WHEN 1 THEN 'Draft' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Issued' ELSE 'Cancelled' END Status,total_amount TotalAmount,currency Currency,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(issued_at) IssuedAtUtc FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1",new{PlanId=planId},cancellationToken:token);
        var invoice=invoices.SingleOrDefault();
        var pricing=invoice is null?null:await GetProformaInvoicePricingAsync(db,invoice.Id,token);
        return new(planItems,suppliers,candidates,inquiries,samples,mails,invoice,pricing);
    }

    public async Task<uint> SaveCandidateAsync(SaveCandidateProductCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.ProductName)) throw new InvalidOperationException("候选产品名称不能为空。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);var editablePlanId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_candidate_products",command.Id.Value,editablePlanId,token);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(command.Id.HasValue){var changed=await db.ExecuteAsync("UPDATE procurement_candidate_products SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,product_name=@ProductName,reference_url=@ReferenceUrl,specification_json=@SpecificationJson,status=@Status,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,ProductName=command.ProductName.Trim(),command.ReferenceUrl,command.SpecificationJson,command.Status,Now=now},cancellationToken:token);if(changed!=1)throw new InvalidOperationException("候选产品不存在。");return command.Id.Value;}
        await db.ExecuteAsync("INSERT procurement_candidate_products(plan_item_id,supplier_id,product_name,reference_url,specification_json,status,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@ProductName,@ReferenceUrl,@SpecificationJson,@Status,@Now,@Now)",new{command.PlanItemId,command.SupplierId,ProductName=command.ProductName.Trim(),command.ReferenceUrl,command.SpecificationJson,command.Status,Now=now},cancellationToken:token);return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<uint> SaveInquiryAsync(SaveInquiryCommand command,CancellationToken token=default)
    {
        if(command.UnitPrice<0||command.Moq<0||command.LeadDays<0)throw new InvalidOperationException("询价数据不能为负数。");if(command.Currency is not("CNY" or "USD"))throw new InvalidOperationException("币种仅支持 CNY 或 USD。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);var editablePlanId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_inquiries",command.Id.Value,editablePlanId,token);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(command.Id.HasValue){var changed=await db.ExecuteAsync("UPDATE procurement_inquiries SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,currency=@Currency,unit_price_cny=@UnitPrice,moq=@Moq,lead_days=@LeadDays,valid_until=@ValidUntil,terms=@Terms,status=@Status,notes=@Notes,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,command.Currency,command.UnitPrice,command.Moq,command.LeadDays,command.ValidUntil,command.Terms,command.Status,command.Notes,Now=now},cancellationToken:token);if(changed!=1)throw new InvalidOperationException("询价记录不存在。");return command.Id.Value;}
        await db.ExecuteAsync("INSERT procurement_inquiries(plan_item_id,supplier_id,currency,unit_price_cny,moq,lead_days,valid_until,terms,status,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Currency,@UnitPrice,@Moq,@LeadDays,@ValidUntil,@Terms,@Status,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Currency,command.UnitPrice,command.Moq,command.LeadDays,command.ValidUntil,command.Terms,command.Status,command.Notes,Now=now},cancellationToken:token);return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<uint> SaveSampleAsync(SaveSampleCommand command,CancellationToken token=default)
    {
        if(command.Quantity<=0||command.CostCny<0)throw new InvalidOperationException("样品数量必须大于零，费用不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);var editablePlanId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_samples",command.Id.Value,editablePlanId,token);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(command.Id.HasValue){var changed=await db.ExecuteAsync("UPDATE procurement_samples SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,quantity=@Quantity,status=@Status,cost_cny=@CostCny,tracking_number=@TrackingNumber,notes=@Notes,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,command.Quantity,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=now},cancellationToken:token);if(changed!=1)throw new InvalidOperationException("样品记录不存在。");return command.Id.Value;}
        await db.ExecuteAsync("INSERT procurement_samples(plan_item_id,supplier_id,quantity,status,cost_cny,tracking_number,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Quantity,@Status,@CostCny,@TrackingNumber,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Quantity,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=now},cancellationToken:token);return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }
    private sealed record PlanForApproval(uint Id,uint RequestId,uint RequestVersionId,decimal? TotalCostUsd,decimal? ProfitRate,byte Status);
}
