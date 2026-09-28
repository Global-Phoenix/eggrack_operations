using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Domain.Modules.Security;

namespace Eggrack.Operations.Tests.Security;

public sealed class PermissionEvaluatorTests
{
    private readonly PermissionEvaluator _sut = new();

    [Fact]
    public void Evaluate_DenyOverridesAllow()
    {
        var authorization = Staff(
            new("order.read", "ALLOW", DataScope.All, null),
            new("order.read", "DENY", DataScope.Self, null));

        var result = _sut.Evaluate(authorization, "order.read");

        Assert.False(result.Allowed);
        Assert.Equal(DataScope.None, result.Scope);
    }

    [Fact]
    public void Evaluate_MergesStrongestScopeAndDepartments()
    {
        var authorization = Staff(
            new("order.read", "ALLOW", DataScope.Self, null),
            new("order.read", "ALLOW", DataScope.Department, 3),
            new("order.read", "ALLOW", DataScope.Department, 8));

        var result = _sut.Evaluate(authorization, "order.read");

        Assert.True(result.Allowed);
        Assert.Equal(DataScope.Department, result.Scope);
        Assert.Equal(new long[] { 3, 8 }, result.DepartmentIds.Order());
    }

    [Fact]
    public void Evaluate_RejectsMissingPermission()
    {
        var result = _sut.Evaluate(Staff(), "order.approve");

        Assert.False(result.Allowed);
    }

    private static StaffAuthorization Staff(params PermissionGrant[] grants) =>
        new(1, "staff-1", 7, grants);
}

