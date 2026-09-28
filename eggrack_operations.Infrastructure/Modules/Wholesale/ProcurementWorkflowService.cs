using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    public async Task<IReadOnlyList<SupplierListItem>> GetSuppliersAsync(string? keyword,CancellationToken token=default)
    {
        const string sql="SELECT Id,Name,Code,Status FROM dbo.WholesaleSuppliers WHERE (@Keyword IS NULL OR Name LIKE '%'+@Keyword+'%' OR Code LIKE '%'+@Keyword+'%') ORDER BY Name";
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        return await db.QueryAsync<SupplierListItem>(sql,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()},cancellationToken:token);
    }

    public async Task<long> CreateSupplierAsync(CreateSupplierCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Name)) throw new InvalidOperationException("供应商名称不能为空。");
        const string sql="INSERT dbo.WholesaleSuppliers(Name,Code,ContactJson,Status) OUTPUT INSERTED.Id VALUES(@Name,@Code,@ContactJson,'Active')";
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        return (await db.QueryAsync<long>(sql,new{Name=command.Name.Trim(),command.Code,command.ContactJson},cancellationToken:token)).Single();
    }

    public async Task<long> RecordInquiryAsync(RecordInquiryCommand command,CancellationToken token=default)
    {
        if(command.UnitPriceCny<0||command.Moq<0||command.LeadDays<0) throw new InvalidOperationException("询价数据不能为负数。");
        const string sql="""
        INSERT dbo.WholesaleInquiries(PlanItemId,SupplierId,UnitPriceCny,Moq,LeadDays,ValidUntil,Terms)
        OUTPUT INSERTED.Id VALUES(@PlanItemId,@SupplierId,@UnitPriceCny,@Moq,@LeadDays,@ValidUntil,@Terms)
        """;
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        return (await db.QueryAsync<long>(sql,command,cancellationToken:token)).Single();
    }

    public async Task<long> RecordSampleAsync(RecordSampleCommand command,CancellationToken token=default)
    {
        if(command.CostCny<0) throw new InvalidOperationException("样品成本不能为负数。");
        const string sql="""
        INSERT dbo.WholesaleSamples(PlanItemId,SupplierId,Status,CostCny,TrackingNumber,Notes)
        OUTPUT INSERTED.Id VALUES(@PlanItemId,@SupplierId,@Status,@CostCny,@TrackingNumber,@Notes)
        """;
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        return (await db.QueryAsync<long>(sql,command,cancellationToken:token)).Single();
    }

    public async Task<QuoteDecisionResult> ApproveQuoteAsync(QuoteDecisionCommand command,string recipient,CancellationToken token=default)
    {
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var rows=await db.QueryAsync<PlanForApproval>("SELECT Id,TotalCostUsd,ProfitRate,Status FROM dbo.WholesalePurchasePlans WITH(UPDLOCK,ROWLOCK) WHERE Id=@PlanId",new{command.PlanId},cancellationToken:token);
            var plan=rows.SingleOrDefault()??throw new InvalidOperationException("采购计划不存在。");
            if(plan.Status!="PendingApproval") throw new InvalidOperationException("当前状态不能批准报价。");
            ProcurementPricing.ValidateFinalQuote(command.QuoteUsd,plan.TotalCostUsd??0);
            var margin=(command.QuoteUsd-plan.TotalCostUsd!.Value)/command.QuoteUsd;
            var approvals=await db.QueryAsync<long>("""
            INSERT dbo.WholesaleQuoteApprovals(PlanId,Status,QuoteUsd,ProfitRate,SubmittedByStaffId,DecidedByStaffId,DecisionNote,DecidedAtUtc)
            OUTPUT INSERTED.Id VALUES(@PlanId,'Approved',@QuoteUsd,@Margin,@StaffId,@StaffId,@Note,SYSUTCDATETIME())
            """,new{command.PlanId,command.QuoteUsd,Margin=margin,command.StaffId,command.Note},cancellationToken:token);
            var approvalId=approvals.Single();
            await db.ExecuteAsync("UPDATE dbo.WholesalePurchasePlans SET Status='Approved',FinalQuoteUsd=@QuoteUsd,ApprovedByStaffId=@StaffId,ApprovedAtUtc=SYSUTCDATETIME(),UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@PlanId",command,cancellationToken:token);
            var tasks=await db.QueryAsync<long>("""
            INSERT dbo.WholesaleMailTasks(PlanId,ApprovalId,TemplateCode,Recipient,PayloadJson)
            OUTPUT INSERTED.Id VALUES(@PlanId,@ApprovalId,'wholesale.final-quote',@Recipient,@Payload)
            """,new{command.PlanId,ApprovalId=approvalId,Recipient=recipient,Payload=System.Text.Json.JsonSerializer.Serialize(new{command.PlanId,approvalId,command.QuoteUsd})},cancellationToken:token);
            await db.CommitAsync(token);
            return new(approvalId,tasks.Single(),"Approved");
        }
        catch{await db.RollbackAsync(token);throw;}
    }

    public async Task<QuoteDecisionResult> RejectQuoteAsync(QuoteDecisionCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Note)) throw new InvalidOperationException("退回时必须填写原因。");
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        var ids=await db.QueryAsync<long>("""
        INSERT dbo.WholesaleQuoteApprovals(PlanId,Status,QuoteUsd,ProfitRate,SubmittedByStaffId,DecidedByStaffId,DecisionNote,DecidedAtUtc)
        OUTPUT INSERTED.Id
        SELECT Id,'Rejected',@QuoteUsd,COALESCE(ProfitRate,0),@StaffId,@StaffId,@Note,SYSUTCDATETIME()
        FROM dbo.WholesalePurchasePlans WHERE Id=@PlanId AND Status='PendingApproval'
        """,command,cancellationToken:token);
        if(ids.Count!=1) throw new InvalidOperationException("当前状态不能退回报价。");
        await db.ExecuteAsync("UPDATE dbo.WholesalePurchasePlans SET Status='Rejected',UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@PlanId",new{command.PlanId},cancellationToken:token);
        return new(ids.Single(),null,"Rejected");
    }

    private sealed record PlanForApproval(long Id,decimal? TotalCostUsd,decimal? ProfitRate,string Status);
}
