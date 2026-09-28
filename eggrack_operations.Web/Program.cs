using Eggrack.Operations.Application.Navigation;
using Eggrack.Operations.Infrastructure;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;
using eggrack_operations.Areas.Security;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<NavigationService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWholesaleProcurement();
builder.Services.AddSecurityArea();
builder.Services.AddInternalIdentity(builder.Configuration);
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


