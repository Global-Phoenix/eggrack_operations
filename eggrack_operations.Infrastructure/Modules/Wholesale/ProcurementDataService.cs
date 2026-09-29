using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService(DatabaseSessionFactory databases)
{
    private const string DatabaseName = "Eggrack";

    private sealed record PlanIdentityRow(uint Id,uint RequestId);

    private sealed record ProcurementPlanRow(
        uint Id, string PlanNumber, string RequestNumber, uint RequestVersion, string CustomerName,
        string ProductSummary, string? BuyerName, decimal? TotalCostCny, decimal? FinalQuoteUsd,
        string Status, DateTime UpdatedAtUtc);


    public async Task<IReadOnlyList<PurchaseRequestSource>> GetPurchaseRequestsAsync(string? keyword,CancellationToken token=default)
    {
        const string sql = """
        SELECT r.id Id,v.id CurrentVersionId,r.request_number RequestNumber,r.current_version CurrentVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,r.email Email,
          FROM_UNIXTIME(r.last_submitted_at) SubmittedAtUtc,
          p.id PlanId,p.plan_number PlanNumber,p.request_version_id PlannedVersionId,p.assigned_buyer_id BuyerId,s.staff_name BuyerName
        FROM purchase_requests r
        JOIN purchase_request_versions v ON v.request_id=r.id AND v.version_number=r.current_version
        LEFT JOIN purchase_plans p ON p.request_id=r.id
        LEFT JOIN eggrack_auth_staff s ON s.id=p.assigned_buyer_id
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
        var rows = await db.QueryAsync<ProcurementPlanRow>(sql,cancellationToken:token);
        return rows.Select(row => new ProcurementPlanListItem(
            row.Id, row.PlanNumber, row.RequestNumber, checked((int)row.RequestVersion),
            row.CustomerName, row.ProductSummary, row.BuyerName, row.TotalCostCny,
            row.FinalQuoteUsd, ProcurementPlanStatusParser.Parse(row.Status), row.UpdatedAtUtc)).ToArray();
    }

    public async Task<uint> CreatePlanAsync(CreateProcurementPlanCommand command,CancellationToken token=default)
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
            var existing=await db.QueryAsync<uint>("SELECT id FROM purchase_plans WHERE request_id=@RequestId FOR UPDATE",new{command.RequestId},cancellationToken:token);
            if(existing.Count>0) throw new InvalidOperationException("该采购申请已经创建采购计划，不能重复创建。");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var number="PP-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
            await db.ExecuteAsync("""
            INSERT purchase_plans(plan_number,request_id,request_version_id,status,assigned_buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at)
            VALUES(@Number,@RequestId,@VersionId,@Status,@BuyerId,@StaffId,@AssignedAt,@StaffId,@StaffId,@Now,@Now)
            """,new{Number=number,command.RequestId,VersionId=command.RequestVersionId,Status=2,BuyerId=command.AssignedBuyerStaffId,StaffId=command.CreatedByStaffId,AssignedAt=now,Now=now},cancellationToken:token);
            var planId=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            const string insertItem="""
            INSERT purchase_plan_items(plan_id,request_item_id,product_key,sort_order,product_name,quantity,quantity_unit,buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at)
            VALUES(@PlanId,@Id,@ProductKey,@SortOrder,@ProductName,@Quantity,@Unit,@BuyerId,@StaffId,@AssignedAt,@StaffId,@StaffId,@Now,@Now)
            """;
            var order=0;
            foreach(var item in items) await db.ExecuteAsync(insertItem,new{PlanId=planId,item.Id,item.ProductKey,SortOrder=order++,item.ProductName,item.Quantity,item.Unit,BuyerId=command.AssignedBuyerStaffId,StaffId=command.CreatedByStaffId,AssignedAt=now,Now=now},cancellationToken:token);
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
    public async Task<IReadOnlyList<ProcurementBuyerOption>> GetBuyersAsync(CancellationToken token=default)
    {
        const string sql="SELECT id Id,staff_name Name FROM eggrack_auth_staff WHERE status=1 AND deleted_at IS NULL ORDER BY staff_name,id";
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<ProcurementBuyerOption>(sql,cancellationToken:token);
    }
    public async Task<IReadOnlyList<PurchaseRequestVersionSource>> GetRequestVersionsAsync(uint requestId,CancellationToken token=default)
    {
        const string sql="SELECT id Id,request_id RequestId,version_number VersionNumber,COALESCE(company_name,contact_name) CustomerName,email Email,FROM_UNIXTIME(submitted_at) SubmittedAtUtc FROM purchase_request_versions WHERE request_id=@RequestId ORDER BY version_number DESC";
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<PurchaseRequestVersionSource>(sql,new{RequestId=requestId},cancellationToken:token);
    }

    public async Task UpdatePlanAsync(UpdateProcurementPlanCommand command,CancellationToken token=default)
    {
        const string itemSql="""
        SELECT id Id,request_id RequestId,version_id VersionId,product_key ProductKey,product_name ProductName,
          quantity Quantity,quantity_unit Unit,
          JSON_OBJECT('sku',sku,'brand',brand,'description',description,'specifications',specifications,'color',color,'size',size,'packagingRequirements',packaging_requirements,'customizationRequirements',customization_requirements,'customerNote',customer_note) SnapshotJson
        FROM purchase_request_version_items WHERE request_id=@RequestId AND version_id=@VersionId ORDER BY sort_order,id
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var plan=(await db.QueryAsync<PlanIdentityRow>("SELECT id Id,request_id RequestId FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:token)).SingleOrDefault()
                ?? throw new InvalidOperationException("采购计划不存在。");
            var versions=await db.QueryAsync<uint>("SELECT id FROM purchase_request_versions WHERE id=@VersionId AND request_id=@RequestId",new{VersionId=command.RequestVersionId,plan.RequestId},cancellationToken:token);
            if(versions.Count!=1) throw new InvalidOperationException("采购申请版本与计划不匹配。");
            var items=await db.QueryAsync<PurchaseRequestItemSource>(itemSql,new{plan.RequestId,VersionId=command.RequestVersionId},cancellationToken:token);
            if(items.Count==0) throw new InvalidOperationException("采购申请版本没有产品。");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("UPDATE purchase_plans SET request_version_id=@RequestVersionId,assigned_buyer_id=@AssignedBuyerStaffId,assigned_by=@UpdatedByStaffId,assigned_at=@Now,status=2,updated_by=@UpdatedByStaffId,updated_at=@Now WHERE id=@PlanId",new{command.PlanId,command.RequestVersionId,command.AssignedBuyerStaffId,command.UpdatedByStaffId,Now=now},cancellationToken:token);
            await db.ExecuteAsync("DELETE FROM purchase_plan_items WHERE plan_id=@PlanId",new{command.PlanId},cancellationToken:token);
            const string insertItem="""
            INSERT purchase_plan_items(plan_id,request_item_id,product_key,sort_order,product_name,quantity,quantity_unit,buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at)
            VALUES(@PlanId,@Id,@ProductKey,@SortOrder,@ProductName,@Quantity,@Unit,@BuyerId,@StaffId,@Now,@StaffId,@StaffId,@Now,@Now)
            """;
            var order=0;
            foreach(var item in items) await db.ExecuteAsync(insertItem,new{PlanId=command.PlanId,item.Id,item.ProductKey,SortOrder=order++,item.ProductName,item.Quantity,item.Unit,BuyerId=command.AssignedBuyerStaffId,StaffId=command.UpdatedByStaffId,Now=now},cancellationToken:token);
            await db.CommitAsync(token);
        }
        catch { await db.RollbackAsync(token); throw; }
    }
}
