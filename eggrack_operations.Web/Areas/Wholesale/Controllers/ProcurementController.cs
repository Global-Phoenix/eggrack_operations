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
            await procurement.GetPurchaseRequestsAsync(null,token)));

    [HttpGet("requests/{requestId:long}/plan")]
    [InternalPermission("wholesale.purchase-plan.create")]
    public async Task<IActionResult> CreatePlanPage(uint requestId,CancellationToken token)
    {
        var request=await procurement.GetPurchaseRequestAsync(requestId,token);
        if(request is null)return NotFound();
        if(request.HasPlan)return RedirectToAction(nameof(EditPlanPage),new{planId=request.PlanId});
        ViewData["Title"]="创建采购计划";
        return View("PlanEditor",new ProcurementPlanEditorPageViewModel(
            request,null,await procurement.GetBuyersAsync(token)));
    }

    [HttpGet("plans/{planId:long}/edit")]
    [InternalPermission("wholesale.purchase-plan.update")]
    public async Task<IActionResult> EditPlanPage(uint planId,CancellationToken token)
    {
        try
        {
            var plan=await procurement.GetPlanEditorAsync(planId,token);
            var request=await procurement.GetPurchaseRequestAsync(plan.RequestId,token);
            if(request is null)return NotFound();
            ViewData["Title"]="更新采购计划";
            return View("PlanEditor",new ProcurementPlanEditorPageViewModel(
                request,plan,await procurement.GetBuyersAsync(token)));
        }
        catch(InvalidOperationException){return NotFound();}
    }

    [HttpGet("sourcing")]
    public async Task<IActionResult> Sourcing(CancellationToken token) =>
        View("Sourcing",await CreateSourcingViewModelAsync(await procurement.GetPlansAsync(token),token:token));

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
        return View("Sourcing",await CreateSourcingViewModelAsync(plans,plan.Id,plan.PlanNumber,token));
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
    [InternalPermission("wholesale.purchase-cost.edit")]
    public async Task<IActionResult> Costs(uint planId,CancellationToken token) => Json(await procurement.GetPhaseOneCostsAsync(planId,token));

    [HttpPost("plans/{planId:long}/costs")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.edit")]
    public async Task<IActionResult> SaveCosts(uint planId,[FromForm]SaveProcurementCostsCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try
        {
            return Json(new{ok=true,data=await procurement.SaveDraftCostsAsync(planId,command,staffId,token)});
        }
        catch(Exception error) when(error is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return UnprocessableEntity(new{ok=false,message=error.Message});
        }
    }

    [HttpPost("plans/{planId:long}/costs/submit")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.submit")]
    public async Task<IActionResult> SubmitCosts(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{return Json(new{ok=true,data=await procurement.SubmitCostReviewAsync(planId,staffId,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }
    [HttpPost("pricing")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.edit")]
    public IActionResult Pricing([FromForm]ProcurementCostInput input)
    {
        try{return Json(new{ok=true,data=ProcurementPricing.Calculate(input)});}
        catch(Exception error) when(error is ArgumentOutOfRangeException or InvalidOperationException)
        {return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    private bool TryStaffIdUnsigned(out ulong staffId) =>
        ulong.TryParse(User.FindFirst("eggrack_staff_id")?.Value,out staffId);
    private async Task<SourcingPageViewModel> CreateSourcingViewModelAsync(IReadOnlyList<ProcurementPlanListItem> plans,uint? planId=null,string? planNumber=null,CancellationToken token=default)
    {
        var authorization=await currentStaff.LoadAsync(token);
        bool Has(string permission)=>authorization is not null&&permissionEvaluator.Evaluate(authorization,permission).Allowed;
        return new(plans,planId,planNumber,
            CanManageCosts:Has("wholesale.purchase-cost.edit"),CanSubmitQuote:Has("wholesale.purchase-quote.submit"),
            CanReviewQuote:Has("wholesale.purchase-quote.review"),CanFinalApprove:Has("wholesale.purchase-quote.final-approve"),
            CanManagePi:Has("wholesale.purchase-pi.manage"),CanIssuePi:Has("wholesale.purchase-pi.issue"),CanSendMail:Has("wholesale.purchase-mail.send"));
    }
}
