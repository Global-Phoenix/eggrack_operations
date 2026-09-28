using Eggrack.Operations.Domain.Modules.Security;

namespace Eggrack.Operations.Application.Modules.Security;

public sealed class PermissionEvaluator
{
    public AuthorizationDecision Evaluate(
        StaffAuthorization authorization,
        string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        var grants = authorization.Grants
            .Where(x => string.Equals(
                x.PermissionCode,
                permissionCode,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (grants.Any(x => string.Equals(x.Effect, "DENY", StringComparison.OrdinalIgnoreCase)))
            return Denied(authorization, permissionCode);

        var allowed = grants
            .Where(x => string.Equals(x.Effect, "ALLOW", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (allowed.Length == 0)
            return Denied(authorization, permissionCode);

        var scope = allowed.Max(x => x.Scope);
        var departmentIds = allowed
            .Where(x => x.DepartmentId.HasValue)
            .Select(x => x.DepartmentId!.Value)
            .ToHashSet();

        return new AuthorizationDecision(
            true,
            permissionCode,
            scope,
            authorization.StaffRef,
            authorization.AuthVersion,
            departmentIds);
    }

    private static AuthorizationDecision Denied(
        StaffAuthorization authorization,
        string permissionCode) =>
        new(
            false,
            permissionCode,
            DataScope.None,
            authorization.StaffRef,
            authorization.AuthVersion,
            new HashSet<long>());
}

