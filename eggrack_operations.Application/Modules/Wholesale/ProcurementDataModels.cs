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
public sealed record PurchaseRequestVersionDetail(
    uint Id,uint RequestId,uint VersionNumber,string CustomerName,string Email,
    DateTime SubmittedAtUtc,IReadOnlyList<PurchaseRequestVersionItemDetail> Items,
    IReadOnlyList<PurchaseRequestAttachmentDetail> Attachments);
public sealed class PurchaseRequestVersionItemDetail
{
    public uint Id { get; set; }
    public uint VersionId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? Specifications { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? PackagingRequirements { get; set; }
    public string? CustomizationRequirements { get; set; }
    public string? CustomerNote { get; set; }
}
public sealed class PurchaseRequestAttachmentDetail
{
    public uint Id { get; set; }
    public uint VersionId { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public uint FileSize { get; set; }
}
public sealed record ProcurementRequestContext(
    string RequestNumber,string PlanNumber,uint VersionNumber,string CustomerName,string Email,
    DateTime SubmittedAtUtc,IReadOnlyList<PurchaseRequestVersionItemDetail> Items,
    IReadOnlyList<PurchaseRequestAttachmentDetail> Attachments);
public sealed record PurchaseRequestItemSource(uint Id,uint RequestId,uint VersionId,string ProductKey,string ProductName,decimal Quantity,string Unit,string SnapshotJson);
public sealed record ProcurementBuyerOption(ulong Id,string Name);
public sealed record CreateProcurementPlanCommand(uint RequestId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong CreatedByStaffId);
public sealed record UpdateProcurementPlanCommand(uint PlanId,uint RequestVersionId,ulong AssignedBuyerStaffId,ulong UpdatedByStaffId);
