using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Wholesale.Controllers;

[Area("Wholesale")]
[Authorize]
[Route("wholesale/procurement")]
public sealed partial class ProcurementController(ProcurementDataService procurement) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await procurement.GetPlansAsync(token));

    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery]string? keyword,CancellationToken token) =>
        Json(await procurement.GetPurchaseRequestsAsync(keyword,token));

    [HttpPost("plans")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm]long requestId,[FromForm]long requestVersionId,[FromForm]long? assignedBuyerStaffId,CancellationToken token)
    {
        var staffClaim=User.FindFirst("eggrack_staff_id")?.Value;
        if(!long.TryParse(staffClaim,out var staffId)) return Forbid();
        var id=await procurement.CreatePlanAsync(new(requestId,requestVersionId,assignedBuyerStaffId,staffId),token);
        return Json(new{ok=true,id});
    }

    [HttpPost("plans/{planId:long}/costs")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCosts(long planId,[FromForm]ProcurementCostInput input,CancellationToken token)
    {
        var staffClaim=User.FindFirst("eggrack_staff_id")?.Value;
        if(!long.TryParse(staffClaim,out var staffId)) return Forbid();
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
    public IActionResult Pricing([FromForm]ProcurementCostInput input)
    {
        try{return Json(new{ok=true,data=ProcurementPricing.Calculate(input)});}
        catch(Exception error) when(error is ArgumentOutOfRangeException or InvalidOperationException)
        {return UnprocessableEntity(new{ok=false,message=error.Message});}
    }
}

