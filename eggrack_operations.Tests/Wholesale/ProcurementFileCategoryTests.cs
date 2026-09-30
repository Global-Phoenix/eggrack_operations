using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementFileCategoryTests
{
    [Theory]
    [InlineData("product_spec",ProcurementFileCategories.Product,"产品资料")]
    [InlineData("promotion",ProcurementFileCategories.Product,"产品资料")]
    [InlineData("inspection",ProcurementFileCategories.Product,"产品资料")]
    [InlineData("supplier_quote",ProcurementFileCategories.Quote,"报价资料")]
    [InlineData("internal_comparison",ProcurementFileCategories.Quote,"报价资料")]
    [InlineData("sample_photo",ProcurementFileCategories.Sample,"样品资料")]
    [InlineData("sample_video",ProcurementFileCategories.Sample,"样品资料")]
    [InlineData("logistics",ProcurementFileCategories.Logistics,"物流资料")]
    [InlineData("customer_output",ProcurementFileCategories.Other,"其他")]
    [InlineData("unknown_legacy_value",ProcurementFileCategories.Other,"其他")]
    public void LegacyTypesMapToFiveBusinessCategories(string input,string expectedCode,string expectedName)
    {
        Assert.Equal(expectedCode,ProcurementFileCategories.Normalize(input));
        Assert.Equal(expectedName,ProcurementFileCategories.DisplayName(input));
    }

    [Fact]
    public void UploadOptionsContainOnlyTheFiveBusinessCategories()
    {
        Assert.Equal(
            [ProcurementFileCategories.Product,ProcurementFileCategories.Quote,ProcurementFileCategories.Sample,
                ProcurementFileCategories.Logistics,ProcurementFileCategories.Other],
            ProcurementFileCategories.Options.Select(option=>option.Code));
    }
}
