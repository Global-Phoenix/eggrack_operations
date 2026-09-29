namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record SupplierListItem(uint Id,string Name,string? Code,string Status);
public sealed record CreateSupplierCommand(string Name,string? Code,string? ContactJson);
public sealed record RecordInquiryCommand(uint PlanItemId,uint SupplierId,decimal? UnitPriceCny,decimal? Moq,int? LeadDays,DateOnly? ValidUntil,string? Terms);
public sealed record RecordSampleCommand(uint PlanItemId,uint? SupplierId,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record QuoteDecisionCommand(uint PlanId,decimal QuoteUsd,string? Note,long StaffId);
public sealed record QuoteDecisionResult(long ApprovalId,long? MailTaskId,string Status);
public sealed record ProcurementPlanItemOption(uint Id,string ProductName,decimal Quantity,string Unit);
public sealed record CandidateProductItem(uint Id,uint PlanItemId,uint? SupplierId,string ProductName,string? SupplierName,string? ReferenceUrl,string? SpecificationJson,string Status);
public sealed record InquiryItem(uint Id,uint PlanItemId,uint SupplierId,string ProductName,string SupplierName,string Currency,decimal? UnitPrice,decimal? Moq,uint? LeadDays,DateTime? ValidUntil,string? Terms,string Status,string? Notes);
public sealed record SampleItem(uint Id,uint PlanItemId,uint? SupplierId,string ProductName,string? SupplierName,decimal Quantity,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record MailTaskItem(uint Id,string Recipient,string Status,string TemplateCode,DateTime CreatedAtUtc,DateTime? SentAtUtc);
public sealed record SourcingWorkspace(IReadOnlyList<ProcurementPlanItemOption> PlanItems,IReadOnlyList<SupplierListItem> Suppliers,IReadOnlyList<CandidateProductItem> Candidates,IReadOnlyList<InquiryItem> Inquiries,IReadOnlyList<SampleItem> Samples,IReadOnlyList<MailTaskItem> MailTasks);
public sealed record SaveCandidateProductCommand(uint? Id,uint PlanItemId,uint? SupplierId,string ProductName,string? ReferenceUrl,string? SpecificationJson,string Status);
public sealed record SaveInquiryCommand(uint? Id,uint PlanItemId,uint SupplierId,string Currency,decimal? UnitPrice,decimal? Moq,uint? LeadDays,DateTime? ValidUntil,string? Terms,string Status,string? Notes);
public sealed record SaveSampleCommand(uint? Id,uint PlanItemId,uint? SupplierId,decimal Quantity,string Status,decimal CostCny,string? TrackingNumber,string? Notes);