using Eggrack.Operations.Application.Modules.Wholesale;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Wholesale.Controllers;

public sealed partial class ProcurementController
{
    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers([FromQuery]string? keyword,CancellationToken token) =>
        Json(await procurement.GetSuppliersAsync(keyword,token));

    [HttpPost("suppliers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupplier([FromForm]CreateSupplierCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.CreateSupplierAsync(command,token)});

    [HttpPost("inquiries")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordInquiry([FromForm]RecordInquiryCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.RecordInquiryAsync(command,token)});

    [HttpPost("samples")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordSample([FromForm]RecordSampleCommand command,CancellationToken token) =>
        Json(new{ok=true,id=await procurement.RecordSampleAsync(command,token)});

    [HttpPost("plans/{planId:long}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(uint planId,[FromForm]decimal quoteUsd,[FromForm]string recipient,[FromForm]string? note,CancellationToken token)
    {
        if(!TryStaffId(out var staffId)) return Forbid();
        try{return Json(new{ok=true,data=await procurement.ApproveQuoteAsync(new(planId,quoteUsd,note,staffId),recipient,token)});}
        catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});}
    }

    [HttpPost("plans/{planId:long}/reject")]
    [ValidateAntiForgeryToken]
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
    public async Task<IActionResult> SaveCandidate([FromForm]SaveCandidateProductCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveCandidateAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }

    [HttpPost("inquiry-records")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInquiry([FromForm]SaveInquiryCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveInquiryAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }

    [HttpPost("sample-records")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSample([FromForm]SaveSampleCommand command,CancellationToken token)
    { try{return Json(new{ok=true,id=await procurement.SaveSampleAsync(command,token)});}catch(InvalidOperationException error){return UnprocessableEntity(new{ok=false,message=error.Message});} }
    private bool TryStaffId(out long staffId) =>
        long.TryParse(User.FindFirst("eggrack_staff_id")?.Value,out staffId);
}
