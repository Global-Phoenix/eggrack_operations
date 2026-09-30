using System.Data.Common;
using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService(DatabaseSessionFactory databases,ProcurementScopePolicy scopePolicy)
{
    private const string DatabaseName = "Eggrack";

    private sealed record PlanIdentityRow(uint Id,uint RequestId,uint RequestVersionId,byte Status);
    private sealed record PlanRequestVersionRow(uint Id,uint RequestId,uint VersionNumber,string RequestNumber,string CustomerName,string Email,DateTime SubmittedAtUtc);

    private sealed record ProcurementPlanRow(
        uint Id, string PlanNumber, string RequestNumber, uint RequestVersion, string CustomerName,
        string ProductSummary, string? BuyerName, decimal? TotalCostCny, decimal? FinalQuoteUsd,
        string Status, DateTime UpdatedAtUtc);


    public async Task<IReadOnlyList<PurchaseRequestSource>> GetPurchaseRequestsAsync(string? keyword,CancellationToken token=default)
    {
        var scope=scopePolicy.Current();
        const string sql = """
        SELECT r.id Id,v.id CurrentVersionId,r.request_number RequestNumber,r.current_version CurrentVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,r.email Email,
          (SELECT COUNT(*) FROM purchase_request_version_items vi WHERE vi.version_id=v.id) ProductCount,
          r.status RequestStatus,FROM_UNIXTIME(r.created_at) CreatedAtUtc,
          FROM_UNIXTIME(r.last_submitted_at) SubmittedAtUtc,
          FROM_UNIXTIME(COALESCE(p.updated_at,r.last_submitted_at)) UpdatedAtUtc,
          p.id PlanId,p.plan_number PlanNumber,p.request_version_id PlannedVersionId,p.assigned_buyer_id BuyerId,s.staff_name BuyerName,
          p.status PlanStatus,
          p.plan_title PlanTitle,p.priority_code Priority,p.planned_start_date PlannedStartDate,
          p.target_completion_date TargetCompletionDate,FROM_UNIXTIME(p.completed_at) CompletedAtUtc,p.internal_note InternalNote
        FROM purchase_requests r
        JOIN purchase_request_versions v ON v.request_id=r.id AND v.version_number=r.current_version
        LEFT JOIN purchase_plans p ON p.request_id=r.id
        LEFT JOIN eggrack_auth_staff s ON s.id=p.assigned_buyer_id
        WHERE (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
          OR p.department_id IN @ScopeDepartmentIds)
          AND (@Keyword IS NULL OR r.request_number LIKE CONCAT('%',@Keyword,'%')
          OR v.company_name LIKE CONCAT('%',@Keyword,'%') OR r.email LIKE CONCAT('%',@Keyword,'%'))
        ORDER BY r.last_submitted_at DESC LIMIT 200
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<PurchaseRequestSource>(sql,
          ProcurementScopePolicy.Params(scope,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()}),
          cancellationToken:token);
    }

    public async Task<PurchaseRequestSource?> GetPurchaseRequestAsync(uint requestId,CancellationToken token=default)
    {
        var scope=scopePolicy.Current();
        const string sql="""
        SELECT r.id Id,v.id CurrentVersionId,r.request_number RequestNumber,r.current_version CurrentVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,r.email Email,
          (SELECT COUNT(*) FROM purchase_request_version_items vi WHERE vi.version_id=v.id) ProductCount,
          r.status RequestStatus,FROM_UNIXTIME(r.created_at) CreatedAtUtc,
          FROM_UNIXTIME(r.last_submitted_at) SubmittedAtUtc,
          FROM_UNIXTIME(COALESCE(p.updated_at,r.last_submitted_at)) UpdatedAtUtc,
          p.id PlanId,p.plan_number PlanNumber,p.request_version_id PlannedVersionId,p.assigned_buyer_id BuyerId,s.staff_name BuyerName,
          p.status PlanStatus,
          p.plan_title PlanTitle,p.priority_code Priority,p.planned_start_date PlannedStartDate,
          p.target_completion_date TargetCompletionDate,FROM_UNIXTIME(p.completed_at) CompletedAtUtc,p.internal_note InternalNote
        FROM purchase_requests r
        JOIN purchase_request_versions v ON v.request_id=r.id AND v.version_number=r.current_version
        LEFT JOIN purchase_plans p ON p.request_id=r.id
        LEFT JOIN eggrack_auth_staff s ON s.id=p.assigned_buyer_id
        WHERE r.id=@RequestId
          AND (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
            OR p.department_id IN @ScopeDepartmentIds)
        LIMIT 1
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return (await db.QueryAsync<PurchaseRequestSource>(
          sql,ProcurementScopePolicy.Params(scope,new{RequestId=requestId}),cancellationToken:token)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<ProcurementPlanListItem>> GetPlansAsync(CancellationToken token=default)
    {
        var scope=scopePolicy.Current();
        const string sql = """
        SELECT p.id Id,p.plan_number PlanNumber,r.request_number RequestNumber,v.version_number RequestVersion,
          COALESCE(v.company_name,v.contact_name) CustomerName,
          COALESCE((SELECT i.product_name FROM purchase_plan_items i WHERE i.plan_id=p.id ORDER BY i.sort_order,i.id LIMIT 1),'—') ProductSummary,
          s.staff_name BuyerName,p.total_cost_cny TotalCostCny,p.approved_quote_amount_usd FinalQuoteUsd,
          CASE p.status WHEN 1 THEN 'Draft' WHEN 2 THEN 'Sourcing' WHEN 3 THEN 'PendingApproval'
            WHEN 4 THEN 'Approved' WHEN 5 THEN 'Completed' WHEN 6 THEN 'PendingFinalApproval'
            WHEN 7 THEN 'Rejected' WHEN 8 THEN 'EmailPending' ELSE 'Draft' END Status,
          FROM_UNIXTIME(p.updated_at) UpdatedAtUtc
        FROM purchase_plans p
        JOIN purchase_requests r ON r.id=p.request_id
        JOIN purchase_request_versions v ON v.id=p.request_version_id AND v.request_id=p.request_id
        LEFT JOIN eggrack_auth_staff s ON s.id=p.assigned_buyer_id
        WHERE (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
          OR p.department_id IN @ScopeDepartmentIds)
        ORDER BY p.updated_at DESC,p.id DESC
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var rows = await db.QueryAsync<ProcurementPlanRow>(
          sql,ProcurementScopePolicy.Params(scope,new{}),cancellationToken:token);
        return rows.Select(row => new ProcurementPlanListItem(
            row.Id, row.PlanNumber, row.RequestNumber, checked((int)row.RequestVersion),
            row.CustomerName, row.ProductSummary, row.BuyerName, row.TotalCostCny,
            row.FinalQuoteUsd, ProcurementPlanStatusParser.Parse(row.Status), row.UpdatedAtUtc)).ToArray();
    }

    public async Task<uint> CreatePlanAsync(uint requestId,SaveProcurementPlanInput input,ulong staffId,CancellationToken token=default)
    {
        ValidatePlanInput(input);
        const string versionSql="SELECT v.id Id,v.request_id RequestId,v.version_number VersionNumber,r.request_number RequestNumber,COALESCE(v.company_name,v.contact_name) CustomerName,v.email Email,FROM_UNIXTIME(v.submitted_at) SubmittedAtUtc FROM purchase_request_versions v JOIN purchase_requests r ON r.id=v.request_id WHERE v.id=@VersionId AND v.request_id=@RequestId";
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var departmentId=await scopePolicy.ResolveBuyerDepartmentAsync(db,input.AssignedBuyerStaffId,token);
        var version=(await db.QueryAsync<PlanRequestVersionRow>(versionSql,new{RequestId=requestId,VersionId=input.RequestVersionId},cancellationToken:token)).SingleOrDefault()
            ?? throw new InvalidOperationException("采购申请版本不存在。");
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var existing=await db.QueryAsync<uint>("SELECT id FROM purchase_plans WHERE request_id=@RequestId FOR UPDATE",new{RequestId=requestId},cancellationToken:token);
            if(existing.Count>0) throw new InvalidOperationException("该采购申请已经创建采购计划，不能重复创建。");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var number="PP-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
            var title=$"{version.RequestNumber} 采购计划";
            var startDate=DateTime.Now.Date;
            await db.ExecuteAsync("""
            INSERT purchase_plans(plan_number,plan_title,request_id,request_version_id,status,priority_code,planned_start_date,target_completion_date,assigned_buyer_id,department_id,assigned_by,assigned_at,internal_note,created_by,updated_by,created_at,updated_at)
            VALUES(@Number,@Title,@RequestId,@VersionId,@Status,@Priority,@StartDate,@TargetDate,@BuyerId,@DepartmentId,@StaffId,@AssignedAt,@InternalNote,@StaffId,@StaffId,@Now,@Now)
            """,new{Number=number,Title=title,RequestId=requestId,VersionId=input.RequestVersionId,Status=2,Priority=input.Priority,StartDate=startDate,TargetDate=(DateTime?)null,BuyerId=input.AssignedBuyerStaffId,DepartmentId=departmentId,StaffId=staffId,AssignedAt=now,InternalNote=Clean(input.InternalNote),Now=now},cancellationToken:token);
            var planId=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
            await SynchronizePlanItemsAsync(db,planId,requestId,input.RequestVersionId,input.Items,staffId,now,token);
            await db.ExecuteAsync(
                "UPDATE purchase_requests SET status=2,updated_at=@Now WHERE id=@RequestId AND status<2",
                new{RequestId=requestId,Now=now},cancellationToken:token);
            await db.CommitAsync(token);
            return planId;
        }
        catch(DbException error) when(error.Message.Contains("uk_purchase_plans_request_id",StringComparison.OrdinalIgnoreCase))
        {
            await db.RollbackAsync(token);
            throw new InvalidOperationException("该采购申请已经创建采购计划，不能重复创建。",error);
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
        WHERE id=@PlanId AND status IN (1,2,3)
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,checked((uint)planId),token);
        var changed=await db.ExecuteAsync(sql,new{PlanId=planId,input.PurchaseCostCny,input.PackagingCostCny,input.SampleCostCny,input.DomesticShippingCny,input.InternationalShippingCny,input.OtherCostCny,result.TotalCostCny,input.CnyPerUsd,result.TotalCostUsd,input.ProfitRate,result.SuggestedQuoteUsd,StaffId=staffId,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        if(changed!=1) throw new InvalidOperationException("采购计划不存在或当前状态不能修改成本。");
    }
    public async Task<ProcurementCosts> GetFlexibleCostsAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        var plan=(await db.QueryAsync<FlexibleCostPlan>("SELECT COALESCE(cny_per_usd,7.12) CnyPerUsd,COALESCE(profit_rate,0.15) ProfitRate,COALESCE(total_cost_cny,0) TotalCostCny,COALESCE(total_cost_usd,0) TotalCostUsd,COALESCE(approved_quote_amount_usd,0) SuggestedQuoteUsd FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId},cancellationToken:token)).Single();
        var items=await db.QueryAsync<ProcurementCostItem>("SELECT id Id,cost_type_id TypeId,cost_name Name,amount_cny AmountCny,sort_order SortOrder FROM procurement_plan_cost_items WHERE plan_id=@PlanId ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:token);
        var types=await db.QueryAsync<ProcurementManagedOption>("SELECT id Id,type_code Code,type_name Name,description Description,NULL AllowedExtensions,NULL MaxFileSizeMb FROM procurement_cost_types WHERE is_active=1 ORDER BY sort_order,id",cancellationToken:token);
        return new(plan.CnyPerUsd,plan.ProfitRate,plan.TotalCostCny,plan.TotalCostUsd,plan.SuggestedQuoteUsd,items,types);
    }
    public async Task<ProcurementCosts> SaveFlexibleCostsAsync(uint planId,SaveProcurementCostsCommand command,long staffId,CancellationToken token=default)
    {
        if(command.CnyPerUsd<=0)throw new ArgumentOutOfRangeException(nameof(command.CnyPerUsd),"汇率必须大于 0。");
        if(command.ProfitRate is <0.10m or >0.20m)throw new ArgumentOutOfRangeException(nameof(command.ProfitRate),"利润率必须在 10% 到 20% 之间。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        var types=await db.QueryAsync<ProcurementManagedOption>("SELECT id Id,type_code Code,type_name Name,description Description,NULL AllowedExtensions,NULL MaxFileSizeMb FROM procurement_cost_types WHERE is_active=1 ORDER BY sort_order,id",cancellationToken:token);
        var typeMap=types.ToDictionary(x=>x.Id);
        var items=(command.Items??[]).Where(x=>x.TypeId.HasValue||!string.IsNullOrWhiteSpace(x.Name)).Select(x=>
        {
            if(x.TypeId.HasValue&&!typeMap.TryGetValue(x.TypeId.Value,out _))throw new InvalidOperationException("成本类型不存在或已停用。");
            var name=x.TypeId.HasValue?typeMap[x.TypeId.Value].Name:x.Name?.Trim();
            if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("自定义成本必须填写名称。");
            return new ProcurementCostItemInput(x.TypeId,name,x.AmountCny);
        }).ToArray();
        if(items.Length==0)throw new InvalidOperationException("请至少添加一项成本。");
        if(items.Any(x=>x.AmountCny<0))throw new InvalidOperationException("成本金额不能为负数。");
        var total=items.Sum(x=>x.AmountCny);var usd=decimal.Round(total/command.CnyPerUsd,2,MidpointRounding.AwayFromZero);var quote=decimal.Round(usd/(1-command.ProfitRate),2,MidpointRounding.AwayFromZero);
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("DELETE FROM procurement_plan_cost_items WHERE plan_id=@PlanId",new{PlanId=planId},cancellationToken:transactionToken);
            for(var index=0;index<items.Length;index++)await db.ExecuteAsync("INSERT procurement_plan_cost_items(plan_id,cost_type_id,cost_name,amount_cny,sort_order,created_by,updated_by,created_at,updated_at) VALUES(@PlanId,@TypeId,@Name,@Amount,@Sort,@StaffId,@StaffId,@Now,@Now)",new{PlanId=planId,items[index].TypeId,items[index].Name,Amount=items[index].AmountCny,Sort=index,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET product_cost_cny=0,packaging_cost_cny=0,sample_cost_cny=0,domestic_shipping_cny=0,international_shipping_cny=0,other_cost_cny=@Total,total_cost_cny=@Total,cny_per_usd=@Rate,total_cost_usd=@Usd,profit_method=2,profit_rate=@Profit,approved_quote_amount_usd=@Quote,status=3,cost_updated_by=@StaffId,cost_updated_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status IN(1,2,3)",new{PlanId=planId,Total=total,Rate=command.CnyPerUsd,Usd=usd,Profit=command.ProfitRate,Quote=quote,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            if(changed!=1)throw new InvalidOperationException("采购计划当前状态不能修改成本。");
        },cancellationToken:token);
        var saved=await GetFlexibleCostsAsync(planId,token);
        return new(command.CnyPerUsd,command.ProfitRate,total,usd,quote,saved.Items,saved.Types);
    }
    private sealed record FlexibleCostPlan(decimal CnyPerUsd,decimal ProfitRate,decimal TotalCostCny,decimal TotalCostUsd,decimal SuggestedQuoteUsd);
    public async Task<IReadOnlyList<ProcurementBuyerOption>> GetBuyersAsync(CancellationToken token=default)
    {
        var scope=scopePolicy.Current();
        const string sql="""
        SELECT DISTINCT s.id Id,s.staff_name Name
        FROM eggrack_auth_staff s
        JOIN eggrack_auth_staff_department sd ON sd.staff_id=s.id
        JOIN eggrack_auth_department d ON d.id=sd.department_id
          AND (LOWER(d.department_code) IN('procurement','purchasing','purchase') OR d.department_name='采购部')
          AND d.status=1 AND d.deleted_at IS NULL
        WHERE s.status=1 AND s.deleted_at IS NULL
          AND (@ScopeAll=1 OR (@ScopeSelf=1 AND s.id=@ScopeStaffId)
            OR sd.department_id IN @ScopeDepartmentIds)
        ORDER BY s.staff_name,s.id
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<ProcurementBuyerOption>(
          sql,ProcurementScopePolicy.Params(scope,new{}),cancellationToken:token);
    }
    public async Task<IReadOnlyList<PurchaseRequestVersionDetail>> GetRequestVersionsAsync(uint requestId,CancellationToken token=default)
    {
        var scope=scopePolicy.Current();
        const string sql="""
        SELECT v.id Id,v.request_id RequestId,v.version_number VersionNumber,
          v.company_name CompanyName,v.contact_name ContactName,v.email Email,
          v.phone Phone,v.whatsapp Whatsapp,v.country Country,v.delivery_address DeliveryAddress,
          v.pickup_trade_info PickupTradeInfo,v.customer_message CustomerMessage,
          FROM_UNIXTIME(v.submitted_at) SubmittedAtUtc
        FROM purchase_request_versions v
        LEFT JOIN purchase_plans p ON p.request_id=v.request_id
        WHERE v.request_id=@RequestId
          AND (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
            OR p.department_id IN @ScopeDepartmentIds)
        ORDER BY v.version_number DESC
        """;
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var versions=await db.QueryAsync<PurchaseRequestVersionSource>(
          sql,ProcurementScopePolicy.Params(scope,new{RequestId=requestId}),cancellationToken:token);
        if(versions.Count==0)return [];
        const string itemSql="""
        SELECT id Id,version_id VersionId,CAST(product_key AS CHAR(64)) ProductKey,
          product_name ProductName,quantity Quantity,
          quantity_unit Unit,sku Sku,brand Brand,description Description,reference_url ReferenceUrl,
          target_unit_price TargetUnitPrice,currency Currency,specifications Specifications,
          color Color,size Size,packaging_requirements PackagingRequirements,
          customization_requirements CustomizationRequirements,customer_note CustomerNote
        FROM purchase_request_version_items
        WHERE request_id=@RequestId AND version_id IN @VersionIds
        ORDER BY version_id,sort_order,id
        """;
        var items=await db.QueryAsync<PurchaseRequestVersionItemDetail>(
          itemSql,new{RequestId=requestId,VersionIds=versions.Select(x=>x.Id).ToArray()},cancellationToken:token);
        const string attachmentSql="""
        SELECT f.id Id,f.version_id VersionId,f.version_item_id VersionItemId,f.file_type FileType,
          f.original_name OriginalName,f.mime_type MimeType,f.file_size FileSize,
          FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc,i.product_name RelatedProductName
        FROM purchase_request_files f
        LEFT JOIN purchase_request_version_items i ON i.id=f.version_item_id AND i.version_id=f.version_id
        WHERE f.request_id=@RequestId AND f.version_id IN @VersionIds
        ORDER BY f.version_id,f.uploaded_at,f.id
        """;
        var attachments=await db.QueryAsync<PurchaseRequestAttachmentDetail>(
          attachmentSql,new{RequestId=requestId,VersionIds=versions.Select(x=>x.Id).ToArray()},cancellationToken:token);
        return versions.Select(version=>new PurchaseRequestVersionDetail(
          version.Id,version.RequestId,version.VersionNumber,version.CompanyName,version.ContactName,
          version.Email,version.Phone,version.Whatsapp,version.Country,version.DeliveryAddress,
          version.PickupTradeInfo,version.CustomerMessage,version.SubmittedAtUtc,
          items.Where(item=>item.VersionId==version.Id).ToArray(),
          attachments.Where(file=>file.VersionId==version.Id).ToArray())).ToArray();
    }

    public async Task<ProcurementPlanEditor> GetPlanEditorAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        var plan=(await db.QueryAsync<ProcurementPlanEditor>("SELECT id Id,request_id RequestId,request_version_id RequestVersionId,assigned_buyer_id AssignedBuyerStaffId,plan_number PlanNumber,COALESCE(plan_title,plan_number) PlanTitle,priority_code Priority,planned_start_date PlannedStartDate,target_completion_date TargetCompletionDate,FROM_UNIXTIME(completed_at) CompletedAtUtc,internal_note InternalNote FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId},cancellationToken:token)).SingleOrDefault()
          ?? throw new InvalidOperationException("采购计划不存在。");
        plan.Items=await db.QueryAsync<ProcurementPlanItemOption>("SELECT id Id,request_item_id RequestItemId,product_name ProductName,quantity Quantity,quantity_unit Unit,sku Sku,brand Brand,description Description,specifications Specifications,color Color,size Size,packaging_requirements PackagingRequirements,customization_requirements CustomizationRequirements,internal_note InternalNote FROM purchase_plan_items WHERE plan_id=@PlanId ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:token);
        return plan;
    }

    public async Task UpdatePlanAsync(uint planId,SaveProcurementPlanInput input,ulong staffId,CancellationToken token=default)
    {
        ValidatePlanInput(input);
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        var departmentId=await scopePolicy.ResolveBuyerDepartmentAsync(db,input.AssignedBuyerStaffId,token);
        await db.BeginTransactionAsync(cancellationToken:token);
        try
        {
            var plan=(await db.QueryAsync<PlanIdentityRow>("SELECT id Id,request_id RequestId,request_version_id RequestVersionId,status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:token)).SingleOrDefault()
                ?? throw new InvalidOperationException("采购计划不存在。");
            if(plan.Status>2) throw new InvalidOperationException("采购计划已进入成本或审批阶段，不能修改计划资料。");
            var versions=await db.QueryAsync<uint>("SELECT id FROM purchase_request_versions WHERE id=@VersionId AND request_id=@RequestId",new{VersionId=input.RequestVersionId,plan.RequestId},cancellationToken:token);
            if(versions.Count!=1) throw new InvalidOperationException("采购申请版本与计划不匹配。");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("UPDATE purchase_plans SET request_version_id=@VersionId,priority_code=@Priority,assigned_buyer_id=@BuyerId,department_id=@DepartmentId,assigned_by=@StaffId,assigned_at=@Now,internal_note=@InternalNote,status=2,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId",new{PlanId=planId,VersionId=input.RequestVersionId,Priority=input.Priority,BuyerId=input.AssignedBuyerStaffId,DepartmentId=departmentId,StaffId=staffId,InternalNote=Clean(input.InternalNote),Now=now},cancellationToken:token);
            await SynchronizePlanItemsAsync(db,planId,plan.RequestId,input.RequestVersionId,input.Items,staffId,now,token);
            await db.CommitAsync(token);
        }
        catch { await db.RollbackAsync(token); throw; }
    }

    private static void ValidatePlanInput(SaveProcurementPlanInput input)
    {
        if(input.AssignedBuyerStaffId==0)throw new InvalidOperationException("请选择采购人员。");
        if(input.Priority is not("low" or "normal" or "high" or "urgent"))throw new InvalidOperationException("采购计划优先级无效。");
        if(input.Items is null||input.Items.Count==0)throw new InvalidOperationException("请至少录入一个内部计划产品。");
        if(input.Items.Any(x=>string.IsNullOrWhiteSpace(x.ProductName)||x.Quantity<=0||string.IsNullOrWhiteSpace(x.Unit)))throw new InvalidOperationException("计划产品必须填写名称、有效数量和单位。");
        if(input.Items.Where(x=>x.Id.HasValue).GroupBy(x=>x.Id).Any(group=>group.Count()>1))throw new InvalidOperationException("计划产品数据重复，请刷新后重试。");
    }

    private static async Task SynchronizePlanItemsAsync(DatabaseSession db,uint planId,uint requestId,uint versionId,IReadOnlyList<SaveProcurementPlanItemCommand> items,ulong staffId,long now,CancellationToken token)
    {
        var sourceIds=(await db.QueryAsync<uint>("SELECT id FROM purchase_request_version_items WHERE request_id=@RequestId AND version_id=@VersionId",new{RequestId=requestId,VersionId=versionId},cancellationToken:token)).ToHashSet();
        var existingIds=(await db.QueryAsync<uint>("SELECT id FROM purchase_plan_items WHERE plan_id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:token)).ToHashSet();
        var retained=new HashSet<uint>();
        for(var index=0;index<items.Count;index++)
        {
            var item=items[index];
            var sourceId=item.RequestItemId.HasValue&&sourceIds.Contains(item.RequestItemId.Value)?item.RequestItemId:null;
            var values=new{PlanId=planId,RequestItemId=sourceId,ProductName=item.ProductName.Trim(),item.Quantity,Unit=item.Unit.Trim(),Sku=Clean(item.Sku),Brand=Clean(item.Brand),Description=Clean(item.Description),Specifications=Clean(item.Specifications),Color=Clean(item.Color),Size=Clean(item.Size),PackagingRequirements=Clean(item.PackagingRequirements),CustomizationRequirements=Clean(item.CustomizationRequirements),InternalNote=Clean(item.InternalNote),Sort=index+1,StaffId=staffId,Now=now};
            if(item.Id.HasValue)
            {
                if(!existingIds.Contains(item.Id.Value))throw new InvalidOperationException("计划产品不属于当前采购计划。");
                await db.ExecuteAsync("UPDATE purchase_plan_items SET request_item_id=@RequestItemId,sort_order=@Sort,product_name=@ProductName,sku=@Sku,brand=@Brand,description=@Description,quantity=@Quantity,quantity_unit=@Unit,specifications=@Specifications,color=@Color,size=@Size,packaging_requirements=@PackagingRequirements,customization_requirements=@CustomizationRequirements,internal_note=@InternalNote,updated_by=@StaffId,updated_at=@Now WHERE id=@Id AND plan_id=@PlanId",new{item.Id,values.PlanId,values.RequestItemId,values.ProductName,values.Quantity,values.Unit,values.Sku,values.Brand,values.Description,values.Specifications,values.Color,values.Size,values.PackagingRequirements,values.CustomizationRequirements,values.InternalNote,values.Sort,values.StaffId,values.Now},cancellationToken:token);
                retained.Add(item.Id.Value);
            }
            else
            {
                await db.ExecuteAsync("INSERT purchase_plan_items(plan_id,request_item_id,product_key,sort_order,product_name,sku,brand,description,quantity,quantity_unit,specifications,color,size,packaging_requirements,customization_requirements,internal_note,buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at) SELECT id,@RequestItemId,@ProductKey,@Sort,@ProductName,@Sku,@Brand,@Description,@Quantity,@Unit,@Specifications,@Color,@Size,@PackagingRequirements,@CustomizationRequirements,@InternalNote,assigned_buyer_id,@StaffId,@Now,@StaffId,@StaffId,@Now,@Now FROM purchase_plans WHERE id=@PlanId",new{values.PlanId,values.RequestItemId,ProductKey=$"PLAN-{Guid.NewGuid():N}",values.Sort,values.ProductName,values.Sku,values.Brand,values.Description,values.Quantity,values.Unit,values.Specifications,values.Color,values.Size,values.PackagingRequirements,values.CustomizationRequirements,values.InternalNote,values.StaffId,values.Now},cancellationToken:token);
            }
        }
        var removed=existingIds.Where(id=>!retained.Contains(id)).ToArray();
        if(removed.Length==0)return;
        var referenced=(await db.QueryAsync<long>("SELECT (SELECT COUNT(*) FROM procurement_candidate_products WHERE plan_item_id IN @Ids)+(SELECT COUNT(*) FROM procurement_inquiries WHERE plan_item_id IN @Ids)+(SELECT COUNT(*) FROM procurement_samples WHERE plan_item_id IN @Ids)",new{Ids=removed},cancellationToken:token)).Single();
        if(referenced>0)throw new InvalidOperationException("被删除的计划产品已有询价或样品记录，请保留该产品后再保存。");
        await db.ExecuteAsync("DELETE FROM purchase_plan_items WHERE plan_id=@PlanId AND id IN @Ids",new{PlanId=planId,Ids=removed},cancellationToken:token);
    }
}
