namespace Eggrack.Operations.Application.Modules.Wholesale;

public enum ProcurementPlanStatus { Draft, Sourcing, Costing, PendingApproval, Approved, Rejected, EmailPending, Completed }

public static class ProcurementPlanStatusParser
{
    public static ProcurementPlanStatus Parse(string value) =>
        Enum.TryParse<ProcurementPlanStatus>(value, ignoreCase: true, out var status)
            ? status
            : ProcurementPlanStatus.Draft;
}

public sealed record ProcurementPlanListItem(
    long Id, string PlanNumber, string RequestNumber, int RequestVersion, string CustomerName,
    string ProductSummary, string? BuyerName, decimal? TotalCostCny, decimal? FinalQuoteUsd,
    ProcurementPlanStatus Status, DateTime UpdatedAtUtc);

public sealed record ProcurementCostInput(
    decimal PurchaseCostCny, decimal PackagingCostCny, decimal SampleCostCny,
    decimal DomesticShippingCny, decimal InternationalShippingCny, decimal OtherCostCny,
    decimal CnyPerUsd, decimal ProfitRate);

public sealed record ProcurementCostResult(decimal TotalCostCny, decimal TotalCostUsd, decimal SuggestedQuoteUsd);

public static class ProcurementPricing
{
    public static ProcurementCostResult Calculate(ProcurementCostInput input)
    {
        if (input.CnyPerUsd <= 0) throw new ArgumentOutOfRangeException(nameof(input.CnyPerUsd));
        if (input.ProfitRate < 0.10m || input.ProfitRate > 0.20m)
            throw new ArgumentOutOfRangeException(nameof(input.ProfitRate), "利润率必须在 10%–20% 之间。");
        var totalCny = input.PurchaseCostCny + input.PackagingCostCny + input.SampleCostCny +
            input.DomesticShippingCny + input.InternationalShippingCny + input.OtherCostCny;
        if (totalCny < 0) throw new ArgumentOutOfRangeException(nameof(input), "成本不能为负数。");
        var totalUsd = decimal.Round(totalCny / input.CnyPerUsd, 2, MidpointRounding.AwayFromZero);
        var quote = decimal.Round(totalUsd / (1m - input.ProfitRate), 2, MidpointRounding.AwayFromZero);
        return new(totalCny, totalUsd, quote);
    }

    public static void ValidateFinalQuote(decimal finalQuoteUsd, decimal totalCostUsd)
    {
        if (finalQuoteUsd <= 4000m) throw new InvalidOperationException("最终报价必须大于 USD 4,000。");
        if (totalCostUsd <= 0) throw new InvalidOperationException("必须先完成成本核算。");
        var margin = (finalQuoteUsd - totalCostUsd) / finalQuoteUsd;
        if (margin < 0.10m || margin > 0.20m) throw new InvalidOperationException("最终报价的实际利润率必须在 10%–20% 之间。");
    }
}
