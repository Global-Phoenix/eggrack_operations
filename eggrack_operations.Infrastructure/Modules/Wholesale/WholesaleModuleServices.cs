using Microsoft.Extensions.DependencyInjection;
namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;
public static class WholesaleModuleServices
{
    public static IServiceCollection AddWholesaleProcurement(this IServiceCollection services)
    {
        services.AddScoped<ProcurementDataService>();
        return services;
    }
}
