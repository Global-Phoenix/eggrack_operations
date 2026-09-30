namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed class PurchaseRequestSource
{
    public uint Id { get; set; }
    public uint CurrentVersionId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public uint CurrentVersion { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public byte RequestStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public uint? PlanId { get; set; }
    public string? PlanNumber { get; set; }
    public uint? PlannedVersionId { get; set; }
    public ulong? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public byte? PlanStatus { get; set; }
    public string? PlanTitle { get; set; }
    public string Priority { get; set; } = "normal";
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? InternalNote { get; set; }
    public bool HasPlan => PlanId.HasValue;
}

public sealed class PurchaseRequestVersionSource
{
    public uint Id { get; set; }
    public uint RequestId { get; set; }
    public uint VersionNumber { get; set; }
    public string? CompanyName { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Whatsapp { get; set; }
    public string? Country { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? PickupTradeInfo { get; set; }
    public string? CustomerMessage { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string CustomerName => string.IsNullOrWhiteSpace(CompanyName) ? ContactName : CompanyName;
}
public sealed record PurchaseRequestVersionDetail(
    uint Id,uint RequestId,uint VersionNumber,string? CompanyName,string ContactName,string Email,
    string? Phone,string? Whatsapp,string? Country,string? DeliveryAddress,string? PickupTradeInfo,
    string? CustomerMessage,DateTime SubmittedAtUtc,IReadOnlyList<PurchaseRequestVersionItemDetail> Items,
    IReadOnlyList<PurchaseRequestAttachmentDetail> Attachments)
{
    public string CustomerName => string.IsNullOrWhiteSpace(CompanyName) ? ContactName : CompanyName;
}
public sealed class PurchaseRequestVersionItemDetail
{
    public uint Id { get; set; }
    public uint VersionId { get; set; }
    public string ProductKey { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? ReferenceUrl { get; set; }
    public decimal? TargetUnitPrice { get; set; }
    public string? Currency { get; set; }
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
    public uint? VersionItemId { get; set; }
    public string FileType { get; set; } = "other";
    public string OriginalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public uint FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string? RelatedProductName { get; set; }
}
public sealed record ProcurementRequestContext(
    string RequestNumber,string PlanNumber,uint VersionNumber,string CustomerName,string Email,
    DateTime SubmittedAtUtc,IReadOnlyList<PurchaseRequestVersionItemDetail> Items,
    IReadOnlyList<PurchaseRequestAttachmentDetail> Attachments);
public sealed record PurchaseRequestItemSource(uint Id,uint RequestId,uint VersionId,string ProductKey,string ProductName,decimal Quantity,string Unit,string SnapshotJson);
public sealed record ProcurementBuyerOption(ulong Id,string Name);
public sealed class SaveProcurementPlanInput
{
    public uint RequestVersionId { get; set; }
    public ulong AssignedBuyerStaffId { get; set; }
    public string Priority { get; set; } = "normal";
    public string? InternalNote { get; set; }
    public List<SaveProcurementPlanItemCommand> Items { get; set; } = [];
}
public sealed class ProcurementPlanEditor
{
    public uint Id { get; set; }
    public uint RequestId { get; set; }
    public uint RequestVersionId { get; set; }
    public ulong? AssignedBuyerStaffId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanTitle { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? InternalNote { get; set; }
    public IReadOnlyList<ProcurementPlanItemOption> Items { get; set; } = [];
}
