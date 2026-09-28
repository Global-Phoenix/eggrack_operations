using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Eggrack.Operations.Infrastructure.Database;

public sealed class DatabaseSessionFactory(
    IOptions<DatabaseOptions> databaseOptions,
    IConfiguration configuration)
{
    public Task<DatabaseSession> OpenSqlServerAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        OpenAsync(DatabaseProvider.SqlServer, name, cancellationToken);

    public Task<DatabaseSession> OpenMySqlAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        OpenAsync(DatabaseProvider.MySql, name, cancellationToken);

    public async Task<DatabaseSession> OpenAsync(
        DatabaseProvider provider,
        string name,
        CancellationToken cancellationToken = default)
    {
        var connections = provider == DatabaseProvider.SqlServer
            ? databaseOptions.Value.SqlServer.Connections
            : databaseOptions.Value.MySql.Connections;

        if (!connections.TryGetValue(name, out var options))
            throw new KeyNotFoundException($"未配置数据库: {provider}/{name}");
        if (!options.Enabled)
            throw new InvalidOperationException($"数据库尚未启用: {provider}/{name}");

        var connectionString = configuration.GetConnectionString(options.ConnectionStringName)
            ?? throw new InvalidOperationException($"缺少连接字符串: {options.ConnectionStringName}");
        DbConnection connection = provider == DatabaseProvider.SqlServer
            ? new SqlConnection(connectionString)
            : new MySqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return new DatabaseSession(connection, options.CommandTimeoutSeconds);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
