namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed class PurchaseRequestSource
{
    public uint Id { get; set; }
    public uint CurrentVersionId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public uint CurrentVersion { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public uint? PlanId { get; set; }
    public string? PlanNumber { get; set; }
    public uint? PlannedVersionId { get; set; }
    public ulong? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public bool HasPlan => PlanId.HasValue;
}

public sealed record PurchaseRequestVersionSource(uint Id,uint RequestId,uint VersionNumber,string CustomerName,string Email,DateTime SubmittedAtUtc);
public sealed record PurchaseRequestItemSource(uint Id,uint RequestId,uint VersionId,string ProductKey,string ProductName,decimal Quantity,string Unit,string SnapshotJson);
public sealed record ProcurementBuyerOption(ulong Id,string Name);
public sealed record CreateProcurementPlanCommand(uint RequestId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong CreatedByStaffId);
public sealed record UpdateProcurementPlanCommand(uint PlanId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong UpdatedByStaffId);
