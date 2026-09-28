using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eggrack.Operations.Infrastructure.Modules.Security.Identity;

public sealed class InternalIdentityDesignFactory
    : IDesignTimeDbContextFactory<InternalIdentityDbContext>
{
    public InternalIdentityDbContext CreateDbContext(string[] args)
    {
        const string designOnlyConnection =
            "Server=localhost;Database=eggrack_design_only;User=unused;Password=unused";
        var options = new DbContextOptionsBuilder<InternalIdentityDbContext>()
            .UseMySql(
                designOnlyConnection,
                new MySqlServerVersion(new Version(5, 7, 32)),
                mysql => mysql.MigrationsHistoryTable("eggrack_identity_migration"))
            .Options;
        return new InternalIdentityDbContext(options);
    }
}
