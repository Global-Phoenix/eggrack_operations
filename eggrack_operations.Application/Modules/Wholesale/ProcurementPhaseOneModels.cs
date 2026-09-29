namespace Eggrack.Operations.Application.Modules.Wholesale;
public static class ProcurementWorkflowStatus { public const byte Draft=1,Sourcing=2,PendingDepartmentReview=3,Approved=4,Completed=5,PendingFinalApproval=6; }
public sealed record SelectInquiryCommand(uint PlanItemId,uint InquiryId);
public sealed record ProcurementProductCost(uint PlanItemId,string ProductName,decimal Quantity,string Unit,uint InquiryId,string SupplierName,string Currency,decimal UnitPrice,decimal AmountCny);
public sealed record ProcurementCostSnapshotSummary(uint Id,uint RevisionNo,string Status,decimal ProductCostCny,decimal SharedCostCny,decimal TotalCostCny,decimal TotalCostUsd,decimal SuggestedQuoteUsd,ulong SubmittedBy,DateTime SubmittedAtUtc);
public sealed record ProcurementQuoteReviewItem(uint Id,string Stage,string Decision,decimal? ProposedQuoteUsd,decimal? ActualProfitRate,string? Note,ulong ActedBy,DateTime ActedAtUtc);
public sealed record ProcurementWorkflowEventItem(ulong Id,string EventCode,byte? FromStatus,byte? ToStatus,string? Note,ulong ActorId,DateTime CreatedAtUtc);
public sealed record SubmitCostReviewResult(uint SnapshotId,uint RevisionNo,string Status);
public sealed record DepartmentQuoteReviewCommand(uint PlanId,decimal ProposedQuoteUsd,string? Note,long StaffId);
