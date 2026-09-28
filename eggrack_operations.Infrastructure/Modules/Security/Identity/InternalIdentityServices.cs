using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eggrack.Operations.Infrastructure.Modules.Security.Identity;

public static class InternalIdentityServices
{
    public static IServiceCollection AddInternalIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString=configuration.GetConnectionString("EggrackConnection")
            ?? throw new InvalidOperationException("缺少 EggrackConnection");
        services.AddDbContext<InternalIdentityDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(5,7,32)),
                mysql => mysql.MigrationsHistoryTable("eggrack_identity_migration")));
        services.AddIdentity<InternalIdentityUser,IdentityRole<long>>(options =>
        {
            options.Password.RequiredLength=12;
            options.Password.RequireDigit=true;
            options.Password.RequireLowercase=true;
            options.Password.RequireUppercase=true;
            options.Password.RequireNonAlphanumeric=true;
            options.Lockout.MaxFailedAccessAttempts=5;
            options.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedAccount=true;
            options.User.RequireUniqueEmail=true;
        })
        .AddEntityFrameworkStores<InternalIdentityDbContext>()
        .AddDefaultTokenProviders();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name="__Host-eggrack-identity";
            options.Cookie.HttpOnly=true;
            options.Cookie.SecurePolicy=Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite=Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.ExpireTimeSpan=TimeSpan.FromHours(4);
            options.SlidingExpiration=false;
            options.LoginPath="/account/login";
            options.AccessDeniedPath="/account/denied";
        });
        return services;
    }
}

