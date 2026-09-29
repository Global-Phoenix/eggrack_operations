using System.Reflection;
using eggrack_operations.Areas.Security;
using eggrack_operations.Areas.Wholesale.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementAuthorizationTests
{
    [Fact]
    public void ControllerRequiresPurchasePlanViewPermission()
    {
        var permission = Assert.Single(typeof(ProcurementController)
            .GetCustomAttributes<InternalPermissionAttribute>());
        Assert.NotNull(permission.Arguments);
        Assert.Equal("wholesale.purchase-plan.view", Assert.Single(permission.Arguments!));
    }

    [Theory]
    [InlineData(nameof(ProcurementController.Create), "wholesale.purchase-plan.create")]
    [InlineData(nameof(ProcurementController.Update), "wholesale.purchase-plan.update")]
    [InlineData(nameof(ProcurementController.SaveCosts), "wholesale.purchase-cost.edit")]
    [InlineData(nameof(ProcurementController.Pricing), "wholesale.purchase-cost.edit")]
    [InlineData(nameof(ProcurementController.CreateSupplier), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.RecordInquiry), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.RecordSample), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SaveCandidate), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SaveInquiry), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SaveSample), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SavePlanItem), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.DeletePlanItem), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SelectInquiry), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.UploadSampleFile), "wholesale.purchase-document.internal")]
    [InlineData(nameof(ProcurementController.SubmitCosts), "wholesale.purchase-quote.submit")]
    [InlineData(nameof(ProcurementController.Review), "wholesale.purchase-quote.review")]
    [InlineData(nameof(ProcurementController.Approve), "wholesale.purchase-quote.final-approve")]
    [InlineData(nameof(ProcurementController.Reject), "wholesale.purchase-quote.review")]
    [InlineData(nameof(ProcurementController.IssueProformaInvoice), "wholesale.purchase-pi.issue")]
    [InlineData(nameof(ProcurementController.CompletePlan), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.UpdateProformaInvoicePricing), "wholesale.purchase-pi.manage")]
    [InlineData(nameof(ProcurementController.SendMailTask), "wholesale.purchase-mail.send")]
    public void WriteActionsRequireSpecificPermission(string methodName,string expectedPermission)
    {
        var method=typeof(ProcurementController).GetMethod(methodName)
            ?? throw new InvalidOperationException(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>());
        var permission=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.NotNull(permission.Arguments);
        Assert.Equal(expectedPermission,Assert.Single(permission.Arguments!));
    }

    [Fact]
    public void MailPreviewRequiresQuoteApprovalPermission()
    {
        var method=typeof(ProcurementController).GetMethod(nameof(ProcurementController.PreviewMailTask))
            ?? throw new InvalidOperationException(nameof(ProcurementController.PreviewMailTask));
        Assert.NotEmpty(method.GetCustomAttributes<HttpGetAttribute>());
        var permission=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.Equal("wholesale.purchase-mail.send",Assert.Single(permission.Arguments!));
    }
}
