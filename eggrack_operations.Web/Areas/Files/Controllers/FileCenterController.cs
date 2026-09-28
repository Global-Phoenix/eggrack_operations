using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Web.Areas.Files.Controllers;

[Area("Files")]
[Route("files")]
public sealed class FileCenterController : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => View();
}
