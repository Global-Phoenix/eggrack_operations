using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProformaInvoicePricingCalculatorTests
{
    [Fact]
    public void CalculatesStoredLineAmountWithCommercialRounding()
    {
        Assert.Equal(11.12m,ProformaInvoicePricingCalculator.CalculateLineAmount(1m,11.115m));
    }

    [Fact]
    public void CalculatesFeesDiscountAndTotal()
    {
        var result=ProformaInvoicePricingCalculator.CalculateTotals([100m,50m],10m,20m,5m,15m);
        Assert.Equal(150m,result.ProductAmount);
        Assert.Equal(185m,result.Subtotal);
        Assert.Equal(170m,result.TotalAmount);
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(1,0)]
    [InlineData(-1,1)]
    public void RejectsInvalidLinePrice(decimal quantity,decimal unitPrice)
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>ProformaInvoicePricingCalculator.CalculateLineAmount(quantity,unitPrice));
    }
}
