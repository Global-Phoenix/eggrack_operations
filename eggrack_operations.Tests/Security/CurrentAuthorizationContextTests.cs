using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Domain.Modules.Security;

namespace Eggrack.Operations.Tests.Security;

public sealed class CurrentAuthorizationContextTests
{
    [Fact]
    public void IntersectsClassAndActionScopes()
    {
        var context = new CurrentAuthorizationContext();
        context.Set(Decision("view", DataScope.All, new HashSet<long>()));
        context.Set(Decision("update", DataScope.Department, new HashSet<long> { 8, 9 }));

        var current = context.Require();

        Assert.Equal(DataScope.Department, current.Scope);
        Assert.Equal([8, 9], current.DepartmentIds.Order());
        Assert.Equal(42, current.StaffId);
        Assert.Equal(DataScope.All, context.Require("view").Scope);
        Assert.Equal(DataScope.Department, context.Require("update").Scope);
    }

    [Fact]
    public void IntersectsDepartmentSetsForMultipleFilters()
    {
        var context = new CurrentAuthorizationContext();
        context.Set(Decision("view", DataScope.Department, new HashSet<long> { 2, 3 }));
        context.Set(Decision("approve", DataScope.SelfOrDepartment, new HashSet<long> { 3, 4 }));

        var current = context.Require();

        Assert.Equal(DataScope.SelfOrDepartment, current.Scope);
        Assert.Equal([3], current.DepartmentIds);
    }

    private static AuthorizationDecision Decision(
        string permission,
        DataScope scope,
        IReadOnlySet<long> departments) =>
        new(true, permission, scope, 42, "staff-42", 1, departments);
}
