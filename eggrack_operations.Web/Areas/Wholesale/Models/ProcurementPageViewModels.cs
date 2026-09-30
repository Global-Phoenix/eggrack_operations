using Eggrack.Operations.Application.Modules.Wholesale;

namespace eggrack_operations.Areas.Wholesale.Models;

public sealed record PurchaseRequestsPageViewModel(
    IReadOnlyList<PurchaseRequestSource> Requests);

public sealed record WholesaleOverviewPageViewModel(
    IReadOnlyList<PurchaseRequestSource> Requests,
    IReadOnlyList<ProcurementPlanListItem> Plans,
    ulong? CurrentStaffId);

public sealed record PurchaseRequestDetailsPageViewModel(
    PurchaseRequestSource Request,
    IReadOnlyList<PurchaseRequestVersionDetail> Versions,
    bool CanCreatePlan,
    bool CanUpdatePlan);

public sealed record ProcurementPlanEditorPageViewModel(
    PurchaseRequestSource Request,
    ProcurementPlanEditor? Plan,
    IReadOnlyList<ProcurementBuyerOption> Buyers);

public sealed record PurchasePlanDetailPageViewModel(
    PurchasePlanDetail Plan,
    IReadOnlyList<ProcurementBuyerOption> Buyers,
    bool CanExecuteProcurement,
    bool CanManageCosts,
    bool CanSubmitQuote,
    bool CanFinalApprove,
    bool CanManagePi,
    bool CanIssuePi,
    bool CanManageFiles);

public sealed record PurchasePlanSourcingTabViewModel(
    PurchasePlanDetail Plan,
    PurchasePlanSourcingData Data,
    bool CanExecuteProcurement);

public sealed record PurchasePlanFilesTabViewModel(
    PurchasePlanDetail Plan,
    PurchasePlanFilesData Data,
    bool CanManageFiles);

public sealed record PurchasePlanCostTabViewModel(
    PurchasePlanDetail Plan,
    bool CanExecuteProcurement,
    bool CanManageCosts,
    bool CanSubmitQuote,
    bool CanFinalApprove,
    bool CanManagePi,
    bool CanIssuePi);

public sealed record PurchasePlanActivityTabViewModel(
    IReadOnlyList<ProcurementWorkflowEventItem> Events);

public sealed record ProformaInvoiceDetailPageViewModel(
    ProformaInvoiceDetail Invoice,
    bool CanManagePi,
    bool CanIssuePi);

public sealed record SourcingPageViewModel(
    IReadOnlyList<ProcurementPlanListItem> Plans);

public sealed record SupplierDirectoryPageViewModel(
    IReadOnlyList<SupplierListItem> Suppliers,
    string? Keyword);

public sealed record ProcurementStatusView(string Text,string Tone);

public static class ProcurementUi
{
    public static ProcurementStatusView RequestStatus(PurchaseRequestSource request) =>
        request.RequestStatus switch
        {
            2 => new("采购中","warning"),
            3 => new("已报价","purple"),
            4 => new("已完成","success"),
            _ => new("已提交","info")
        };

    public static ProcurementStatusView PlanStatus(ProcurementPlanStatus status) =>
        status switch
        {
            ProcurementPlanStatus.PendingApproval or ProcurementPlanStatus.PendingFinalApproval => new("审核中","warning"),
            ProcurementPlanStatus.Approved or ProcurementPlanStatus.EmailPending => new("已报价","purple"),
            ProcurementPlanStatus.Completed => new("已完成","success"),
            ProcurementPlanStatus.Rejected => new("已退回","danger"),
            ProcurementPlanStatus.Draft => new("草稿","neutral"),
            _ => new("采购中","warning")
        };

    public static ProcurementStatusView PlanStatus(byte? status) => status.HasValue
        ? PlanStatus((ProcurementPlanStatus)status.Value)
        : new("尚未创建","neutral");

    public static string RelativeTime(DateTime utc,DateTime? nowLocal=null)
    {
        var local=utc.Kind==DateTimeKind.Utc?utc.ToLocalTime():utc;
        var now=nowLocal??DateTime.Now;
        if(local.Date==now.Date)return local.ToString("HH:mm");
        if(local.Date==now.Date.AddDays(-1))return $"昨天 {local:HH:mm}";
        if(local.Year==now.Year)return local.ToString("M月d日 HH:mm");
        return local.ToString("yyyy年M月d日");
    }

