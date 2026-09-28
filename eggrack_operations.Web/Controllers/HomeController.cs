using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using eggrack_operations.Models;

namespace eggrack_operations.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }


    [Route("status/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
