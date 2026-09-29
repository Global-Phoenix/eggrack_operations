using Eggrack.Operations.Application.Modules.Wholesale;

namespace eggrack_operations.Areas.Wholesale.Models;

public sealed record PurchaseRequestsPageViewModel(
    IReadOnlyList<PurchaseRequestSource> Requests);

public sealed record ProcurementPlanEditorPageViewModel(
    PurchaseRequestSource Request,
    ProcurementPlanEditor? Plan,
    IReadOnlyList<ProcurementBuyerOption> Buyers);

public sealed record SourcingPageViewModel(
    IReadOnlyList<ProcurementPlanListItem> Plans,
    uint? InitialPlanId = null,
    string? InitialPlanNumber = null,
    bool CanManageCosts = false,
    bool CanSubmitQuote = false,
    bool CanReviewQuote = false,
    bool CanFinalApprove = false,
    bool CanManagePi = false,
    bool CanIssuePi = false,
    bool CanSendMail = false);

public sealed record SupplierDirectoryPageViewModel(
    IReadOnlyList<SupplierListItem> Suppliers,
    string? Keyword);
