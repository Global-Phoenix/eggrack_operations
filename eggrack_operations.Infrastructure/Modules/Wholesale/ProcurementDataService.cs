using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService(DatabaseSessionFactory databases)
{
    private const string DatabaseName = "Eggrack";

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
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<PurchaseRequestSource>(sql,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()},cancellationToken:token);
    }

    public async Task<IReadOnlyList<ProcurementPlanListItem>> GetPlansAsync(CancellationToken token=default)
    {
        const string sql = """
        SELECT p.id Id,p.plan_number PlanNumber,r.request_number RequestNumber,v.version_number RequestVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,
          COALESCE((SELECT i.product_name FROM purchase_plan_items i WHERE i.plan_id=p.id ORDER BY i.sort_order,i.id LIMIT 1),'—') ProductSummary,
          s.staff_name BuyerName,p.total_cost_cny TotalCostCny,p.approved_quote_amount_usd FinalQuoteUsd,
          CASE p.status WHEN 1 THEN 'Draft' WHEN 2 THEN 'Sourcing' WHEN 3 THEN 'PendingApproval'
            WHEN 4 THEN 'Approved' WHEN 5 THEN 'Completed' ELSE 'Draft' END Status,
          FROM_UNIXTIME(p.updated_at) UpdatedAtUtc
        FROM purchase_plans p
        JOIN purchase_requests r ON r.id=p.request_id
        JOIN purchase_request_versions v ON v.id=p.request_version_id AND v.request_id=p.request_id
        LEFT JOIN eggrack_auth_staff s ON s.id=p.assigned_buyer_id
        ORDER BY p.updated_at DESC,p.id DESC
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
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
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var version=(await db.QueryAsync<PurchaseRequestVersionSource>(versionSql,new{command.RequestId,VersionId=command.RequestVersionId},cancellationToken:token)).SingleOrDefault()
            ?? throw new InvalidOperationException("采购申请版本不存在。");
        var items=await db.QueryAsync<PurchaseRequestItemSource>(itemSql,new{command.RequestId,VersionId=command.RequestVersionId},cancellationToken:token);
        if(items.Count==0) throw new InvalidOperationException("采购申请版本没有产品。");

        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var number="PP-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
            await db.ExecuteAsync("""
            INSERT purchase_plans(plan_number,request_id,request_version_id,status,assigned_buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at)
            VALUES(@Number,@RequestId,@VersionId,@Status,@BuyerId,@StaffId,@AssignedAt,@StaffId,@StaffId,@Now,@Now)
            """,new{Number=number,command.RequestId,VersionId=command.RequestVersionId,Status=command.AssignedBuyerStaffId.HasValue?2:1,BuyerId=command.AssignedBuyerStaffId,StaffId=command.CreatedByStaffId,AssignedAt=command.AssignedBuyerStaffId.HasValue?now:(long?)null,Now=now},cancellationToken:token);
            var planId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            const string insertItem="""
            INSERT purchase_plan_items(plan_id,request_item_id,product_key,sort_order,product_name,quantity,quantity_unit,buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at)
            VALUES(@PlanId,@Id,@ProductKey,@SortOrder,@ProductName,@Quantity,@Unit,@BuyerId,@StaffId,@AssignedAt,@StaffId,@StaffId,@Now,@Now)
            """;
            var order=0;
            foreach(var item in items) await db.ExecuteAsync(insertItem,new{PlanId=planId,item.Id,item.ProductKey,SortOrder=order++,item.ProductName,item.Quantity,item.Unit,BuyerId=command.AssignedBuyerStaffId,StaffId=command.CreatedByStaffId,AssignedAt=command.AssignedBuyerStaffId.HasValue?now:(long?)null,Now=now},cancellationToken:token);
            await db.CommitAsync(token);
            return planId;
        }
        catch { await db.RollbackAsync(token); throw; }
    }

    public async Task SaveCostsAsync(long planId,ProcurementCostInput input,long staffId,CancellationToken token=default)
    {
        var result=ProcurementPricing.Calculate(input);
        const string sql="""
        UPDATE purchase_plans SET product_cost_cny=@PurchaseCostCny,packaging_cost_cny=@PackagingCostCny,
          sample_cost_cny=@SampleCostCny,domestic_shipping_cny=@DomesticShippingCny,international_shipping_cny=@InternationalShippingCny,
          other_cost_cny=@OtherCostCny,total_cost_cny=@TotalCostCny,cny_per_usd=@CnyPerUsd,total_cost_usd=@TotalCostUsd,
          profit_method=2,profit_rate=@ProfitRate,approved_quote_amount_usd=@SuggestedQuoteUsd,status=3,
          cost_updated_by=@StaffId,cost_updated_at=@Now,updated_by=@StaffId,updated_at=@Now
        WHERE id=@PlanId
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var changed=await db.ExecuteAsync(sql,new{PlanId=planId,input.PurchaseCostCny,input.PackagingCostCny,input.SampleCostCny,input.DomesticShippingCny,input.InternationalShippingCny,input.OtherCostCny,result.TotalCostCny,input.CnyPerUsd,result.TotalCostUsd,input.ProfitRate,result.SuggestedQuoteUsd,StaffId=staffId,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        if(changed!=1) throw new InvalidOperationException("采购计划不存在。");
    }
}
