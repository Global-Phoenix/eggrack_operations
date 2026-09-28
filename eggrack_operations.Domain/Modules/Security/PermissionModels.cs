namespace Eggrack.Operations.Domain.Modules.Security;

public enum DataScope
{
    None = 0,
    Self = 10,
    SelfOrDepartment = 20,
    Department = 30,
    All = 40
}

public sealed record PermissionGrant(
    string PermissionCode,
    string Effect,
    DataScope Scope,
    long? DepartmentId);

public sealed record StaffAuthorization(
    long StaffId,
    string StaffRef,
    long AuthVersion,
    IReadOnlyList<PermissionGrant> Grants);

public sealed record AuthorizationDecision(
    bool Allowed,
    string PermissionCode,
    DataScope Scope,
    string StaffRef,
    long AuthVersion,
    IReadOnlySet<long> DepartmentIds);

