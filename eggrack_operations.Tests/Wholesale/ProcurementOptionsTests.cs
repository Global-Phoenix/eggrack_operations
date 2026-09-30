using Eggrack.Operations.Infrastructure.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementOptionsTests
{
    [Fact]
    public void Defaults_to_current_buyer_department()
    {
        var options = new ProcurementOptions();

        Assert.Equal(["development"], options.NormalizedBuyerDepartmentCodes());
        Assert.Equal("开发部", options.BuyerDepartmentLabel);
    }

    [Fact]
    public void Normalizes_configured_department_codes()
    {
        var options = new ProcurementOptions
        {
            BuyerDepartmentCodes = [" Development ", "PROCUREMENT", "development", ""]
        };

        Assert.Equal(["development", "procurement"], options.NormalizedBuyerDepartmentCodes());
    }
}
