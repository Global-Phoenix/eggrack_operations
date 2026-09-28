using eggrack_operations.Areas.Security.Models;
using Eggrack.Operations.Infrastructure.Modules.Security;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Security.Controllers;

[Area("Security")]
[Route("security")]
public sealed class SecurityController(
    SecurityAdminService admin,
    CurrentStaffAccessor currentStaff,
    UserManager<InternalIdentityUser> users) : Controller
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

    [HttpPost("staff/create")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.staff.create")]
    public async Task<IActionResult> CreateStaff(CreateStaffAccountInput input, CancellationToken cancellationToken)
    {
        var operatorRef = currentStaff.GetStaffRef();
        if (string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "请完整填写用户名、姓名、有效邮箱和初始密码。";
            return RedirectToAction(nameof(Staff));
        }

        var user = new InternalIdentityUser
        {
            UserName = input.UserName.Trim(),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = input.StaffName.Trim(),
            StaffRef = $"pending-{Guid.NewGuid():N}",
            MustEnableTwoFactor = false,
            LockoutEnabled = true
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            TempData["Error"] = string.Join("；", created.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Staff));
        }

        user.StaffRef = user.Id.ToString();
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            await users.DeleteAsync(user);
            TempData["Error"] = "登录账号映射失败，创建操作已回滚。";
            return RedirectToAction(nameof(Staff));
        }

        try
        {
            await admin.CreateStaffAsync(user.StaffRef, user.DisplayName, user.Email!, input.RoleId, input.DepartmentId, operatorRef, cancellationToken);
            TempData["Success"] = $"人员 {user.DisplayName} 已创建，可以使用邮箱或用户名登录。";
        }
        catch (Exception exception) when (exception is InvalidOperationException or MySqlConnector.MySqlException)
        {
            await users.DeleteAsync(user);
            TempData["Error"] = $"人员创建失败，登录账号已回滚：{exception.Message}";
        }
        return RedirectToAction(nameof(Staff));
    }
}

