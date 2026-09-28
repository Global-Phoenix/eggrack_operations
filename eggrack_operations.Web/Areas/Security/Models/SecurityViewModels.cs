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
    IReadOnlyList<DepartmentOption> Departments);

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

