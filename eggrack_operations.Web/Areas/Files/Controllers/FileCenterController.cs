using System.Security.Cryptography;
using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Web.Areas.Files.Services;
using Eggrack.Operations.Infrastructure.Modules.Files;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Web.Areas.Files.Controllers;

[Authorize]
[InternalPermission("files.view")]
[Area("Files")]
[Route("files")]
public sealed class FileCenterController(
    FileCenterDataService files,
    FileCenterStorageService storage,
    CurrentStaffAccessor currentStaff,
    PermissionEvaluator permissionEvaluator) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(
        string? keyword,
        string? sourceKind,
        string? fileType,
        string? dateRange,
        CancellationToken token)
    {
        var query=new FileCenterQuery(keyword,sourceKind,fileType,dateRange);
        var purchaseFiles = await files.GetPurchaseFilesAsync(
            await CanViewInternalDocumentsAsync(token),query,token);
        return View(new FileCenterIndexViewModel(purchaseFiles,query));
    }

    [HttpGet("content/{sourceKind}/{id:long}")]
    public async Task<IActionResult> Content(string sourceKind, uint id, bool download = false, CancellationToken token = default)
    {
        var file = await files.GetStoredFileAsync(sourceKind, id, token);
        if (file is null) return NotFound();
        if (file.IsInternal && !await CanViewInternalDocumentsAsync(token)) return Forbid();
        try
        {
            var stream = await storage.OpenReadAsync(file, token);
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; media-src 'self'; img-src 'self' data:";
            Response.Headers.CacheControl = "private, no-store, max-age=0";
            Response.Headers.Pragma = "no-cache";
            var preview = FilePreviewPolicy.Resolve(file.OriginalName, file.MimeType);
            if (!download && preview.Kind == FilePreviewKind.Spreadsheet)
            {
                await using (stream)
                {
                    return Content(XlsxPreviewRenderer.Render(stream), "text/html; charset=utf-8");
                }
            }
            if (download || !preview.CanInline)
                return File(stream, "application/octet-stream", file.OriginalName, enableRangeProcessing: file.SourceKind == "plan");
            return File(stream, preview.ContentType, enableRangeProcessing: file.SourceKind == "plan");
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (InvalidDataException) { return UnprocessableEntity(); }
        catch (CryptographicException) { return UnprocessableEntity(); }
    }

    private async Task<bool> CanViewInternalDocumentsAsync(CancellationToken token)
    {
        var authorization=await currentStaff.LoadAsync(token);
        return authorization is not null &&
            permissionEvaluator.Evaluate(authorization,"wholesale.purchase-document.internal").Allowed;
    }
}
