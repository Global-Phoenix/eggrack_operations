namespace Eggrack.Operations.Application.Modules.Security;

public sealed class SecurityDashboard
{
    public int StaffCount { get; set; }
    public int EnabledStaffCount { get; set; }
    public int DepartmentCount { get; set; }
    public int RoleCount { get; set; }
}
public sealed class StaffListItem
{
    public long Id { get; set; }
    public string StaffRef { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsEnabled { get; set; }
    public string Departments { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
}
public sealed class RoleListItem
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string DefaultScope { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int StaffCount { get; set; }
    public int PermissionCount { get; set; }
}
public sealed class DepartmentOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
public sealed class StaffRoleAssignment
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
}
public sealed class DepartmentListItem
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentName { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    public int StaffCount { get; set; }
    public int RoleCount { get; set; }
}
public sealed class PermissionListItem
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
public sealed class RolePermissionGrant
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}
public sealed class PermissionAuditItem
{
    public long Id { get; set; }
    public string OperatorRef { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetRef { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

