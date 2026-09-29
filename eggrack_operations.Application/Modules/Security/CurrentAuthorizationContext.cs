using Eggrack.Operations.Domain.Modules.Security;

namespace Eggrack.Operations.Application.Modules.Security;

public sealed class CurrentAuthorizationContext
{
    private readonly Dictionary<string, AuthorizationDecision> decisions =
        new(StringComparer.OrdinalIgnoreCase);
    public AuthorizationDecision? Current { get; private set; }

    public void Set(AuthorizationDecision decision)
    {
        decisions[decision.PermissionCode] = decision;
        Current = Current is null ? decision : Intersect(Current, decision);
    }

    public AuthorizationDecision Require(string? permissionCode = null)
    {
        var decision = permissionCode is null ? Current : decisions.GetValueOrDefault(permissionCode);
        return decision is { Allowed: true }
            ? decision
            : throw new UnauthorizedAccessException("缺少有效的内部数据授权上下文。");
    }

    private static AuthorizationDecision Intersect(
        AuthorizationDecision left,
        AuthorizationDecision right)
    {
        if (left.StaffId != right.StaffId)
            throw new InvalidOperationException("同一请求中出现了不一致的人员授权上下文。");
        if (left.Scope == DataScope.All) return right;
        if (right.Scope == DataScope.All) return left;

        var departments = left.DepartmentIds.Intersect(right.DepartmentIds).ToHashSet();
        return right with
        {
            Scope = left.Scope < right.Scope ? left.Scope : right.Scope,
            DepartmentIds = departments
        };
    }
}
