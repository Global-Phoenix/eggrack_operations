using eggrack_operations.Areas.Wholesale.Models;
using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementPresentationTests
{
    [Fact]
    public void RequestWithoutPlanIsPresentedAsSubmitted()
    {
        var status = ProcurementUi.RequestStatus(new PurchaseRequestSource());

        Assert.Equal("已提交", status.Text);
        Assert.Equal("info", status.Tone);
    }

    [Theory]
    [InlineData(1, "已提交", "info")]
    [InlineData(2, "采购中", "warning")]
    [InlineData(3, "已报价", "purple")]
    [InlineData(4, "已完成", "success")]
    public void RequestUsesTheExistingFourBusinessStatuses(byte input,string expectedText,string expectedTone)
    {
        var status=ProcurementUi.RequestStatus(new PurchaseRequestSource{RequestStatus=input});

        Assert.Equal(expectedText,status.Text);
        Assert.Equal(expectedTone,status.Tone);
    }

    [Fact]
    public void RelativeTimeUsesTodayAndYesterdayLabels()
    {
        var now=new DateTime(2026,9,29,16,0,0,DateTimeKind.Local);

        Assert.Equal("14:20",ProcurementUi.RelativeTime(new DateTime(2026,9,29,14,20,0,DateTimeKind.Local),now));
        Assert.Equal("昨天 23:10",ProcurementUi.RelativeTime(new DateTime(2026,9,28,23,10,0,DateTimeKind.Local),now));
    }

    [Theory]
    [InlineData("https://example.com/product",true)]
    [InlineData("http://example.com/product",true)]
    [InlineData("javascript:alert(1)",false)]
    [InlineData("/relative/path",false)]
    public void ExternalReferencesOnlyAllowHttpSchemes(string input,bool allowed)
    {
        Assert.Equal(allowed,ProcurementUi.SafeExternalUrl(input) is not null);
    }

    [Theory]
    [InlineData(ProcurementPlanStatus.Draft, "草稿", "neutral")]
    [InlineData(ProcurementPlanStatus.PendingApproval, "审核中", "warning")]
    [InlineData(ProcurementPlanStatus.Approved, "已批准", "purple")]
    [InlineData(ProcurementPlanStatus.EmailPending, "待发送", "warning")]
    [InlineData(ProcurementPlanStatus.Completed, "已完成", "success")]
    [InlineData(ProcurementPlanStatus.Rejected, "已退回", "danger")]
    public void PlanStatusesHaveStableUserFacingLabels(
        ProcurementPlanStatus input,
        string expectedText,
        string expectedTone)
    {
        var status = ProcurementUi.PlanStatus(input);

        Assert.Equal(expectedText, status.Text);
        Assert.Equal(expectedTone, status.Tone);
    }

    [Fact]
    public void VersionChangesSummarizeProductsQuantitiesAndFiles()
    {
        var previous = Version(
            1,
            [Item("纸箱", 10), Item("标签", 20)],
            [Attachment("drawing.pdf")]);
        var current = Version(
            2,
            [Item("纸箱", 15), Item("说明书", 5)],
            [Attachment("drawing.pdf"), Attachment("manual.pdf")]);

        var changes = ProcurementUi.VersionChanges(current, previous);

        Assert.Contains("新增 1 项产品", changes);
        Assert.Contains("移除 1 项产品", changes);
        Assert.Contains("纸箱 数量 10 pcs → 15 pcs", changes);
        Assert.Contains("新增 1 个附件", changes);
    }

    [Fact]
    public void DuplicateProductNamesDoNotBreakVersionComparison()
    {
        var previous = Version(1, [Item("纸箱", 10)], []);
        var current = Version(2, [Item("纸箱", 10), Item("纸箱", 10)], []);

        var changes = ProcurementUi.VersionChanges(current, previous);

        Assert.Equal(["客户资料或产品要求已更新"], changes);
    }

    private static PurchaseRequestVersionDetail Version(
        uint number,
        IReadOnlyList<PurchaseRequestVersionItemDetail> items,
        IReadOnlyList<PurchaseRequestAttachmentDetail> attachments) =>
        new(number, 1, number, "客户", "联系人", "buyer@example.com", null, null, null,
            null, null, null, DateTime.UtcNow, items, attachments);

    private static PurchaseRequestVersionItemDetail Item(string name, decimal quantity) =>
        new() { ProductName = name, Quantity = quantity, Unit = "pcs" };

    private static PurchaseRequestAttachmentDetail Attachment(string name) =>
        new() { OriginalName = name };
}
