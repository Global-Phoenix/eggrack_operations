using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService(DatabaseSessionFactory databases)
{
    public async Task<IReadOnlyList<PurchaseRequestSource>> GetPurchaseRequestsAsync(string? keyword,CancellationToken token=default)
    {
        const string sql = """
        SELECT r.id Id,r.request_number RequestNumber,r.current_version CurrentVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,r.email Email,
          FROM_UNIXTIME(r.last_submitted_at) SubmittedAtUtc
        FROM purchase_requests r
        JOIN purchase_request_versions v ON v.request_id=r.id AND v.version_number=r.current_version
        WHERE (@Keyword IS NULL OR r.request_number LIKE CONCAT('%',@Keyword,'%')
          OR v.company_name LIKE CONCAT('%',@Keyword,'%') OR r.email LIKE CONCAT('%',@Keyword,'%'))
        ORDER BY r.last_submitted_at DESC LIMIT 200
        """;
        await using var db=await databases.OpenMySqlAsync("Eggrack",token);
        return await db.QueryAsync<PurchaseRequestSource>(sql,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()},cancellationToken:token);
    }

    public async Task<IReadOnlyList<ProcurementPlanListItem>> GetPlansAsync(CancellationToken token=default)
    {
        const string sql = """
        SELECT p.Id,p.PlanNumber,CONVERT(varchar(32),p.SourceRequestId) RequestNumber,1 RequestVersion,
          CONVERT(nvarchar(100),p.SourceRequestId) CustomerName,
          COALESCE((SELECT TOP 1 i.ProductName FROM dbo.WholesalePlanItems i WHERE i.PlanId=p.Id ORDER BY i.Id),'—') ProductSummary,
          CONVERT(nvarchar(100),p.AssignedBuyerStaffId) BuyerName,p.TotalCostCny,p.FinalQuoteUsd,p.Status,p.UpdatedAtUtc
        FROM dbo.WholesalePurchasePlans p ORDER BY p.UpdatedAtUtc DESC
        """;
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        return await db.QueryAsync<ProcurementPlanListItem>(sql,cancellationToken:token);
    }

    public async Task<long> CreatePlanAsync(CreateProcurementPlanCommand command,CancellationToken token=default)
    {
        const string versionSql="SELECT id Id,request_id RequestId,version_number VersionNumber,COALESCE(company_name,contact_name) CustomerName,email Email,FROM_UNIXTIME(submitted_at) SubmittedAtUtc FROM purchase_request_versions WHERE id=@VersionId AND request_id=@RequestId";
        const string itemSql="""
        SELECT id Id,request_id RequestId,version_id VersionId,product_key ProductKey,product_name ProductName,
          quantity Quantity,quantity_unit Unit,
          JSON_OBJECT('sku',sku,'brand',brand,'description',description,'specifications',specifications,'color',color,'size',size,'packagingRequirements',packaging_requirements,'customizationRequirements',customization_requirements,'customerNote',customer_note) SnapshotJson
        FROM purchase_request_version_items WHERE request_id=@RequestId AND version_id=@VersionId ORDER BY sort_order,id
        """;
        await using var source=await databases.OpenMySqlAsync("Eggrack",token);
        var version=(await source.QueryAsync<PurchaseRequestVersionSource>(versionSql,new{command.RequestId,VersionId=command.RequestVersionId},cancellationToken:token)).SingleOrDefault()
            ?? throw new InvalidOperationException("采购申请版本不存在。");
        var items=await source.QueryAsync<PurchaseRequestItemSource>(itemSql,new{command.RequestId,VersionId=command.RequestVersionId},cancellationToken:token);
        if(items.Count==0) throw new InvalidOperationException("采购申请版本没有产品。");

        await using var target=await databases.OpenSqlServerAsync("Orders",token);
        await target.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var number="PP-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
            var ids=await target.QueryAsync<long>("""
            INSERT dbo.WholesalePurchasePlans(PlanNumber,SourceRequestId,SourceRequestVersionId,Status,AssignedBuyerStaffId,CreatedByStaffId)
            OUTPUT INSERTED.Id VALUES(@Number,@RequestId,@VersionId,@Status,@BuyerId,@StaffId)
            """,new{Number=number,command.RequestId,VersionId=command.RequestVersionId,Status=command.AssignedBuyerStaffId.HasValue?"Sourcing":"Draft",BuyerId=command.AssignedBuyerStaffId,StaffId=command.CreatedByStaffId},cancellationToken:token);
            var planId=ids.Single();
            const string insertItem="""
            INSERT dbo.WholesalePlanItems(PlanId,SourceRequestItemId,ProductKey,ProductName,Quantity,Unit,BuyerStaffId,SnapshotJson)
            VALUES(@PlanId,@Id,@ProductKey,@ProductName,@Quantity,@Unit,@BuyerId,@SnapshotJson)
            """;
            foreach(var item in items) await target.ExecuteAsync(insertItem,new{PlanId=planId,item.Id,item.ProductKey,item.ProductName,item.Quantity,item.Unit,BuyerId=command.AssignedBuyerStaffId,item.SnapshotJson},cancellationToken:token);
            await target.CommitAsync(token);
            return planId;
        }
        catch { await target.RollbackAsync(token); throw; }
    }

    public async Task SaveCostsAsync(long planId,ProcurementCostInput input,long staffId,CancellationToken token=default)
    {
        var result=ProcurementPricing.Calculate(input);
        const string sql="""
        UPDATE dbo.WholesalePurchasePlans SET PurchaseCostCny=@PurchaseCostCny,PackagingCostCny=@PackagingCostCny,
          SampleCostCny=@SampleCostCny,DomesticShippingCny=@DomesticShippingCny,InternationalShippingCny=@InternationalShippingCny,
          OtherCostCny=@OtherCostCny,TotalCostCny=@TotalCostCny,CnyPerUsd=@CnyPerUsd,TotalCostUsd=@TotalCostUsd,
          ProfitRate=@ProfitRate,FinalQuoteUsd=@SuggestedQuoteUsd,Status='PendingApproval',UpdatedAtUtc=SYSUTCDATETIME()
        WHERE Id=@PlanId
        """;
        await using var db=await databases.OpenSqlServerAsync("Orders",token);
        var changed=await db.ExecuteAsync(sql,new{PlanId=planId,input.PurchaseCostCny,input.PackagingCostCny,input.SampleCostCny,input.DomesticShippingCny,input.InternationalShippingCny,input.OtherCostCny,result.TotalCostCny,input.CnyPerUsd,result.TotalCostUsd,input.ProfitRate,result.SuggestedQuoteUsd,StaffId=staffId},cancellationToken:token);
        if(changed!=1) throw new InvalidOperationException("采购计划不存在。");
    }
}

