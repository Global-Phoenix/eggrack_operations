using Eggrack.Operations.Domain.Modules.Security;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Http;

namespace Eggrack.Operations.Tests.Security;

public sealed class InternalAuthorizationHttpContextTests
{
    [Fact]
    public void ResolvesDatabaseStaffIdFromAuthorizedDecisionWithoutCustomCookieClaim()
    {
        var context = new DefaultHttpContext();
        context.Items["InternalAuthorization"] = new AuthorizationDecision(
            true,
            "wholesale.purchase-plan.create",
            DataScope.All,
            27,
            "identity-staff-reference",
            1,
            new HashSet<long>());

        Assert.True(context.TryGetInternalStaffId(out long signedStaffId));
        Assert.True(context.TryGetInternalStaffId(out ulong unsignedStaffId));
        Assert.Equal(27, signedStaffId);
        Assert.Equal(27UL, unsignedStaffId);
    }

    [Fact]
    public void RejectsMissingOrDeniedAuthorizationDecision()
    {
        var context = new DefaultHttpContext();
        Assert.False(context.TryGetInternalStaffId(out long missingStaffId));
        Assert.Equal(0, missingStaffId);

        context.Items["InternalAuthorization"] = new AuthorizationDecision(
            false,
            "wholesale.purchase-plan.create",
            DataScope.None,
            27,
            "identity-staff-reference",
            1,
            new HashSet<long>());

        Assert.False(context.TryGetInternalStaffId(out ulong deniedStaffId));
        Assert.Equal(0UL, deniedStaffId);
    }
}
