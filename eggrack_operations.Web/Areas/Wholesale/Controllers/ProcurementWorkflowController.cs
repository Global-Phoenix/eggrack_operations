using Eggrack.Operations.Application.Modules.Wholesale;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Wholesale.Controllers;

public sealed partial class ProcurementController
{
    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers([FromQuery]string? keyword,CancellationToken token) =>
        Json(await procurement.GetSuppliersAsync(keyword,token));

    [HttpPost("suppliers")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> CreateSupplier([FromForm]CreateSupplierCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.CreateSupplierAsync(command,token)});

    [HttpPost("inquiries")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> RecordInquiry([FromForm]RecordInquiryCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.RecordInquiryAsync(command,token)});

    [HttpPost("samples")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> RecordSample([FromForm]RecordSampleCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.RecordSampleAsync(command,token)});

    [HttpPost("plans/{planId:long}/approve")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> Approve(uint planId,[FromForm]decimal quoteUsd,[FromForm]string recipient,[FromForm]string? note,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.ApproveQuoteAsync(new(planId,quoteUsd,note,staffId),recipient,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    [HttpPost("plans/{planId:long}/reject")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> Reject(uint planId,[FromForm]decimal quoteUsd,[FromForm]string? note,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.RejectQuoteAsync(new(planId,quoteUsd,note,staffId),token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    [HttpGet("plans/{planId:long}/workspace")]
    public async Task<IActionResult> Workspace(uint planId,CancellationToken token) =>
        Json(await procurement.GetSourcingWorkspaceAsync(planId,token));

    [HttpPost("candidates")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> SaveCandidate([FromForm]SaveCandidateProductCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveCandidateAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }

    [HttpPost("inquiry-records")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> SaveInquiry([FromForm]SaveInquiryCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveInquiryAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }

    [HttpPost("sample-records")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> SaveSample([FromForm]SaveSampleCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveSampleAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }

    [HttpPost("plans/{planId:long}/pi/pricing")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> UpdateProformaInvoicePricing(uint planId,[FromForm]UpdateProformaInvoicePricingCommand command,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.UpdateProformaInvoicePricingAsync(planId,command,staffId,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    [HttpPost("plans/{planId:long}/pi/issue")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> IssueProformaInvoice(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.IssueProformaInvoiceAsync(planId,staffId,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    [HttpPost("plans/{planId:long}/complete")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.procurement.manage")]
    public async Task<IActionResult> CompletePlan(uint planId,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.CompletePlanAsync(planId,staffId,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }
    [HttpGet("mail-tasks/{mailTaskId:long}/preview")]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> PreviewMailTask(uint mailTaskId,CancellationToken token)
    {
        try{return Content(await mailDispatcher.PreviewHtmlAsync(mailTaskId,token),"text/html; charset=utf-8");}
        catch(InvalidOperationException error){return NotFound(error.Message);}
    }

    [HttpPost("mail-tasks/{mailTaskId:long}/send")]
    [ValidateAntiForgeryToken]
    [InternalPermission("wholesale.purchase-quote.approve")]
    public async Task<IActionResult> SendMailTask(uint mailTaskId,CancellationToken token)
    {
        try
        {
            var sent=await mailDispatcher.DispatchAsync(mailTaskId,token);
            return sent?Json(new{ok=true,message="邮件发送成功。"}):UnprocessableEntity(new{ok=false,message="邮件发送失败，已记录失败原因，可稍后重试。"});
        }
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    private bool TryStaffId(out long staffId) =>
        long.TryParse(User.FindFirst("eggrack_staff_id")?.Value,out staffId);
}
