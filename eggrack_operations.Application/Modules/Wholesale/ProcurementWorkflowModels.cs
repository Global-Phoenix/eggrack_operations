namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record SupplierListItem(uint Id,string Name,string? Code,string Status);
public sealed record CreateSupplierCommand(string Name,string? Code,string? ContactJson);
public sealed record RecordInquiryCommand(uint PlanItemId,uint SupplierId,decimal? UnitPriceCny,decimal? Moq,int? LeadDays,DateOnly? ValidUntil,string? Terms);
public sealed record RecordSampleCommand(uint PlanItemId,uint? SupplierId,string Status,decimal CostCny,string? TrackingNumber,string? Notes);
public sealed record QuoteDecisionCommand(uint PlanId,decimal QuoteUsd,string? Note,long StaffId);
public sealed record QuoteDecisionResult(long ApprovalId,long? MailTaskId,string Status);
