using Eggrack.Operations.Application.Modules.Security;
using System.ComponentModel.DataAnnotations;

namespace eggrack_operations.Areas.Security.Models;

public sealed record SecurityOverviewViewModel(
    SecurityDashboard Dashboard,
    IReadOnlyList<StaffListItem> RecentStaff,
    IReadOnlyList<RoleListItem> Roles);

public sealed record StaffIndexViewModel(
    string? Keyword,
    IReadOnlyList<StaffListItem> Staff,
    IReadOnlyList<RoleListItem> Roles,
    IReadOnlyList<DepartmentOption> Departments,
    IReadOnlyList<StaffRoleAssignment> Assignments);

public sealed class AssignRoleInput
{
    public long StaffId { get; set; }
    public long RoleId { get; set; }
    public long? DepartmentId { get; set; }
}

public sealed class CreateStaffAccountInput
{
    [Required, StringLength(128)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string StaffName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(190)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public long RoleId { get; set; }
    public long? DepartmentId { get; set; }
}

public sealed class SetStaffStatusInput
{
    public long StaffId { get; set; }
    [Required] public string StaffRef { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class ResetStaffPasswordInput
{
    [Required] public string StaffRef { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
}

public sealed class RevokeRoleInput
{
    public long AssignmentId { get; set; }
}

public sealed record DepartmentIndexViewModel(IReadOnlyList<DepartmentListItem> Departments);

public sealed class SaveDepartmentInput
{
    public long? Id { get; set; }
    [Required, StringLength(64)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(128)] public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class SetDepartmentStatusInput
{
    public long Id { get; set; }
    public bool Enabled { get; set; }
}
public sealed record DepartmentFormViewModel(string Code,string Name,long? ParentId,int SortOrder,IReadOnlyList<DepartmentListItem> Departments,long? CurrentId);

public sealed record RoleIndexViewModel(
    IReadOnlyList<RoleListItem> Roles,
    IReadOnlyList<PermissionListItem> Permissions,
    IReadOnlyList<RolePermissionGrant> Grants);

public sealed class SaveRolePermissionsInput
{
    public long RoleId { get; set; }
    public long[] PermissionIds { get; set; } = [];
}

public sealed record PermissionAuditViewModel(
    string? OperatorRef,
    string? ActionCode,
    string? TargetType,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<PermissionAuditItem> Items);

