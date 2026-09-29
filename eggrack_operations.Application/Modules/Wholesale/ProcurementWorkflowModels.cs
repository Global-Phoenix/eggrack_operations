namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed class SupplierListItem
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? LegalRepresentative { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public string? ContactJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
}
public sealed record CreateSupplierCommand(string Name,string? Code,string? Address,string? LegalRepresentative,string? ContactName,string? ContactPhone,string? Website);
public sealed record RecordInquiryCommand(uint PlanItemId,uint SupplierId,decimal? UnitPriceCny,decimal? Moq,int? LeadDays,DateOnly? ValidUntil,string? Terms);
public sealed record RecordSampleCommand(uint PlanItemId,uint? SupplierId,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record QuoteDecisionCommand(uint PlanId,decimal QuoteUsd,string? Note,long StaffId);
public sealed record QuoteDecisionResult(long ApprovalId,long? MailTaskId,long? ProformaInvoiceId,string? ProformaInvoiceNumber,string Status);
public sealed record ProcurementPlanItemOption(uint Id,string ProductName,decimal Quantity,string Unit);
public sealed record CandidateProductItem(uint Id,uint PlanItemId,uint? SupplierId,string ProductName,string? SupplierName,string? ReferenceUrl,string? SpecificationJson,string Status);
public sealed record InquiryItem(uint Id,uint PlanItemId,uint SupplierId,string ProductName,string SupplierName,string? OfferedProductName,decimal? LengthCm,decimal? WidthCm,decimal? HeightCm,decimal? WeightKg,string? Color,string? SizeDetails,string? ParameterDetails,string Currency,decimal? UnitPrice,decimal? Moq,uint? LeadDays,DateTime? ValidUntil,string? Terms,string Status,string? Notes);
public sealed record SampleItem(uint Id,uint PlanItemId,uint? SupplierId,string ProductName,string? SupplierName,decimal Quantity,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record SampleFileItem(uint Id,uint? SampleId,string OriginalName,string? Description,string MimeType,uint FileSize,DateTime UploadedAtUtc);
public sealed record MailTaskItem(uint Id,string Recipient,string Status,string TemplateCode,byte Attempts,string? LastError,DateTime CreatedAtUtc,DateTime? SentAtUtc);
public sealed record ProformaInvoiceSummary(uint Id,string Number,string Status,decimal TotalAmount,string Currency,DateTime CreatedAtUtc,DateTime? IssuedAtUtc);
public sealed record ProformaInvoicePriceItem(uint Id,string ProductName,decimal Quantity,string Unit,decimal UnitPrice,decimal LineAmount);
public sealed record ProformaInvoicePricing(uint InvoiceId,decimal ProductAmount,decimal PackagingFee,decimal ShippingFee,decimal OtherFee,decimal DiscountAmount,decimal TotalAmount,IReadOnlyList<ProformaInvoicePriceItem> Items);
public sealed record UpdateProformaInvoicePriceItem(uint Id,decimal UnitPrice);
public sealed record UpdateProformaInvoicePricingCommand(uint InvoiceId,decimal PackagingFee,decimal ShippingFee,decimal OtherFee,decimal DiscountAmount,List<UpdateProformaInvoicePriceItem> Items);
public sealed record ProcurementLifecycleResult(uint PlanId,string PlanStatus,uint ProformaInvoiceId,string ProformaInvoiceNumber,string ProformaInvoiceStatus);
public sealed record SourcingWorkspace(ProcurementRequestContext Request,IReadOnlyList<ProcurementPlanItemOption> PlanItems,IReadOnlyList<SupplierListItem> Suppliers,IReadOnlyList<CandidateProductItem> Candidates,IReadOnlyList<InquiryItem> Inquiries,IReadOnlyList<SampleItem> Samples,IReadOnlyList<SampleFileItem> SampleFiles,IReadOnlyList<MailTaskItem> MailTasks,ProformaInvoiceSummary? ProformaInvoice,ProformaInvoicePricing? ProformaInvoicePricing);
public sealed record SaveCandidateProductCommand(uint? Id,uint PlanItemId,uint? SupplierId,string ProductName,string? ReferenceUrl,string? SpecificationJson,string Status);
public sealed record SaveInquiryCommand(uint? Id,uint PlanItemId,uint SupplierId,string? OfferedProductName,decimal? LengthCm,decimal? WidthCm,decimal? HeightCm,decimal? WeightKg,string? Color,string? SizeDetails,string? ParameterDetails,string Currency,decimal? UnitPrice,decimal? Moq,uint? LeadDays,DateTime? ValidUntil,string? Terms,string Status,string? Notes);
public sealed record SaveSampleCommand(uint? Id,uint PlanItemId,uint? SupplierId,decimal Quantity,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record ProcurementCostItemInput(string Name,decimal AmountCny);
public sealed record ProcurementCostItem(uint Id,string Name,decimal AmountCny,uint SortOrder);
public sealed record SaveProcurementCostsCommand(decimal CnyPerUsd,decimal ProfitRate,List<ProcurementCostItemInput> Items);
public sealed record ProcurementCosts(decimal CnyPerUsd,decimal ProfitRate,decimal TotalCostCny,decimal TotalCostUsd,decimal SuggestedQuoteUsd,IReadOnlyList<ProcurementCostItem> Items);
