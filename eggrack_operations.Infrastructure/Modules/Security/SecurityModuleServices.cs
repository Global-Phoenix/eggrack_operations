using Eggrack.Operations.Application.Modules.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Eggrack.Operations.Infrastructure.Modules.Security;

public static class SecurityModuleServices
{
    public static IServiceCollection AddInternalPermissions(this IServiceCollection services)
    {
        services.AddSingleton<PermissionEvaluator>();
        services.AddScoped<StaffAuthorizationQuery>();
        services.AddScoped<SecurityAdminService>();
        return services;
    }
}

