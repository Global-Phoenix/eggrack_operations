using Eggrack.Operations.Application.Modules.Wholesale;

namespace eggrack_operations.Models;

public sealed record OperationsOverviewPageViewModel(
    string DisplayName,
    bool CanViewWholesale,
    IReadOnlyList<PurchaseRequestSource> Requests,
    IReadOnlyList<ProcurementPlanListItem> Plans,
    ulong? CurrentStaffId,
    bool CanCreatePlan,
    bool CanUpdatePlan,
    bool CanReviewQuote,
    bool CanFinalApprove);
