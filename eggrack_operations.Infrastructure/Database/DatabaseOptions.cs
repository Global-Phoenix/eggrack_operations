namespace Eggrack.Operations.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public DatabaseProviderOptions SqlServer { get; set; } = new();
    public DatabaseProviderOptions MySql { get; set; } = new();
}

public sealed class DatabaseProviderOptions
{
    public Dictionary<string, DatabaseConnectionOptions> Connections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DatabaseConnectionOptions
{
    public bool Enabled { get; set; }
    public string ConnectionStringName { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public enum DatabaseProvider
{
    SqlServer,
    MySql
}