    public static string FileTypeLabel(string? fileType,string? mimeType) => (fileType??string.Empty).ToLowerInvariant() switch
    {
        "image" => "图片",
        "video" => "视频",
        "logo" => "Logo",
        "specification" => "规格文件",
        "document" => "文档",
        _ when mimeType?.Equals("application/pdf",StringComparison.OrdinalIgnoreCase)==true => "PDF",
        _ => "其他文件"
    };

    public static string? SafeExternalUrl(string? value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri))return null;
        return string.Equals(uri.Scheme,Uri.UriSchemeHttp,StringComparison.OrdinalIgnoreCase)||
               string.Equals(uri.Scheme,Uri.UriSchemeHttps,StringComparison.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : null;
    }

    public static IReadOnlyList<string> VersionChanges(
        PurchaseRequestVersionDetail current,
        PurchaseRequestVersionDetail? previous)
    {
        if(previous is null)return ["首次提交采购申请"];
        var changes=new List<string>();
        static string ItemKey(PurchaseRequestVersionItemDetail item) =>
            string.IsNullOrWhiteSpace(item.ProductKey)?$"name:{item.ProductName}":item.ProductKey;
        var previousItems=previous.Items.GroupBy(ItemKey,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.OrdinalIgnoreCase);
        var currentItems=current.Items.GroupBy(ItemKey,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.OrdinalIgnoreCase);
        var added=currentItems.Keys.Except(previousItems.Keys,StringComparer.OrdinalIgnoreCase).Count();
        var removed=previousItems.Keys.Except(currentItems.Keys,StringComparer.OrdinalIgnoreCase).Count();
        if(added>0)changes.Add($"新增 {added} 项产品");
        if(removed>0)changes.Add($"移除 {removed} 项产品");
        foreach(var pair in currentItems)
        {
            if(!previousItems.TryGetValue(pair.Key,out var oldItem))continue;
            var item=pair.Value;
            if(oldItem.Quantity!=item.Quantity||!string.Equals(oldItem.Unit,item.Unit,StringComparison.OrdinalIgnoreCase))
                changes.Add($"{item.ProductName} 数量 {oldItem.Quantity:0.###} {oldItem.Unit} → {item.Quantity:0.###} {item.Unit}");
            if(!string.Equals(oldItem.PackagingRequirements,item.PackagingRequirements,StringComparison.Ordinal))
                changes.Add($"{item.ProductName} 包装要求已更新");
            if(!string.Equals(oldItem.CustomizationRequirements,item.CustomizationRequirements,StringComparison.Ordinal))
                changes.Add($"{item.ProductName} 定制要求已更新");
            if(!string.Equals(oldItem.Specifications,item.Specifications,StringComparison.Ordinal)||
               !string.Equals(oldItem.Color,item.Color,StringComparison.Ordinal)||
               !string.Equals(oldItem.Size,item.Size,StringComparison.Ordinal))
                changes.Add($"{item.ProductName} 规格信息已更新");
        }
        if(!string.Equals(previous.Phone,current.Phone,StringComparison.Ordinal)||
           !string.Equals(previous.Whatsapp,current.Whatsapp,StringComparison.Ordinal))
            changes.Add("客户联系方式已更新");
        if(!string.Equals(previous.Country,current.Country,StringComparison.Ordinal)||
           !string.Equals(previous.DeliveryAddress,current.DeliveryAddress,StringComparison.Ordinal)||
           !string.Equals(previous.PickupTradeInfo,current.PickupTradeInfo,StringComparison.Ordinal))
            changes.Add("交付与贸易信息已更新");
        if(!string.Equals(previous.CustomerMessage,current.CustomerMessage,StringComparison.Ordinal))
            changes.Add("客户采购说明已更新");
        var fileDelta=current.Attachments.Count-previous.Attachments.Count;
        if(fileDelta>0)changes.Add($"新增 {fileDelta} 个附件");
        if(fileDelta<0)changes.Add($"减少 {-fileDelta} 个附件");
        if(changes.Count==0)changes.Add("客户资料或产品要求已更新");
        return changes;
    }
}
