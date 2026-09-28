using System.Security.Claims;
using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Domain.Modules.Security;
using Eggrack.Operations.Infrastructure.Modules.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eggrack_operations.Areas.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class InternalPermissionAttribute : TypeFilterAttribute
{
    public InternalPermissionAttribute(string permissionCode)
        : base(typeof(InternalPermissionFilter)) =>
        Arguments = [permissionCode];
}

public sealed class CurrentStaffAccessor(
    IHttpContextAccessor httpContextAccessor,
    StaffAuthorizationQuery authorizationQuery)
{
    public string? GetStaffRef()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public Task<StaffAuthorization?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var staffRef = GetStaffRef();
        return string.IsNullOrWhiteSpace(staffRef)
            ? Task.FromResult<StaffAuthorization?>(null)
            : authorizationQuery.LoadAsync(staffRef, cancellationToken);
    }
}

public sealed class InternalPermissionFilter(
    CurrentStaffAccessor currentStaff,
    PermissionEvaluator evaluator,
    string permissionCode) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var authorization = await currentStaff.LoadAsync(context.HttpContext.RequestAborted);
        if (authorization is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        var decision = evaluator.Evaluate(authorization, permissionCode);
        if (!decision.Allowed)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        context.HttpContext.Items["InternalAuthorization"] = decision;
    }
}


