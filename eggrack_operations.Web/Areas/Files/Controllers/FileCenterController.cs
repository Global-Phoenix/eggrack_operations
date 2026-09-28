using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Infrastructure.Modules.Files;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Web.Areas.Files.Controllers;

[Area("Files")]
[Route("files")]
public sealed class FileCenterController(FileCenterDataService files) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var purchaseFiles = await files.GetPurchaseFilesAsync(token);
        return View(new FileCenterIndexViewModel(purchaseFiles));
    }
}
