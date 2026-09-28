using Eggrack.Operations.Application.Modules.Security;

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

