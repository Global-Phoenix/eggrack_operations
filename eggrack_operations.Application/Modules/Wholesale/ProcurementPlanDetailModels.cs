namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed class PurchasePlanDetail
{
    public uint Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanTitle { get; set; } = string.Empty;
    public uint RequestId { get; set; }
    public uint RequestVersionId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public uint RequestVersion { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string Priority { get; set; } = string.Empty;
    public ulong? AssignedBuyerId { get; set; }
    public string? AssignedBuyerName { get; set; }
    public string? InternalNote { get; set; }
    public decimal ProductCostCny { get; set; }
    public decimal PackagingCostCny { get; set; }
    public decimal SampleCostCny { get; set; }
    public decimal DomesticShippingCny { get; set; }
    public decimal InternationalShippingCny { get; set; }
    public decimal OtherCostCny { get; set; }
    public decimal TotalCostCny { get; set; }
    public decimal CnyPerUsd { get; set; }
    public decimal TotalCostUsd { get; set; }
    public byte ProfitMethod { get; set; }
    public decimal ProfitRate { get; set; }
    public decimal ApprovedQuoteUsd { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int InquiryCount { get; set; }
    public int SupplierCount { get; set; }
    public int QuotedProductCount { get; set; }
    public int SelectedQuoteCount { get; set; }
    public int FileCount { get; set; }
    public int EventCount { get; set; }
    public byte? LatestInvoiceStatus { get; set; }
    public ProcurementCostSnapshotSummary? LatestCostSnapshot { get; set; }
    public IReadOnlyList<ProcurementQuoteReviewItem> QuoteReviews { get; set; } = [];
    public IReadOnlyList<MailTaskItem> MailTasks { get; set; } = [];
    public IReadOnlyList<PurchasePlanItemDetail> Items { get; set; } = [];
    public IReadOnlyList<PurchasePlanFileDetail> Files { get; set; } = [];
    public IReadOnlyList<ProcurementWorkflowEventItem> Events { get; set; } = [];
    public ProformaInvoiceDetail? Invoice { get; set; }
}

public sealed record PurchasePlanSourcingData(
    IReadOnlyList<SupplierListItem> Suppliers,
    IReadOnlyList<CandidateProductItem> Candidates,
    IReadOnlyList<InquiryItem> Inquiries,
    IReadOnlyList<SampleItem> Samples,
    IReadOnlyList<uint> BoundSupplierIds);

public sealed record PurchasePlanFilesData(
    IReadOnlyList<PurchasePlanFileDetail> Files,
    IReadOnlyList<SupplierListItem> Suppliers,
    IReadOnlyList<InquiryItem> Inquiries,
    IReadOnlyList<SampleItem> Samples);

public sealed class PurchasePlanItemDetail
{
    public uint Id { get; set; }
    public uint? RequestItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Brand { get; set; }
    public string? Specifications { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? PackagingRequirements { get; set; }
    public string? CustomizationRequirements { get; set; }
    public string? CustomerNote { get; set; }
    public ulong? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public decimal? PurchaseUnitPriceCny { get; set; }
    public string? InternalNote { get; set; }
    public int QuoteCount { get; set; }
    public int FileCount { get; set; }
    public string? SelectedSupplierName { get; set; }
    public string? SelectedSupplierProductName { get; set; }
    public string? SelectedSupplierCurrency { get; set; }
    public decimal? SelectedSupplierUnitPrice { get; set; }
    public decimal? LineCostCny => PurchaseUnitPriceCny.HasValue
        ? decimal.Round(Quantity*PurchaseUnitPriceCny.Value,2,MidpointRounding.AwayFromZero)
        : null;
}

public sealed class PurchasePlanFileDetail
{
    public uint Id { get; set; }
    public uint? PlanItemId { get; set; }
    public string? ProductName { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public uint FileSize { get; set; }
    public bool IsCustomerVisible { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public sealed class ProformaInvoiceDetail
{
    public uint Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public uint PurchasePlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public uint RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public uint? SupersedesPiId { get; set; }
    public byte Status { get; set; }
    public string? CompanyName { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? BillingAddress { get; set; }
    public string? DeliveryAddress { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string? SellerAddress { get; set; }
    public string? SellerEmail { get; set; }
    public string? SellerPhone { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ProductAmount { get; set; }
    public decimal PackagingFee { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal OtherFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? PaymentInstructions { get; set; }
    public string? TradeTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? LeadTime { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public IReadOnlyList<ProformaInvoiceItemDetail> Items { get; set; } = [];
}

public sealed class ProformaInvoiceItemDetail
{
    public uint Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
}

public sealed class SavePurchasePlanItemProcurementCommand
{
    public uint ItemId { get; set; }
    public ulong BuyerId { get; set; }
    public decimal PurchaseUnitPriceCny { get; set; }
    public string? InternalNote { get; set; }
}

public sealed class SavePurchasePlanCostQuoteCommand
{
    public decimal PackagingCostCny { get; set; }
    public decimal SampleCostCny { get; set; }
    public decimal DomesticShippingCny { get; set; }
    public decimal InternationalShippingCny { get; set; }
    public decimal OtherCostCny { get; set; }
    public decimal CnyPerUsd { get; set; }
    public byte ProfitMethod { get; set; } = 2;
    public decimal ProfitRate { get; set; } = .15m;
}

public sealed record PurchasePlanQuoteCalculation(
    decimal ProductCostCny,decimal TotalCostCny,decimal TotalCostUsd,
    decimal QuoteUsd,decimal ActualProfitRate,bool ProfitRateInRange,bool QuoteAboveMinimum);

public static class PurchasePlanQuoteCalculator
{
    public static PurchasePlanQuoteCalculation Calculate(
        decimal productCostCny,SavePurchasePlanCostQuoteCommand input)
    {
        if(productCostCny<0||input.PackagingCostCny<0||input.SampleCostCny<0||
           input.DomesticShippingCny<0||input.InternationalShippingCny<0||input.OtherCostCny<0)
            throw new ArgumentOutOfRangeException(nameof(input),"成本不能为负数。");
        if(input.CnyPerUsd<=0)throw new ArgumentOutOfRangeException(nameof(input.CnyPerUsd),"汇率必须大于零。");
        if(input.ProfitMethod is not(1 or 2))throw new ArgumentOutOfRangeException(nameof(input.ProfitMethod),"利润方式无效。");
        if(input.ProfitRate<0||input.ProfitRate>=1)throw new ArgumentOutOfRangeException(nameof(input.ProfitRate),"利润率必须大于等于 0 且小于 100%。");
        var totalCny=productCostCny+input.PackagingCostCny+input.SampleCostCny+
            input.DomesticShippingCny+input.InternationalShippingCny+input.OtherCostCny;
        var totalUsd=decimal.Round(totalCny/input.CnyPerUsd,2,MidpointRounding.AwayFromZero);
        var quote=input.ProfitMethod==1
            ? decimal.Round(totalUsd*(1+input.ProfitRate),2,MidpointRounding.AwayFromZero)
            : decimal.Round(totalUsd/(1-input.ProfitRate),2,MidpointRounding.AwayFromZero);
        var actual=quote<=0?0:decimal.Round((quote-totalUsd)/quote,4,MidpointRounding.AwayFromZero);
        return new(productCostCny,totalCny,totalUsd,quote,actual,actual is>=.10m and<=.20m,quote>4000m);
    }
}

public sealed class SaveProformaInvoiceCommand
{
    public uint InvoiceId { get; set; }
    public decimal PackagingFee { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal OtherFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? PaymentInstructions { get; set; }
    public string? TradeTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? LeadTime { get; set; }
    public DateTime? ValidUntil { get; set; }
    public List<UpdateProformaInvoicePriceItem> Items { get; set; } = [];
}
