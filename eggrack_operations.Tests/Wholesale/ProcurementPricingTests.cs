using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementPricingTests
{
    [Fact]
    public void Calculate_UsesAllCostsAndGrossMargin()
    {
        var result = ProcurementPricing.Calculate(new(22320m,1800m,480m,950m,6200m,300m,7.12m,0.15m));
        Assert.Equal(32050m, result.TotalCostCny);
        Assert.Equal(4501.40m, result.TotalCostUsd);
        Assert.Equal(5295.76m, result.SuggestedQuoteUsd);
    }

    [Theory]
    [InlineData(0.09)]
    [InlineData(0.21)]
    public void Calculate_RejectsProfitOutsideRange(decimal rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcurementPricing.Calculate(new(1,0,0,0,0,0,7,rate)));

    [Fact]
    public void ValidateFinalQuote_RejectsMinimumAmount() =>
        Assert.Throws<InvalidOperationException>(() => ProcurementPricing.ValidateFinalQuote(4000m,3400m));
    [Theory]
    [InlineData("Draft", ProcurementPlanStatus.Draft)]
    [InlineData("PendingApproval", ProcurementPlanStatus.PendingApproval)]
    [InlineData("Completed", ProcurementPlanStatus.Completed)]
    [InlineData("unknown", ProcurementPlanStatus.Draft)]
    public void ParseStorageStatus_MapsKnownValuesAndFallsBackToDraft(string value, ProcurementPlanStatus expected)
    {
        Assert.Equal(expected, ProcurementPlanStatusParser.Parse(value));
    }

}
