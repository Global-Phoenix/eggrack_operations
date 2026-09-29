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
    }
}
