using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Domain.Modules.Security;
using Eggrack.Operations.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed record ProcurementAccessScope(
    bool IsAll,
    bool AllowSelf,
    long StaffId,
    IReadOnlyList<long> DepartmentIds)
{
    public long[] SqlDepartmentIds => DepartmentIds.Count == 0 ? [0] : DepartmentIds.ToArray();
}

public sealed class ProcurementScopePolicy(CurrentAuthorizationContext authorization,IOptions<ProcurementOptions> procurementOptions)
{
    public ProcurementAccessScope Current(string? permissionCode = null)
    {
        var decision = authorization.Require(permissionCode);
        return new(
            decision.Scope == DataScope.All,
            decision.Scope is DataScope.Self or DataScope.SelfOrDepartment,
            decision.StaffId,
            decision.DepartmentIds.ToArray());
    }

    public async Task EnsurePlanAsync(DatabaseSession db, uint planId, CancellationToken token)
    {
        var scope = Current();
        var rows = await db.QueryAsync<uint>(
            """
            SELECT p.id FROM purchase_plans p
            WHERE p.id=@PlanId AND
              (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
               OR p.department_id IN @ScopeDepartmentIds)
            """,
            Params(scope, new { PlanId = planId }),
            cancellationToken: token);
        if (rows.Count != 1)
            throw new DataScopeDeniedException("采购计划不存在或不在当前部门数据范围内。");
    }

    public async Task EnsurePlanItemAsync(DatabaseSession db, uint planItemId, CancellationToken token)
    {
        var scope = Current();
        var rows = await db.QueryAsync<uint>(
            """
            SELECT i.id FROM purchase_plan_items i
            JOIN purchase_plans p ON p.id=i.plan_id
            WHERE i.id=@PlanItemId AND
              (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
               OR p.department_id IN @ScopeDepartmentIds)
            """,
            Params(scope, new { PlanItemId = planItemId }),
            cancellationToken: token);
        if (rows.Count != 1)
            throw new DataScopeDeniedException("采购计划产品不存在或不在当前部门数据范围内。");
    }

    public async Task EnsureRequestAsync(DatabaseSession db,uint requestId,CancellationToken token)
    {
        var scope=Current();
        var rows=await db.QueryAsync<uint>(
            """
            SELECT r.id FROM purchase_requests r
            LEFT JOIN purchase_plans p ON p.request_id=r.id
            WHERE r.id=@RequestId AND
              (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
               OR p.department_id IN @ScopeDepartmentIds)
            """,
            Params(scope,new{RequestId=requestId}),cancellationToken:token);
        if(rows.Count!=1)
            throw new DataScopeDeniedException("采购申请不存在或不在当前部门数据范围内。");
    }

    public async Task EnsureMailTaskAsync(DatabaseSession db, uint mailTaskId, CancellationToken token)
    {
        var scope = Current();
        var rows = await db.QueryAsync<uint>(
            """
            SELECT t.id FROM procurement_mail_tasks t
            JOIN purchase_plans p ON p.id=t.plan_id
            WHERE t.id=@MailTaskId AND
              (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
               OR p.department_id IN @ScopeDepartmentIds)
            """,
            Params(scope, new { MailTaskId = mailTaskId }),
            cancellationToken: token);
        if (rows.Count != 1)
            throw new DataScopeDeniedException("邮件任务不存在或不在当前部门数据范围内。");
    }

    public async Task<long> ResolveBuyerDepartmentAsync(
        DatabaseSession db,
        ulong buyerStaffId,
        CancellationToken token)
    {
        var rows = await db.QueryAsync<long>(
            """
            SELECT sd.department_id
            FROM eggrack_auth_staff_department sd
            JOIN eggrack_auth_staff s ON s.id=sd.staff_id AND s.status=1 AND s.deleted_at IS NULL
            JOIN eggrack_auth_department d ON d.id=sd.department_id
              AND LOWER(d.department_code) IN @BuyerDepartmentCodes
              AND d.status=1 AND d.deleted_at IS NULL
            WHERE sd.staff_id=@BuyerStaffId
            ORDER BY sd.is_primary DESC,sd.id
            LIMIT 1
            """,
            new { BuyerStaffId = buyerStaffId, BuyerDepartmentCodes = procurementOptions.Value.NormalizedBuyerDepartmentCodes() },
            cancellationToken: token);
        var departmentId = rows.SingleOrDefault();
        return departmentId > 0
            ? departmentId
            : throw new BusinessRuleException($"采购人员必须是启用状态并且属于{procurementOptions.Value.BuyerDepartmentLabel}。", "procurement.buyer.department-denied");
    }

    public static object Params(ProcurementAccessScope scope, object values)
    {
        var parameters = new Dapper.DynamicParameters(values);
        parameters.Add("ScopeAll", scope.IsAll);
        parameters.Add("ScopeSelf", scope.AllowSelf);
        parameters.Add("ScopeStaffId", scope.StaffId);
        parameters.Add("ScopeDepartmentIds", scope.SqlDepartmentIds);
        return parameters;
    }
}
