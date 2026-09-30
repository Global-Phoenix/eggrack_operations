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
public sealed partial class ProcurementController(ProcurementDataService procurement,FileCenterStorageService fileStorage,CurrentStaffAccessor currentStaff,PermissionEvaluator permissionEvaluator) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(WholesaleOverview));

    [HttpGet("requests")]
    public async Task<IActionResult> PurchaseRequests(CancellationToken token) =>
        View("Requests",new PurchaseRequestsPageViewModel(
            await procurement.GetPurchaseRequestsAsync(null,token)));

    [HttpGet("overview")]
    public async Task<IActionResult> WholesaleOverview(CancellationToken token)
    {
        var requestsTask=procurement.GetPurchaseRequestsAsync(null,token);
        var plansTask=procurement.GetPlansAsync(token);
        await Task.WhenAll(requestsTask,plansTask);
        TryStaffIdUnsigned(out var staffId);
        return View("Overview",new WholesaleOverviewPageViewModel(
            await requestsTask,await plansTask,staffId==0?null:staffId));
    }

    [HttpGet("requests/{requestId:long}/details")]
    public async Task<IActionResult> RequestDetails(uint requestId,CancellationToken token)
    {
        var request=await procurement.GetPurchaseRequestAsync(requestId,token);
        if(request is null)return NotFound();
        var versions=await procurement.GetRequestVersionsAsync(requestId,token);
        var authorization=await currentStaff.LoadAsync(token);
        var canCreatePlan=authorization is not null&&
            permissionEvaluator.Evaluate(authorization,"wholesale.purchase-plan.create").Allowed;
        var canUpdatePlan=authorization is not null&&
            permissionEvaluator.Evaluate(authorization,"wholesale.purchase-plan.update").Allowed;
        ViewData["Title"]=$"{request.RequestNumber} 详情";
        return View("RequestDetails",new PurchaseRequestDetailsPageViewModel(request,versions,canCreatePlan,canUpdatePlan));
    }

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
        View("Sourcing",new SourcingPageViewModel(await procurement.GetPlansAsync(token)));

    [HttpGet("suppliers")]
    public async Task<IActionResult> SupplierDirectory([FromQuery]string? keyword,CancellationToken token) =>
        View("Suppliers",new SupplierDirectoryPageViewModel(await procurement.GetSuppliersAsync(keyword,token),keyword));

    [HttpGet("plans/{planId:long}/details")]
    public async Task<IActionResult> Details(uint planId,CancellationToken token)
    {
        try
        {
            var plan=await procurement.GetPurchasePlanDetailAsync(planId,token);
            var authorization=await currentStaff.LoadAsync(token);
            bool Has(string permission)=>authorization is not null&&permissionEvaluator.Evaluate(authorization,permission).Allowed;
            ViewData["Title"]=$"{plan.PlanNumber} 详情";
            return View("PlanDetails",new PurchasePlanDetailPageViewModel(
                plan,await procurement.GetPhaseOneWorkspaceAsync(planId,token),await procurement.GetBuyersAsync(token),
                Has("wholesale.procurement.execute"),Has("wholesale.purchase-cost.edit"),
                Has("wholesale.purchase-quote.submit"),Has("wholesale.purchase-quote.final-approve"),
                Has("wholesale.purchase-pi.manage"),Has("wholesale.purchase-pi.issue"),
                Has("wholesale.purchase-document.internal")));
        }
        catch(InvalidOperationException){return NotFound();}
    }

    [HttpGet("proforma-invoices/{invoiceId:long}")]
    public async Task<IActionResult> ProformaInvoiceDetails(uint invoiceId,CancellationToken token)
    {
        try
        {
            var invoice=await procurement.GetProformaInvoiceDetailAsync(invoiceId,token);
            if(invoice is null)return NotFound();
            var authorization=await currentStaff.LoadAsync(token);
            bool Has(string permission)=>authorization is not null&&permissionEvaluator.Evaluate(authorization,permission).Allowed;
            ViewData["Title"]=$"{invoice.Number} 详情";
            return View("ProformaInvoiceDetails",new ProformaInvoiceDetailPageViewModel(
                invoice,Has("wholesale.purchase-pi.manage"),Has("wholesale.purchase-pi.issue")));
        }
        catch(InvalidOperationException){return NotFound();}
    }

    [HttpGet("plans/{planId:long}/files/{fileId:long}/content")]
    [InternalPermission("wholesale.purchase-document.internal")]
    public async Task<IActionResult> PlanFileContent(uint planId,uint fileId,bool download=false,CancellationToken token=default)
    {
        var file=await procurement.GetPurchasePlanFileAsync(planId,fileId,token);
        if(file is null)return NotFound();
        try
        {
            var stream=await fileStorage.OpenReadAsync(file,token);
            Response.Headers.XContentTypeOptions="nosniff";
            return download
                ?File(stream,file.MimeType,file.OriginalName,enableRangeProcessing:true)
                :File(stream,file.MimeType,enableRangeProcessing:true);
        }
        catch(FileNotFoundException){return NotFound();}
        catch(InvalidDataException){return UnprocessableEntity();}
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

    private bool TryStaffIdUnsigned(out ulong staffId) =>
        ulong.TryParse(User.FindFirst("eggrack_staff_id")?.Value,out staffId);
}
