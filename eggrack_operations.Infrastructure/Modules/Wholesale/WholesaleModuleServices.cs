using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;
public static class WholesaleModuleServices
{
    public static IServiceCollection AddWholesaleProcurement(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddOptions<ProcurementOptions>()
            .Bind(configuration.GetSection(ProcurementOptions.SectionName))
            .Validate(options=>options.NormalizedBuyerDepartmentCodes().Length>0,"至少配置一个采购人员所属部门编码。")
            .Validate(options=>!string.IsNullOrWhiteSpace(options.BuyerDepartmentLabel),"采购人员所属部门名称不能为空。")
            .ValidateOnStart();
        services.AddScoped<ProcurementDataService>();
        services.AddScoped<ProcurementScopePolicy>();
        services.AddSingleton<LegacySmtpSender>();
        services.AddScoped<ProcurementMailDispatcher>();
        return services;
    }
}
