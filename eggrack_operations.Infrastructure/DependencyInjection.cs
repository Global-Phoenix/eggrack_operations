using Eggrack.Operations.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eggrack.Operations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(ValidateDatabases, "数据库配置无效：启用项必须包含连接字符串名称且超时必须大于 0")
            .ValidateOnStart();
        services.AddSingleton<DatabaseSessionFactory>();
        return services;
    }

    private static bool ValidateDatabases(DatabaseOptions options) =>
        options.SqlServer.Connections.Values.All(IsValid) &&
        options.MySql.Connections.Values.All(IsValid);

    private static bool IsValid(DatabaseConnectionOptions options) =>
        !options.Enabled ||
        (!string.IsNullOrWhiteSpace(options.ConnectionStringName) && options.CommandTimeoutSeconds > 0);
}
