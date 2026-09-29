using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Infrastructure.Modules.Files;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;
using eggrack_operations.Areas.Security;
using eggrack_operations.Areas.Wholesale.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Wholesale.Controllers;

[Area("Wholesale")]
[Authorize]
[Route("wholesale/procurement")]
[InternalPermission("wholesale.purchase-plan.view")]
public sealed partial class ProcurementController(ProcurementDataService procurement,ProcurementMailDispatcher mailDispatcher,FileCenterStorageService fileStorage,CurrentStaffAccessor currentStaff,PermissionEvaluator permissionEvaluator) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(PurchaseRequests));

    [HttpGet("requests")]
    public async Task<IActionResult> PurchaseRequests(CancellationToken token) =>
        View("Requests",new PurchaseRequestsPageViewModel(
            await procurement.GetPurchaseRequestsAsync(null,token),
            await procurement.GetBuyersAsync(token)));

    [HttpGet("sourcing")]
    public async Task<IActionResult> Sourcing(CancellationToken token) =>
        View("Sourcing",new SourcingPageViewModel(await procurement.GetPlansAsync(token),CanManageCosts:await CanManageCostsAsync(token)));

    [HttpGet("suppliers")]
    public async Task<IActionResult> SupplierDirectory([FromQuery]string? keyword,CancellationToken token) =>
        View("Suppliers",new SupplierDirectoryPageViewModel(
            await procurement.GetSuppliersAsync(keyword,token),keyword));

    [HttpGet("plans/{planId:long}/details")]
    public async Task<IActionResult> Details(uint planId,CancellationToken token)
    {
        var plans=await procurement.GetPlansAsync(token);
        var plan=plans.SingleOrDefault(item=>item.Id==planId);
        if(plan is null)return NotFound();
        ViewData["Title"]=$"{plan.PlanNumber} 详情";
        return View("Sourcing",new SourcingPageViewModel(plans,plan.Id,plan.PlanNumber,await CanManageCostsAsync(token)));
    }

    [HttpGet("request-options")]
    public async Task<IActionResult> RequestOptions([FromQuery]string? keyword,CancellationToken token) =>
        Json(await procurement.GetPurchaseRequestsAsync(keyword,token));

    [HttpGet("requests/{requestId:long}/versions")]
    public async Task<IActionResult> Versions(uint requestId,CancellationToken token) =>
        Json(await procurement.GetRequestVersionsAsync(requestId,token));

    [HttpGet("plans/{planId:long}/editor")]
    [InternalPermission("wholesale.purchase-plan.update")]
    public async Task<IActionResult> PlanEditor(uint planId,CancellationToken token)
    {
        try{return Json(await procurement.GetPlanEditorAsync(planId,token));}
        catch(InvalidOperationException error){return NotFound(new{ok=false,message=error.Message});}
    }

    [HttpPost("plans")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-plan.create")]
    public async Task<IActionResult> Create([FromForm]uint requestId,[FromForm]SaveProcurementPlanInput input,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId)) return Forbid();
        try
        {
            var id=await procurement.CreatePlanAsync(requestId,input,staffId,token);
            return Json(new{ok=true,id});
        }
        catch(InvalidOperationException error)
        {
            return UnprocessableEntity(new{ok=false,message=error.Message});
        }
    }

    [HttpPost("plans/{planId:long}")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-plan.update")]
    public async Task<IActionResult> Update(uint planId,[FromForm]SaveProcurementPlanInput input,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId)) return Forbid();
        try
        {
            await procurement.UpdatePlanAsync(planId,input,staffId,token);
            return Json(new{ok=true});
        }
        catch(InvalidOperationException error)
        {
            return UnprocessableEntity(new{ok=false,message=error.Message});
        }
    }

    [HttpGet("plans/{planId:long}/costs")]
    [InternalPermission("wholesale.purchase-cost.manage")]
    public async Task<IActionResult> Costs(uint planId,CancellationToken token) => Json(await procurement.GetFlexibleCostsAsync(planId,token));

    [HttpPost("plans/{planId:long}/costs")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.manage")]
    public async Task<IActionResult> SaveCosts(uint planId,[FromForm]SaveProcurementCostsCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try
        {
            return Json(new{ok=true,data=await procurement.SaveFlexibleCostsAsync(planId,command,staffId,token)});
        }
        catch(Exception error) when(error is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return UnprocessableEntity(new{ok=false,message=error.Message});
        }
    }

    [HttpPost("pricing")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.manage")]
    public IActionResult Pricing([FromForm]ProcurementCostInput input)
    {
        try{return Json(new{ok=true,data=ProcurementPricing.Calculate(input)});}
        catch(Exception error) when(error is ArgumentOutOfRangeException or InvalidOperationException)
        {return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    private bool TryStaffIdUnsigned(out ulong staffId) =>
        ulong.TryParse(User.FindFirst("eggrack_staff_id")?.Value,out staffId);
    private async Task<bool> CanManageCostsAsync(CancellationToken token)
    {
        var authorization=await currentStaff.LoadAsync(token);
        return authorization is not null&&permissionEvaluator.Evaluate(authorization,"wholesale.purchase-cost.manage").Allowed;
    }
}
