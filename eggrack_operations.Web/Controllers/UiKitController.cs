using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Controllers;

public sealed class UiKitController : Controller
{
    public IActionResult Index() => View();
}