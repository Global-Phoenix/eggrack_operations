using eggrack_operations.Areas.Security.Models;
using Eggrack.Operations.Infrastructure.Modules.Security;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;
using Eggrack.Operations.Application.Modules.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Security.Controllers;

[Area("Security")]
[Route("security")]
public sealed class SecurityController(
    SecurityAdminService admin,
    CurrentStaffAccessor currentStaff,
    PermissionEvaluator permissionEvaluator,
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
        var data = await admin.GetStaffAdministrationAsync(keyword, cancellationToken);
        var authorization=await currentStaff.LoadAsync(cancellationToken);
        bool Has(string permission)=>authorization is not null&&permissionEvaluator.Evaluate(authorization,permission).Allowed;
        return View(new StaffIndexViewModel(
            keyword,
            data.Staff,
            data.Roles,
            data.Departments,
            data.Assignments,
            Has("auth.staff.create"),Has("auth.staff.update"),Has("auth.role.assign"),
            Has("auth.role.revoke"),Has("auth.staff.disable")));
    }

    [HttpGet("staff/{staffId:long}")]
    [InternalPermission("auth.staff.read")]
    public async Task<IActionResult> StaffDetails(long staffId,CancellationToken cancellationToken)
    {
        var data=await admin.GetStaffAdministrationAsync(null,cancellationToken);
        var staff=data.Staff.SingleOrDefault(item=>item.Id==staffId);
        if(staff is null)return NotFound();
        var identity=await users.Users.SingleOrDefaultAsync(item=>item.StaffRef==staff.StaffRef,cancellationToken);
        var authorization=await currentStaff.LoadAsync(cancellationToken);
        var canUpdate=authorization is not null&&permissionEvaluator.Evaluate(authorization,"auth.staff.update").Allowed;
        ViewData["Title"]=$"{staff.StaffName} 详情";
        return View(new StaffDetailsViewModel(
            staff,identity?.UserName??staff.StaffRef,data.Roles,data.Departments,
            data.Assignments.Where(item=>item.StaffId==staffId).ToArray(),canUpdate));
    }

    [HttpPost("staff/update")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.staff.update")]
    public async Task<IActionResult> UpdateStaff(UpdateStaffInput input,CancellationToken cancellationToken)
    {
        if(!ModelState.IsValid)
        {
            TempData["Error"]="请填写有效的用户名、姓名和邮箱。";
            return RedirectToAction(nameof(StaffDetails),new{staffId=input.StaffId});
        }
        var operatorRef=currentStaff.GetStaffRef();
        if(string.IsNullOrWhiteSpace(operatorRef))return Challenge();
        try{await admin.EnsureStaffRefMatchesAsync(input.StaffId,input.StaffRef,cancellationToken);}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;return RedirectToAction(nameof(Staff));}
        var user=await users.Users.SingleOrDefaultAsync(item=>item.StaffRef==input.StaffRef,cancellationToken);
        if(user is null)
        {
            TempData["Error"]="未找到对应登录账号。";
            return RedirectToAction(nameof(StaffDetails),new{staffId=input.StaffId});
        }
        var previous=(UserName:user.UserName,Email:user.Email,DisplayName:user.DisplayName);
        user.UserName=input.UserName.Trim();
        user.Email=input.Email.Trim();
        user.DisplayName=input.StaffName.Trim();
        user.NormalizedUserName=users.NormalizeName(user.UserName);
        user.NormalizedEmail=users.NormalizeEmail(user.Email);
        var identityResult=await users.UpdateAsync(user);
        if(!identityResult.Succeeded)
        {
            TempData["Error"]=string.Join("；",identityResult.Errors.Select(item=>item.Description));
            return RedirectToAction(nameof(StaffDetails),new{staffId=input.StaffId});
        }
        try
        {
            await admin.UpdateStaffAsync(input.StaffId,input.StaffName,input.Email,input.PrimaryDepartmentId,operatorRef,cancellationToken);
            TempData["Success"]="人员资料和主部门已更新。";
        }
        catch(Exception error) when(error is InvalidOperationException or MySqlConnector.MySqlException)
        {
            user.UserName=previous.UserName;user.Email=previous.Email;user.DisplayName=previous.DisplayName;
            user.NormalizedUserName=users.NormalizeName(user.UserName);user.NormalizedEmail=users.NormalizeEmail(user.Email);
            await users.UpdateAsync(user);
            TempData["Error"]=$"人员资料更新失败，登录资料已回滚：{error.Message}";
        }
        return RedirectToAction(nameof(StaffDetails),new{staffId=input.StaffId});
    }

    [HttpGet("roles")]
    [InternalPermission("auth.role.read")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var roles=admin.GetRolesAsync(cancellationToken);var permissions=admin.GetPermissionsAsync(cancellationToken);var grants=admin.GetRolePermissionGrantsAsync(cancellationToken);
        var authorizationTask=currentStaff.LoadAsync(cancellationToken);
        await Task.WhenAll(roles,permissions,grants,authorizationTask);
        var authorization=await authorizationTask;
        var canManage=authorization is not null&&permissionEvaluator.Evaluate(authorization,"auth.permission.manage").Allowed;
        return View(new RoleIndexViewModel(await roles,await permissions,await grants,canManage));
    }

    [HttpGet("departments")]
    [InternalPermission("auth.department.read")]
    public async Task<IActionResult> Departments(CancellationToken cancellationToken)
    {
        var departmentsTask=admin.GetDepartmentListAsync(cancellationToken);
        var authorizationTask=currentStaff.LoadAsync(cancellationToken);
        await Task.WhenAll(departmentsTask,authorizationTask);
        var authorization=await authorizationTask;
        return View(new DepartmentIndexViewModel(await departmentsTask,
            authorization is not null&&permissionEvaluator.Evaluate(authorization,"auth.department.manage").Allowed));
    }

    [HttpGet("audit")]
    [InternalPermission("auth.audit.read")]
    public async Task<IActionResult> Audit(
        string? operatorRef,
        string? actionCode,
        string? targetType,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken) =>
        View(new PermissionAuditViewModel(
            operatorRef,actionCode,targetType,from,to,
            await admin.GetAuditLogsAsync(operatorRef,actionCode,targetType,from,to,cancellationToken)));

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

    [HttpPost("staff/status")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.staff.disable")]
    public async Task<IActionResult> SetStaffStatus(SetStaffStatusInput input, CancellationToken cancellationToken)
    {
        var operatorRef = currentStaff.GetStaffRef();
        if (string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        if (operatorRef == input.StaffRef)
        {
            TempData["Error"] = "不能停用或启用当前登录账号。";
            return RedirectToAction(nameof(Staff));
        }

        try{await admin.EnsureStaffRefMatchesAsync(input.StaffId,input.StaffRef,cancellationToken);}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;return RedirectToAction(nameof(Staff));}

        var user = await users.Users.SingleOrDefaultAsync(x => x.StaffRef == input.StaffRef, cancellationToken);
        if (user is null)
        {
            TempData["Error"] = "未找到对应登录账号。";
            return RedirectToAction(nameof(Staff));
        }

        var previousLockout = user.LockoutEnd;
        var identityResult = await users.SetLockoutEndDateAsync(
            user, input.Enabled ? null : DateTimeOffset.MaxValue);
        if (!identityResult.Succeeded)
        {
            TempData["Error"] = string.Join("；", identityResult.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Staff));
        }
        await users.UpdateSecurityStampAsync(user);

        try
        {
            await admin.SetStaffEnabledAsync(input.StaffId, input.Enabled, operatorRef, cancellationToken);
            TempData["Success"] = input.Enabled ? "人员账号已启用。" : "人员账号已停用，现有会话已失效。";
        }
        catch (Exception exception)
        {
            await users.SetLockoutEndDateAsync(user, previousLockout);
            TempData["Error"] = $"人员状态更新失败，登录状态已回滚：{exception.Message}";
        }
        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("staff/reset-password")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.staff.update")]
    public async Task<IActionResult> ResetStaffPassword(ResetStaffPasswordInput input, CancellationToken cancellationToken)
    {
        var operatorRef = currentStaff.GetStaffRef();
        if (string.IsNullOrWhiteSpace(operatorRef)) return Challenge();

        var user = await users.Users.SingleOrDefaultAsync(x => x.StaffRef == input.StaffRef, cancellationToken);
        if (user is null)
        {
            TempData["Error"] = "未找到对应登录账号。";
            return RedirectToAction(nameof(Staff));
        }
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, input.NewPassword);
        if (result.Succeeded)
        {
            await users.UpdateSecurityStampAsync(user);
            await admin.RecordPasswordResetAsync(
                input.StaffRef,
                operatorRef,
                HttpContext.TraceIdentifier,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
            TempData["Success"] = $"{user.DisplayName} 的密码已重置，旧会话已失效且操作已记录审计。";
        }
        else
        {
            TempData["Error"] = string.Join("；", result.Errors.Select(x => x.Description));
        }
        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("staff/revoke-role")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.role.revoke")]
    public async Task<IActionResult> RevokeRole(RevokeRoleInput input, CancellationToken cancellationToken)
    {
        var operatorRef = currentStaff.GetStaffRef();
        if (string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        try
        {
            await admin.RevokeRoleAsync(input.AssignmentId, operatorRef, cancellationToken);
            TempData["Success"] = "角色授权已撤销，人员权限缓存版本已更新。";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("departments/save")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.department.manage")]
    public async Task<IActionResult> SaveDepartment(SaveDepartmentInput input, CancellationToken cancellationToken)
    {
        var operatorRef=currentStaff.GetStaffRef(); if(string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        if(!ModelState.IsValid){TempData["Error"]="请填写部门编码和名称。";return RedirectToAction(nameof(Departments));}
        try{await admin.SaveDepartmentAsync(input.Id,input.Code.Trim(),input.Name.Trim(),input.ParentId,input.SortOrder,operatorRef,cancellationToken);TempData["Success"]="部门信息已保存。";}
        catch(Exception exception) when(exception is InvalidOperationException or MySqlConnector.MySqlException){TempData["Error"]=$"部门保存失败：{exception.Message}";}
        return RedirectToAction(nameof(Departments));
    }

    [HttpPost("departments/status")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.department.manage")]
    public async Task<IActionResult> SetDepartmentStatus(SetDepartmentStatusInput input, CancellationToken cancellationToken)
    {
        var operatorRef=currentStaff.GetStaffRef(); if(string.IsNullOrWhiteSpace(operatorRef)) return Challenge();
        try{await admin.SetDepartmentEnabledAsync(input.Id,input.Enabled,operatorRef,cancellationToken);TempData["Success"]=input.Enabled?"部门已启用。":"部门已停用。";}
        catch(InvalidOperationException exception){TempData["Error"]=exception.Message;}
        return RedirectToAction(nameof(Departments));
    }

    [HttpPost("roles/permissions")]
    [ValidateAntiForgeryToken]
    [InternalPermission("auth.permission.manage")]
    public async Task<IActionResult> SaveRolePermissions(SaveRolePermissionsInput input, CancellationToken cancellationToken)
    {
        var operatorRef=currentStaff.GetStaffRef();if(string.IsNullOrWhiteSpace(operatorRef))return Challenge();
        try{await admin.SaveRolePermissionsAsync(input.RoleId,input.PermissionIds,operatorRef,cancellationToken);TempData["Success"]="角色权限已保存，相关人员权限版本已更新。";}
        catch(InvalidOperationException exception){TempData["Error"]=exception.Message;}
        return RedirectToAction(nameof(Roles));
    }
}

