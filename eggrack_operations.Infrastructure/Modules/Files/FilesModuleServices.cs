using Microsoft.Extensions.DependencyInjection;

namespace Eggrack.Operations.Infrastructure.Modules.Files;

public static class FilesModuleServices
{
    public static IServiceCollection AddFileCenter(this IServiceCollection services)
    {
        services.AddScoped<FileCenterDataService>();
        return services;
    }
}
