using Eggrack.Operations.Infrastructure.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementQuotationMailTemplateTests
{
    [Fact]
    public void BuildRendersResponsiveQuotationSummaryAndPortalActions()
    {
        var html=ProcurementQuotationMailTemplate.Build(new ProcurementQuotationMailModel(
            "Gavin",
            "PI-260930-0900046532",
            "PR-260930-27571E37650D92CF",
            new DateTime(2026,10,30),
            12000m,
            "usd",
            "https://test.eggracks.com/wholesale/request/PR-260930-27571E37650D92CF/pi",
            "https://test.eggracks.com/wholesale/request/PR-260930-27571E37650D92CF",
            "https://test.eggracks.com/u_file/logo.png",
            [new(3000m,"pcs")]));

        Assert.Contains("Your Quotation Is Ready",html);
        Assert.Contains("PI-260930-0900046532",html);
        Assert.Contains("October 30, 2026",html);
        Assert.Contains("3,000 pcs",html);
        Assert.Contains("USD 12,000.00",html);
        Assert.Contains("View Quotation",html);
        Assert.Contains("Print / Save PDF",html);
        Assert.Contains("https://test.eggracks.com/wholesale/request/PR-260930-27571E37650D92CF/pi",html);
        Assert.Contains("max-width:680px",html);
    }

    [Fact]
    public void BuildEscapesCustomerDataAndGroupsMixedQuantityUnits()
    {
        var html=ProcurementQuotationMailTemplate.Build(new ProcurementQuotationMailModel(
            "<script>alert(1)</script>",
            "PI<&>",
            "PR<&>",
            null,
            1m,
            "usd",
            "https://example.com/pi",
            "https://example.com/request",
            "https://example.com/logo.png",
            [new(2m,"pcs"),new(3m,"PCS"),new(4m,"sets")]));

        Assert.DoesNotContain("<script>",html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;",html);
        Assert.Contains("5 pcs",html);
        Assert.Contains("4 sets",html);
        Assert.Contains("PI&lt;&amp;&gt;",html);
    }
}
