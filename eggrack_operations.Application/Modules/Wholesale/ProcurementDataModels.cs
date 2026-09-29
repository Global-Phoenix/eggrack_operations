namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record PurchaseRequestSource(
    uint Id, uint CurrentVersionId, string RequestNumber, uint CurrentVersion,
    string CustomerName, string Email, DateTime SubmittedAtUtc,
    uint? PlanId, string? PlanNumber, uint? PlannedVersionId, ulong? BuyerId, string? BuyerName)
{
    public bool HasPlan => PlanId.HasValue;
}

public sealed record PurchaseRequestVersionSource(uint Id,uint RequestId,uint VersionNumber,string CustomerName,string Email,DateTime SubmittedAtUtc);
public sealed record PurchaseRequestItemSource(uint Id,uint RequestId,uint VersionId,string ProductKey,string ProductName,decimal Quantity,string Unit,string SnapshotJson);
public sealed record ProcurementBuyerOption(ulong Id,string Name);
public sealed record CreateProcurementPlanCommand(uint RequestId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong CreatedByStaffId);
public sealed record UpdateProcurementPlanCommand(uint PlanId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong UpdatedByStaffId);