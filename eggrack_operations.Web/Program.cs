using Eggrack.Operations.Application.Navigation;
using Eggrack.Operations.Infrastructure;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Modules.Files;
using eggrack_operations.Areas.Security;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add<DataScopeExceptionFilter>());
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<NavigationService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWholesaleProcurement(builder.Configuration);
builder.Services.AddFileCenter();
builder.Services.AddSecurityArea();
builder.Services.AddInternalIdentity(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "eggrack-identity-dev";
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });
}
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/status/{0}");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;


