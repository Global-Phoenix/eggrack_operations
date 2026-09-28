using eggrack_operations.Areas.Security.Models;
using Eggrack.Operations.Infrastructure.Modules.Security;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Security.Controllers;

[Area("Security")]
[Route("security")]
public sealed class SecurityController(
    SecurityAdminService admin,
    CurrentStaffAccessor currentStaff) : Controller
{
    [HttpGet("")]
    [InternalPermission("auth.staff.read")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboardTask = admin.GetDashboardAsync(cancellationToken);
        var staffTask = admin.GetStaffAsync(null, cancellationToken);
        var rolesTask = admin.GetRolesAsync(cancellationToken);
        await Task.WhenAll(dashboardTask, staffTask, rolesTask);
        return View(new SecurityOverviewViewModel(
            await dashboardTask,
            (await staffTask).Take(6).ToArray(),
            await rolesTask));
    }

    [HttpGet("staff")]
    [InternalPermission("auth.staff.read")]
    public async Task<IActionResult> Staff(string? keyword, CancellationToken cancellationToken)
    {
        var staffTask = admin.GetStaffAsync(keyword, cancellationToken);
        var rolesTask = admin.GetRolesAsync(cancellationToken);
        var departmentsTask = admin.GetDepartmentsAsync(cancellationToken);
        await Task.WhenAll(staffTask, rolesTask, departmentsTask);
        return View(new StaffIndexViewModel(
            keyword,
            await staffTask,
            await rolesTask,
            await departmentsTask));
    }

    [HttpGet("roles")]
    [InternalPermission("auth.role.read")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken) =>
        View(await admin.GetRolesAsync(cancellationToken));

    [HttpPost("staff/assign-role")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.role.assign")]
    public async Task<IActionResult> AssignRole(AssignRoleInput input, CancellationToken cancellationToken)
    {
        var operatorRef = currentStaff.GetStaffRef();
        if (string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        try
        {
            await admin.AssignRoleAsync(input.StaffId, input.RoleId, input.DepartmentId, operatorRef, cancellationToken);
            TempData["Success"] = "角色授权已保存，人员权限缓存版本已更新。";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Staff));
    }
}

