using Microsoft.Extensions.DependencyInjection;

namespace Eggrack.Operations.Infrastructure.Modules.Files;

public static class FilesModuleServices
{
    public static IServiceCollection AddFileCenter(this IServiceCollection services)
    {
        services.AddScoped<FileCenterDataService>();
        services.AddHttpClient("FileCenterPublicFiles", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EggrackOperations/1.0");
        });
        services.AddScoped<FileCenterStorageService>();
        return services;
    }
}
