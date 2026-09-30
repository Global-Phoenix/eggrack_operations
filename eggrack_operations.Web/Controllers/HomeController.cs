using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using eggrack_operations.Models;
using eggrack_operations.Areas.Security;
using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;

namespace eggrack_operations.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    ProcurementDataService procurement,
    CurrentStaffAccessor currentStaff,
    PermissionEvaluator permissionEvaluator,
    CurrentAuthorizationContext authorizationContext) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;

    public async Task<IActionResult> Index(CancellationToken token)
    {
        var authorization=await currentStaff.LoadAsync(token);
        var decision=authorization is null
            ? null
            : permissionEvaluator.Evaluate(authorization,"wholesale.purchase-plan.view");
        if(decision is not {Allowed:true})
            return View(new OperationsOverviewPageViewModel(
                User.Identity?.Name??"用户",false,[],[],authorization is null?null:checked((ulong)authorization.StaffId),
                false,false,false,false));

        authorizationContext.Set(decision);
        bool Allowed(string permission)=>permissionEvaluator.Evaluate(authorization!,permission).Allowed;
        var requestsTask=procurement.GetPurchaseRequestsAsync(null,token);
        var plansTask=procurement.GetPlansAsync(token);
        await Task.WhenAll(requestsTask,plansTask);
        return View(new OperationsOverviewPageViewModel(
            User.Identity?.Name??"用户",true,await requestsTask,await plansTask,checked((ulong)authorization!.StaffId),
            Allowed("wholesale.purchase-plan.create"),Allowed("wholesale.purchase-plan.update"),
            Allowed("wholesale.purchase-quote.review"),Allowed("wholesale.purchase-quote.final-approve")));
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
