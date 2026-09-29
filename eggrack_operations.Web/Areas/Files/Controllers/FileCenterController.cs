using System.Security.Cryptography;
using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Web.Areas.Files.Services;
using Eggrack.Operations.Infrastructure.Modules.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Web.Areas.Files.Controllers;

[Authorize]
[Area("Files")]
[Route("files")]
public sealed class FileCenterController(FileCenterDataService files, FileCenterStorageService storage) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var purchaseFiles = await files.GetPurchaseFilesAsync(token);
        return View(new FileCenterIndexViewModel(purchaseFiles));
    }

    [HttpGet("content/{sourceKind}/{id:long}")]
    public async Task<IActionResult> Content(string sourceKind, uint id, bool download = false, CancellationToken token = default)
    {
        var file = await files.GetStoredFileAsync(sourceKind, id, token);
        if (file is null) return NotFound();
        try
        {
            var stream = await storage.OpenReadAsync(file, token);
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; media-src 'self'; img-src 'self' data:";
            if (!download && Path.GetExtension(file.OriginalName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                await using (stream)
                {
                    return Content(XlsxPreviewRenderer.Render(stream), "text/html; charset=utf-8");
                }
            }
            return download
                ? File(stream, file.MimeType, file.OriginalName, enableRangeProcessing: file.SourceKind == "plan")
                : File(stream, file.MimeType, enableRangeProcessing: file.SourceKind == "plan");
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (InvalidDataException) { return UnprocessableEntity(); }
        catch (CryptographicException) { return UnprocessableEntity(); }
    }
}