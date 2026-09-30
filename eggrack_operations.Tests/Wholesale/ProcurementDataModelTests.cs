using Eggrack.Operations.Application.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementDataModelTests
{
    [Fact]
    public void PurchaseRequestSourceSupportsPropertyBasedMaterialization()
    {
        var constructor = typeof(PurchaseRequestSource).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.PlanId))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.BuyerId))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.PlanTitle))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.TargetCompletionDate))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.ProductCount))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.RequestStatus))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.CreatedAtUtc))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.UpdatedAtUtc))!.CanWrite);
        Assert.True(typeof(PurchaseRequestSource).GetProperty(nameof(PurchaseRequestSource.PlanStatus))!.CanWrite);
        Assert.True(typeof(PurchaseRequestAttachmentDetail).GetProperty(nameof(PurchaseRequestAttachmentDetail.FileType))!.CanWrite);
        Assert.True(typeof(PurchaseRequestAttachmentDetail).GetProperty(nameof(PurchaseRequestAttachmentDetail.UploadedAtUtc))!.CanWrite);
    }

    [Fact]
    public void ProcurementPlanInputOwnsIndependentEditableItems()
    {
        var input = new SaveProcurementPlanInput
        {
            RequestVersionId = 7,
            AssignedBuyerStaffId = 3,
            Priority = "high",
            Items = [new SaveProcurementPlanItemCommand { ProductName = "内部产品", Quantity = 12, Unit = "pcs" }]
        };

        Assert.Single(input.Items);
        Assert.Equal("内部产品", input.Items[0].ProductName);
        Assert.Null(input.Items[0].RequestItemId);
        Assert.True(typeof(ProcurementPlanEditor).GetProperty(nameof(ProcurementPlanEditor.Items))!.CanWrite);
        Assert.Null(typeof(SaveProcurementPlanInput).GetProperty("PlanTitle"));
        Assert.Null(typeof(SaveProcurementPlanInput).GetProperty("PlannedStartDate"));
        Assert.Null(typeof(SaveProcurementPlanInput).GetProperty("TargetCompletionDate"));
        Assert.True(typeof(ProcurementPlanEditor).GetProperty(nameof(ProcurementPlanEditor.CompletedAtUtc))!.CanWrite);
    }

    [Fact]
    public void PurchasePlanQuoteCalculatorIncludesAllCostsAndGrossMargin()
    {
        var result=PurchasePlanQuoteCalculator.Calculate(21_000m,new SavePurchasePlanCostQuoteCommand
        {
            PackagingCostCny=700m,
            SampleCostCny=350m,
            DomesticShippingCny=350m,
            InternationalShippingCny=5_600m,
            OtherCostCny=0m,
            CnyPerUsd=7m,
            ProfitMethod=2,
            ProfitRate=.125m
        });

        Assert.Equal(28_000m,result.TotalCostCny);
        Assert.Equal(4_000m,result.TotalCostUsd);
        Assert.Equal(4_571.43m,result.QuoteUsd);
        Assert.Equal(.125m,result.ActualProfitRate);
        Assert.True(result.ProfitRateInRange);
        Assert.True(result.QuoteAboveMinimum);
    }

    [Fact]
    public void PurchasePlanQuoteCalculatorSupportsMarkupAndOnlyWarnsOutsideGuidance()
    {
        var result=PurchasePlanQuoteCalculator.Calculate(7_000m,new SavePurchasePlanCostQuoteCommand
        {
            CnyPerUsd=7m,
            ProfitMethod=1,
            ProfitRate=.5m
        });

        Assert.Equal(1_000m,result.TotalCostUsd);
        Assert.Equal(1_500m,result.QuoteUsd);
        Assert.False(result.ProfitRateInRange);
        Assert.False(result.QuoteAboveMinimum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PurchasePlanQuoteCalculatorRejectsInvalidExchangeRate(decimal exchangeRate)
    {
        var command=new SavePurchasePlanCostQuoteCommand
        {
            CnyPerUsd=exchangeRate,
            ProfitMethod=2,
            ProfitRate=.15m
        };

        Assert.Throws<ArgumentOutOfRangeException>(()=>PurchasePlanQuoteCalculator.Calculate(100m,command));
    }
}
