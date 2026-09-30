using Eggrack.Operations.Application.Modules.Wholesale;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Wholesale.Controllers;

public sealed partial class ProcurementController
{
    private IActionResult PlanDetailsRedirect(uint planId)=>RedirectToAction(nameof(Details),new{planId});

    [HttpPost("plans/{planId:long}/suppliers")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> CreatePlanSupplier(uint planId,[FromForm]CreateSupplierCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try
        {
            var supplierId=await procurement.CreatePlanSupplierAsync(planId,command,staffId,token);
            const string message="供应商已加入公司公共供应商库，可继续绑定到计划产品或录入报价。";
            if(WantsJson())return Json(new{ok=true,supplierId,message});
            TempData["Success"]=message;
        }
        catch(InvalidOperationException error)
        {
            if(WantsJson())return UnprocessableEntity(new{ok=false,message=error.Message});
            TempData["Error"]=error.Message;
        }
        return RedirectToAction(nameof(Details),null,new{planId},"sourcing");
    }

    [HttpPost("plans/{planId:long}/candidates")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> SavePlanCandidate(uint planId,[FromForm]SaveCandidateProductCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try
        {
            var plan=await procurement.GetPurchasePlanDetailAsync(planId,token);
            if(plan.Items.All(item=>item.Id!=command.PlanItemId))throw new InvalidOperationException("计划产品不属于当前采购计划。");
            await procurement.SaveCandidateAsync(command,staffId,token);
            TempData["Success"]="已将公司供应商及本次候选产品绑定到计划产品。";
        }
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return RedirectToAction(nameof(Details),null,new{planId},"sourcing");
    }

    [HttpPost("plans/{planId:long}/inquiries")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> SavePlanInquiry(uint planId,[FromForm]SaveInquiryCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try
        {
            var plan=await procurement.GetPurchasePlanDetailAsync(planId,token);
            if(plan.Items.All(item=>item.Id!=command.PlanItemId))throw new InvalidOperationException("计划产品不属于当前采购计划。");
            await procurement.SaveInquiryRevisionAsync(command,staffId,token);
            TempData["Success"]=command.Id.HasValue?"供应商报价修订版本已保存。":"供应商报价已录入。";
        }
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/inquiries/select")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> SelectPlanInquiry(uint planId,[FromForm]SelectInquiryCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.SelectInquiryAsync(planId,command,staffId,token);TempData["Success"]="已选择该供应商报价作为产品成本依据。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/samples")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> SavePlanSample(uint planId,[FromForm]SaveSampleCommand command,CancellationToken token)
    {
        try
        {
            var plan=await procurement.GetPurchasePlanDetailAsync(planId,token);
            if(plan.Items.All(item=>item.Id!=command.PlanItemId))throw new InvalidOperationException("计划产品不属于当前采购计划。");
            await procurement.SaveSampleAsync(command,token);
            TempData["Success"]=command.Id.HasValue?"样品记录已更新。":"样品记录已添加。";
        }
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/items/procurement")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> SavePlanItemProcurement(uint planId,[FromForm]SavePurchasePlanItemProcurementCommand command,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId))return Forbid();
        try{await procurement.SavePurchasePlanItemProcurementAsync(planId,command,staffId,token);TempData["Success"]="产品采购信息已保存。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/cost-quote")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-cost.edit")]
    public async Task<IActionResult> SavePlanCostQuote(uint planId,[FromForm]SavePurchasePlanCostQuoteCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try
        {
            var result=await procurement.SavePurchasePlanCostQuoteAsync(planId,command,staffId,token);
            TempData["Success"]=$"成本与建议报价已保存：USD {result.QuoteUsd:N2}。";
            if(!result.ProfitRateInRange||!result.QuoteAboveMinimum)
                TempData["Warning"]="提示：当前报价未同时满足利润率 10%–20% 且报价高于 USD 4,000，Boss 仍可特殊审批。";
        }
        catch(Exception error) when(error is InvalidOperationException or ArgumentOutOfRangeException){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/quote/submit")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.submit")]
    public async Task<IActionResult> SubmitPlanQuote(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.SubmitPurchasePlanQuoteAsync(planId,staffId,token);TempData["Success"]="报价已提交 Boss 最终确认。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/quote/boss-approve")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.final-approve")]
    public async Task<IActionResult> BossApprovePlanQuote(uint planId,[FromForm]decimal quoteUsd,[FromForm]string? note,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.ApprovePurchasePlanQuoteAsync(planId,quoteUsd,note,staffId,token);TempData["Success"]="Boss 已确认最终报价，可以生成 PI。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/pi/generate")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-pi.manage")]
    public async Task<IActionResult> GeneratePlanPi(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{var result=await procurement.GenerateProformaInvoiceAsync(planId,staffId,token);TempData["Success"]=$"{result.ProformaInvoiceNumber} 已生成，请填写客户销售单价和条款。";return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId=result.ProformaInvoiceId});}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/pi/save")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-pi.manage")]
    public async Task<IActionResult> SavePlanPi(uint planId,[FromForm]SaveProformaInvoiceCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.SaveProformaInvoiceAsync(planId,command,staffId,token);TempData["Success"]="PI 定价和条款已保存并进入待签发状态。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId=command.InvoiceId});
    }

    [HttpPost("plans/{planId:long}/pi/issue-page")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-pi.issue")]
    public async Task<IActionResult> IssuePlanPi(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{var result=await procurement.IssueProformaInvoiceAsync(planId,staffId,token);TempData["Success"]=$"{result.ProformaInvoiceNumber} 已签发，后续禁止直接修改。";return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId=result.ProformaInvoiceId});}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/pi/{invoiceId:long}/revision")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-pi.manage")]
    public async Task<IActionResult> CreatePlanPiRevision(uint planId,uint invoiceId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try
        {
            var replacementId=await procurement.CreateReplacementProformaInvoiceAsync(planId,invoiceId,staffId,token);
            TempData["Success"]="已从签发快照创建新的 PI 修订草稿，原 PI 保持不变。";
            return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId=replacementId});
        }
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId});
    }

    [HttpPost("plans/{planId:long}/pi/{invoiceId:long}/cancel")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-pi.manage")]
    public async Task<IActionResult> CancelPlanPi(uint planId,uint invoiceId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.CancelProformaInvoiceAsync(planId,invoiceId,staffId,token);TempData["Success"]="PI 已取消，可以从采购计划重新生成。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return RedirectToAction(nameof(ProformaInvoiceDetails),new{invoiceId});
    }

    [HttpPost("plans/{planId:long}/complete-workflow")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.execute")]
    public async Task<IActionResult> CompletePurchasePlan(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId))return Forbid();
        try{await procurement.CompletePurchasePlanAsync(planId,staffId,token);TempData["Success"]="采购计划已完成，采购申请状态已同步。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/files/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    [InternalPermission("wholesale.purchase-document.internal")]
    public async Task<IActionResult> UploadPlanFile(
        uint planId,[FromForm]IFormFile file,[FromForm]uint? planItemId,[FromForm]string fileType,
        [FromForm]string? title,[FromForm]string? description,[FromForm]uint? supplierId,
        [FromForm]uint? inquiryId,[FromForm]uint? sampleId,[FromForm]uint? fileTypeId,
        [FromForm]string visibility="internal",CancellationToken token=default)
    {
        if(!TryStaffIdUnsigned(out var staffId))return Forbid();
        if(file is null||file.Length==0){TempData["Error"]="请选择需要上传的文件。";return PlanDetailsRedirect(planId);}
        Eggrack.Operations.Infrastructure.Modules.Files.StoredPlanUpload? stored=null;
        try
        {
            await using var stream=file.OpenReadStream();
            stored=await fileStorage.SavePlanFileAsync(planId,file.FileName,file.ContentType,stream,token);
            await using var checksumStream=System.IO.File.OpenRead(stored.PhysicalPath);
            var checksum=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(checksumStream,token)).ToLowerInvariant();
            if(fileTypeId.HasValue)
                await procurement.AddProcurementDocumentAsync(planId,planItemId,supplierId,inquiryId,sampleId,
                    fileTypeId.Value,visibility,stored.OriginalName,stored.StoragePath,checksum,stored.MimeType,
                    stored.FileSize,description,staffId,token);
            else
                await procurement.AddPurchasePlanFileAsync(planId,planItemId,fileType,title,description,stored.OriginalName,
                    stored.StoragePath,checksum,stored.MimeType,stored.FileSize,staffId,token);
            TempData["Success"]="采购计划文件已上传，默认仅内部可见。";
        }
        catch(Exception error) when(error is InvalidDataException or InvalidOperationException)
        {
            if(stored is not null&&System.IO.File.Exists(stored.PhysicalPath))System.IO.File.Delete(stored.PhysicalPath);
            TempData["Error"]=error.Message;
        }
        return PlanDetailsRedirect(planId);
    }

    [HttpPost("plans/{planId:long}/files/{fileId:long}/visibility")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-document.internal")]
    public async Task<IActionResult> SetPlanFileVisibility(uint planId,uint fileId,[FromForm]bool customerVisible,CancellationToken token)
    {
        if(!TryStaffIdUnsigned(out var staffId))return Forbid();
        try{await procurement.SetPurchasePlanFileVisibilityAsync(planId,fileId,customerVisible,staffId,token);TempData["Success"]=customerVisible?"文件已设置为客户可见。":"文件已恢复为仅内部可见。";}
        catch(InvalidOperationException error){TempData["Error"]=error.Message;}
        return PlanDetailsRedirect(planId);
    }

    private bool TryStaffId(out long staffId) =>
        HttpContext.TryGetInternalStaffId(out staffId);
}
