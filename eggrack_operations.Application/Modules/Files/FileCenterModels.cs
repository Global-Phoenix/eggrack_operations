namespace Eggrack.Operations.Application.Modules.Files;

public sealed record FileCenterFileItem(
    string ItemKey,
    string SourceKind,
    uint FileId,
    string OriginalName,
    string MimeType,
    uint FileSize,
    string SourceNumber,
    string CategoryName,
    string UploadedBy,
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
    public string StatusLabel => SourceKind == "plan" && Status == 0 ? "已停用" : SourceLabel + "附件";
    public string PreviewKind => MimeType.ToLowerInvariant() switch
    {
        var mime when mime.StartsWith("image/") => "image",
        "application/pdf" => "pdf",
        var mime when mime.StartsWith("video/") => "video",
        var mime when mime.StartsWith("text/") => "text",
        "application/csv" or "application/vnd.ms-excel" when FileType == "CSV" => "text",
        _ => "download"
    };
}

public sealed record FileCenterIndexViewModel(IReadOnlyList<FileCenterFileItem> PurchaseFiles);
