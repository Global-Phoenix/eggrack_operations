namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed class ProcurementOptions
{
    public const string SectionName = "Procurement";

    public string[] BuyerDepartmentCodes { get; set; } = ["development"];

    public string BuyerDepartmentLabel { get; set; } = "开发部";

    public string[] NormalizedBuyerDepartmentCodes() => BuyerDepartmentCodes
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Select(code => code.Trim().ToLowerInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
