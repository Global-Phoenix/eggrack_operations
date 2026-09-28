using Eggrack.Operations.Application.Modules.Wholesale;

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
        await db.ExecuteAsync("INSERT procurement_inquiries(plan_item_id,supplier_id,unit_price_cny,moq,lead_days,valid_until,terms,created_at) VALUES(@PlanItemId,@SupplierId,@UnitPriceCny,@Moq,@LeadDays,@ValidUntil,@Terms,@Now)",new{command.PlanItemId,command.SupplierId,command.UnitPriceCny,command.Moq,command.LeadDays,ValidUntil=command.ValidUntil?.ToString("yyyy-MM-dd"),command.Terms,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<long> RecordSampleAsync(RecordSampleCommand command,CancellationToken token=default)
    {
        if(command.CostCny<0) throw new InvalidOperationException("样品成本不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await db.ExecuteAsync("INSERT procurement_samples(plan_item_id,supplier_id,status,cost_cny,tracking_number,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Status,@CostCny,@TrackingNumber,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<QuoteDecisionResult> ApproveQuoteAsync(QuoteDecisionCommand command,string recipient,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var rows=await db.QueryAsync<PlanForApproval>("SELECT id Id,total_cost_usd TotalCostUsd,profit_rate ProfitRate,status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:token);
            var plan=rows.SingleOrDefault()??throw new InvalidOperationException("采购计划不存在。");
            if(plan.Status!=3) throw new InvalidOperationException("当前状态不能批准报价。");
            ProcurementPricing.ValidateFinalQuote(command.QuoteUsd,plan.TotalCostUsd??0);
            var margin=(command.QuoteUsd-plan.TotalCostUsd!.Value)/command.QuoteUsd;
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("INSERT procurement_quote_approvals(plan_id,status,quote_usd,profit_rate,submitted_by,decided_by,decision_note,submitted_at,decided_at) VALUES(@PlanId,'approved',@QuoteUsd,@Margin,@StaffId,@StaffId,@Note,@Now,@Now)",new{command.PlanId,command.QuoteUsd,Margin=margin,command.StaffId,command.Note,Now=now},cancellationToken:token);
            var approvalId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            await db.ExecuteAsync("UPDATE purchase_plans SET status=4,approved_quote_amount_usd=@QuoteUsd,approved_by=@StaffId,approved_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId",new{command.PlanId,command.QuoteUsd,command.StaffId,Now=now},cancellationToken:token);
            await db.ExecuteAsync("INSERT procurement_mail_tasks(plan_id,approval_id,template_code,recipient,status,payload_json,created_at) VALUES(@PlanId,@ApprovalId,'wholesale.final-quote',@Recipient,'pending',@Payload,@Now)",new{command.PlanId,ApprovalId=approvalId,Recipient=recipient,Payload=System.Text.Json.JsonSerializer.Serialize(new{command.PlanId,approvalId,command.QuoteUsd}),Now=now},cancellationToken:token);
            var taskId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            await db.CommitAsync(token);
            return new(approvalId,taskId,"Approved");
        }
        catch{await db.RollbackAsync(token);throw;}
    }

    public async Task<QuoteDecisionResult> RejectQuoteAsync(QuoteDecisionCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Note)) throw new InvalidOperationException("退回时必须填写原因。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=2,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=3",new{command.PlanId,command.StaffId,Now=now},cancellationToken:token);
            if(changed!=1) throw new InvalidOperationException("当前状态不能退回报价。");
            await db.ExecuteAsync("INSERT procurement_quote_approvals(plan_id,status,quote_usd,profit_rate,submitted_by,decided_by,decision_note,submitted_at,decided_at) SELECT id,'rejected',@QuoteUsd,COALESCE(profit_rate,0),@StaffId,@StaffId,@Note,@Now,@Now FROM purchase_plans WHERE id=@PlanId",new{command.PlanId,command.QuoteUsd,command.StaffId,command.Note,Now=now},cancellationToken:token);
            var approvalId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            await db.CommitAsync(token);
            return new(approvalId,null,"Rejected");
        }
        catch{await db.RollbackAsync(token);throw;}
    }

    private sealed record PlanForApproval(long Id,decimal? TotalCostUsd,decimal? ProfitRate,int Status);
}
