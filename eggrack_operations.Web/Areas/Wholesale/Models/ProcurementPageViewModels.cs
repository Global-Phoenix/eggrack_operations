using Eggrack.Operations.Application.Modules.Wholesale;

namespace eggrack_operations.Areas.Wholesale.Models;

public sealed record PurchaseRequestsPageViewModel(
    IReadOnlyList<PurchaseRequestSource> Requests,
    IReadOnlyList<ProcurementBuyerOption> Buyers);

public sealed record SourcingPageViewModel(
    IReadOnlyList<ProcurementPlanListItem> Plans,
    uint? InitialPlanId = null,
    string? InitialPlanNumber = null);
