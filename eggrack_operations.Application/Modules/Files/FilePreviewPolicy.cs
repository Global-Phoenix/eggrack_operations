namespace Eggrack.Operations.Application.Modules.Files;

public enum FilePreviewKind
{
    Download,
    Image,
    Pdf,
    Video,
    Text,
    Spreadsheet
}

public sealed record FilePreviewDecision(FilePreviewKind Kind, string ContentType)
{
    public bool CanInline => Kind != FilePreviewKind.Download;
}

public static class FilePreviewPolicy
{
    public static FilePreviewDecision Resolve(string originalName, string? suppliedMimeType)
    {
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var mime = suppliedMimeType?.Trim().ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            ".xlsx" when mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                new(FilePreviewKind.Spreadsheet, mime),
            ".jpg" or ".jpeg" when mime == "image/jpeg" => new(FilePreviewKind.Image, "image/jpeg"),
            ".png" when mime == "image/png" => new(FilePreviewKind.Image, "image/png"),
            ".gif" when mime == "image/gif" => new(FilePreviewKind.Image, "image/gif"),
            ".webp" when mime == "image/webp" => new(FilePreviewKind.Image, "image/webp"),
            ".pdf" when mime == "application/pdf" => new(FilePreviewKind.Pdf, "application/pdf"),
            ".mp4" when mime == "video/mp4" => new(FilePreviewKind.Video, "video/mp4"),
            ".webm" when mime == "video/webm" => new(FilePreviewKind.Video, "video/webm"),
            ".txt" when mime.StartsWith("text/") => new(FilePreviewKind.Text, "text/plain; charset=utf-8"),
            ".csv" when mime.StartsWith("text/") || mime is "application/csv" or "application/vnd.ms-excel" =>
                new(FilePreviewKind.Text, "text/plain; charset=utf-8"),
            _ => new(FilePreviewKind.Download, "application/octet-stream")
        };
    }
}
