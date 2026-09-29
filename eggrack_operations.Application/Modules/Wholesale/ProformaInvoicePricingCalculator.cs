namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record ProformaInvoiceTotals(decimal ProductAmount,decimal Subtotal,decimal TotalAmount);

public static class ProformaInvoicePricingCalculator
{
    public static decimal CalculateLineAmount(decimal quantity,decimal unitPrice)
    {
        if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));
        if(unitPrice<=0)throw new ArgumentOutOfRangeException(nameof(unitPrice));
        return Math.Round(quantity*unitPrice,2,MidpointRounding.AwayFromZero);
    }

    public static ProformaInvoiceTotals CalculateTotals(IEnumerable<decimal> lineAmounts,decimal packagingFee,decimal shippingFee,decimal otherFee,decimal discountAmount)
    {
        ArgumentNullException.ThrowIfNull(lineAmounts);
        if(packagingFee<0||shippingFee<0||otherFee<0||discountAmount<0)throw new ArgumentOutOfRangeException(nameof(packagingFee));
        var productAmount=lineAmounts.Sum();
        if(productAmount<0)throw new ArgumentOutOfRangeException(nameof(lineAmounts));
        var subtotal=productAmount+packagingFee+shippingFee+otherFee;
        if(discountAmount>subtotal)throw new ArgumentOutOfRangeException(nameof(discountAmount));
        return new(productAmount,subtotal,subtotal-discountAmount);
    }
}
