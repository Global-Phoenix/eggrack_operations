using System.Reflection;
using eggrack_operations.Areas.Security;
using eggrack_operations.Areas.Security.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Security;

public sealed class SecurityAuthorizationTests
{
    [Theory]
    [InlineData(nameof(SecurityController.Index),"auth.staff.read")]
    [InlineData(nameof(SecurityController.Staff),"auth.staff.read")]
    [InlineData(nameof(SecurityController.StaffDetails),"auth.staff.read")]
    [InlineData(nameof(SecurityController.Roles),"auth.role.read")]
    [InlineData(nameof(SecurityController.Departments),"auth.department.read")]
    [InlineData(nameof(SecurityController.Audit),"auth.audit.read")]
    public void ReadActionsRequireExpectedPermission(string methodName,string permission)
    {
        var method=Method(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpGetAttribute>());
        AssertPermission(method,permission);
    }

    [Theory]
    [InlineData(nameof(SecurityController.UpdateStaff),"auth.staff.update")]
    [InlineData(nameof(SecurityController.AssignRole),"auth.role.assign")]
    [InlineData(nameof(SecurityController.CreateStaff),"auth.staff.create")]
    [InlineData(nameof(SecurityController.SetStaffStatus),"auth.staff.disable")]
    [InlineData(nameof(SecurityController.ResetStaffPassword),"auth.staff.update")]
    [InlineData(nameof(SecurityController.RevokeRole),"auth.role.revoke")]
    [InlineData(nameof(SecurityController.SaveDepartment),"auth.department.manage")]
    [InlineData(nameof(SecurityController.SetDepartmentStatus),"auth.department.manage")]
    [InlineData(nameof(SecurityController.SaveRolePermissions),"auth.permission.manage")]
    public void WriteActionsRequirePostAntiforgeryAndExpectedPermission(string methodName,string permission)
    {
        var method=Method(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>());
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
        AssertPermission(method,permission);
    }

    private static MethodInfo Method(string name)=>typeof(SecurityController).GetMethod(name)
        ??throw new InvalidOperationException(name);

    private static void AssertPermission(MemberInfo method,string expected)
    {
        var attribute=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.Equal(expected,Assert.Single(attribute.Arguments!));
    }
}
