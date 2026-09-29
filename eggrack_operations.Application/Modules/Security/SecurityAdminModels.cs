namespace Eggrack.Operations.Application.Modules.Security;

public sealed record SecurityDashboard(int StaffCount, int EnabledStaffCount, int DepartmentCount, int RoleCount);
public sealed record StaffListItem(long Id, string StaffRef, string StaffName, string? Email, bool IsEnabled, string Departments, string Roles);
public sealed record RoleListItem(long Id, string Code, string Name, int Level, string DefaultScope, bool IsEnabled, int StaffCount, int PermissionCount);
public sealed record DepartmentOption(long Id, string Name);
public sealed record StaffRoleAssignment(long Id, long StaffId, string RoleCode, string RoleName, string? DepartmentName);
public sealed record DepartmentListItem(long Id, long? ParentId, string Code, string Name, string? ParentName, int SortOrder, bool IsEnabled, int StaffCount, int RoleCount);
public sealed record PermissionListItem(long Id, string Code, string Name, string ResourceType, bool IsEnabled);
public sealed record RolePermissionGrant(long RoleId, long PermissionId);
public sealed record PermissionAuditItem(long Id, string OperatorRef, string ActionCode, string TargetType, string? TargetRef, string? BeforeData, string? AfterData, string? RequestId, string? IpAddress, DateTime CreatedAt);

