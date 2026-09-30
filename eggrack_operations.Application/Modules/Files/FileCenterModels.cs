namespace Eggrack.Operations.Application.Modules.Files;

public sealed record FileCenterFileItem(
    string ItemKey,
    string SourceKind,
    uint SourceId,
    uint FileId,
    string OriginalName,
    string MimeType,
    uint FileSize,
    string SourceNumber,
    string CategoryName,
    string UploadedBy,
    string VisibilityCode,
    DateTime UploadedAtUtc,
    long Status)
{
    public string FileType
    {
        get
        {
            var extension = Path.GetExtension(OriginalName).TrimStart('.').ToUpperInvariant();
            return string.IsNullOrEmpty(extension) ? "FILE" : extension;
        }
    }

    public string FileSizeText => FileSize switch
    {
        >= 1_073_741_824 => $"{FileSize / 1_073_741_824d:N1} GB",
        >= 1_048_576 => $"{FileSize / 1_048_576d:N1} MB",
        >= 1_024 => $"{FileSize / 1_024d:N0} KB",
        _ => $"{FileSize} B"
    };

    public string SourceLabel => SourceKind == "plan" ? "采购计划" : "采购申请";
    public string VisibilityLabel => SourceKind == "request"
        ? "客户提交"
        : VisibilityCode.Equals("customer", StringComparison.OrdinalIgnoreCase) ? "客户可见" : "仅内部";

    public string PreviewKind => FilePreviewPolicy.Resolve(OriginalName, MimeType).Kind.ToString().ToLowerInvariant();

    public string BusinessUrl => SourceKind == "plan"
        ? $"/wholesale/procurement/plans/{SourceId}/details"
        : $"/wholesale/procurement/requests/{SourceId}/details";
}

public sealed record FileCenterQuery(
    string? Keyword = null,
    string? SourceKind = null,
    string? FileType = null,
    string? DateRange = null);

public sealed record FileCenterIndexViewModel(
    IReadOnlyList<FileCenterFileItem> PurchaseFiles,
    FileCenterQuery Query);
