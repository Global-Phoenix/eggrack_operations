namespace Eggrack.Operations.Domain.Entities;

public sealed class FileAsset : AuditableEntity
{
    public required string OriginalName { get; set; }
    public required string StorageName { get; set; }
    public required string StorageProvider { get; set; }
    public required string RelativePath { get; set; }
    public string? ContentType { get; set; }
    public long Length { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class BackgroundTask : AuditableEntity
{
    public required string TaskType { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public int Progress { get; set; }
    public string? PayloadJson { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class OperationLog
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public required string Category { get; set; }
    public required string Action { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public bool Succeeded { get; set; }
}

public sealed class EmailTemplate : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string SubjectTemplate { get; set; }
    public required string BodyTemplate { get; set; }
    public bool IsHtml { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
}
