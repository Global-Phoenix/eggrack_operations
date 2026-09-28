using Eggrack.Operations.Infrastructure.Modules.Security;

namespace eggrack_operations.Areas.Security;

public static class SecurityWebServices
{
    public static IServiceCollection AddSecurityArea(this IServiceCollection services)
    {
        services.AddInternalPermissions();
        services.AddScoped<CurrentStaffAccessor>();
        return services;
    }
}

