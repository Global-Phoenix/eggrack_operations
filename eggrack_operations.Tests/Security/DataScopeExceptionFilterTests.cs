using Eggrack.Operations.Common.Models;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Eggrack.Operations.Tests.Security;

public sealed class DataScopeExceptionFilterTests
{
    [Fact]
    public void ConvertsScopeDenialToForbidden()
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        var context = new ExceptionContext(actionContext, [])
        {
            Exception = new DataScopeDeniedException("denied")
        };

        new DataScopeExceptionFilter().OnException(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }
}
