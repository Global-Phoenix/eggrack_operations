namespace Eggrack.Operations.Application.Modules.Wholesale;

public sealed record ProcurementFileCategoryOption(string Code,string Name);

public static class ProcurementFileCategories
{
    public const string Product="product";
    public const string Quote="quote";
    public const string Sample="sample";
    public const string Logistics="logistics";
    public const string Other="other";

    public static IReadOnlyList<ProcurementFileCategoryOption> Options { get; } =
    [
        new(Product,"产品资料"),
        new(Quote,"报价资料"),
        new(Sample,"样品资料"),
        new(Logistics,"物流资料"),
        new(Other,"其他")
    ];

    public static string Normalize(string? value)
    {
        var code=(value??string.Empty).Trim().ToLowerInvariant();
        return code switch
        {
            Product or "product_spec" or "specification" or "design" or "promotion" or "marketing" or
                "inspection" or "customer_source" or "customer_original" or "image" or "video" or
                "pdf" or "document" => Product,
            Quote or "supplier_quote" or "internal_quote" or "internal_comparison" or "quotation" => Quote,
            Sample or "sample_photo" or "sample_image" or "sample_video" or "sample_file" => Sample,
            Logistics or "shipping" or "tracking" or "warehouse_logistics" => Logistics,
            _ => Other
        };
    }

    public static string DisplayName(string? value) => Normalize(value) switch
    {
        Product=>"产品资料",
        Quote=>"报价资料",
        Sample=>"样品资料",
        Logistics=>"物流资料",
        _=>"其他"
    };
}
