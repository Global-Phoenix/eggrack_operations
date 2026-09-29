using Eggrack.Operations.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eggrack_operations.Areas.Security;

public sealed class DataScopeExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DataScopeDeniedException) return;
        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        context.ExceptionHandled = true;
    }
}
