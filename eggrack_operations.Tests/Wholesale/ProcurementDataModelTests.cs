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
}
