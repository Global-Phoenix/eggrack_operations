using Eggrack.Operations.Application.Modules.Wholesale;
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
public sealed partial class ProcurementController(ProcurementDataService procurement,ProcurementMailDispatcher mailDispatcher) : Controller
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
        View("Sourcing",new SourcingPageViewModel(await procurement.GetPlansAsync(token)));

    [HttpGet("plans/{planId:long}/details")]
    public async Task<IActionResult> Details(uint planId,CancellationToken token)
    {
        var plans=await procurement.GetPlansAsync(token);
        var plan=plans.SingleOrDefault(item=>item.Id==planId);
        if(plan is null)return NotFound();
        ViewData["Title"]=$"{plan.PlanNumber} 详情";
        return View("Sourcing",new SourcingPageViewModel(plans,plan.Id,plan.PlanNumber));
    }

    [HttpGet("request-options")]
    public async Task<IActionResult> RequestOptions([FromQuery]string? keyword,CancellationToken token) =>
        Json(await procurement.GetPurchaseRequestsAsync(keyword,token));

    [HttpGet("requests/{requestId:long}/versions")]
    public async Task<IActionResult> Versions(uint requestId,CancellationToken token) =>
        Json(await procurement.GetRequestVersionsAsync(requestId,token));

    [HttpPost("plans")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-plan.create")]
    public async Task<IActionResult> Create([FromForm]uint requestId,[FromForm]uint requestVersionId,[FromForm]ulong assignedBuyerStaffId,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId)) return Forbid();
        if(assignedBuyerStaffId==0) return UnprocessableEntity(new{ok=false,message="请选择采购人员。"});
        try
        {
            var id=await procurement.CreatePlanAsync(new(requestId,requestVersionId,assignedBuyerStaffId,staffId),token);
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
    public async Task<IActionResult> Update(uint planId,[FromForm]uint requestVersionId,[FromForm]ulong assignedBuyerStaffId,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId)) return Forbid();
        if(assignedBuyerStaffId==0) return UnprocessableEntity(new{ok=false,message="请选择采购人员。"});
        try
        {
            await procurement.UpdatePlanAsync(new(planId,requestVersionId,assignedBuyerStaffId,staffId),token);
            return Json(new{ok=true});
        }
        catch(InvalidOperationException error)
        {
            return UnprocessableEntity(new{ok=false,message=error.Message});
        }
    }

    [HttpPost("plans/{planId:long}/costs")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.manage")]
    public async Task<IActionResult> SaveCosts(uint planId,[FromForm]ProcurementCostInput input,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try
        {
            await procurement.SaveCostsAsync(planId,input,staffId,token);
            return Json(new{ok=true,data=ProcurementPricing.Calculate(input)});
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
}
