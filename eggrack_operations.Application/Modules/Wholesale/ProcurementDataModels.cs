namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record PurchaseRequestSource(long Id,string RequestNumber,int CurrentVersion,string CustomerName,string Email,DateTime SubmittedAtUtc);
public sealed record PurchaseRequestVersionSource(long Id,long RequestId,int VersionNumber,string CustomerName,string Email,DateTime SubmittedAtUtc);
public sealed record PurchaseRequestItemSource(long Id,long RequestId,long VersionId,string ProductKey,string ProductName,decimal Quantity,string Unit,string SnapshotJson);
public sealed record CreateProcurementPlanCommand(long RequestId,long RequestVersionId,long? AssignedBuyerStaffId,long CreatedByStaffId);
