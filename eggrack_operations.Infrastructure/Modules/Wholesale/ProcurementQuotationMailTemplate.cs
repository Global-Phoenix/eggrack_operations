using System.Globalization;
using System.Net;
using System.Text;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed record ProcurementQuotationMailProduct(decimal Quantity,string Unit);

public sealed record ProcurementQuotationMailModel(
    string ContactName,
    string PiNumber,
    string RequestNumber,
    DateTime? ValidUntil,
    decimal TotalAmount,
    string Currency,
    string QuotationUrl,
    string RequestUrl,
    string LogoUrl,
    IReadOnlyList<ProcurementQuotationMailProduct> Products);

public static class ProcurementQuotationMailTemplate
{
    public static string Build(ProcurementQuotationMailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var contactName=Encode(string.IsNullOrWhiteSpace(model.ContactName)?"Customer":model.ContactName.Trim());
        var piNumber=Encode(model.PiNumber);
        var requestNumber=Encode(model.RequestNumber);
        var currency=Encode(model.Currency.ToUpperInvariant());
        var quotationUrl=EncodeUrl(model.QuotationUrl,nameof(model.QuotationUrl));
        var requestUrl=EncodeUrl(model.RequestUrl,nameof(model.RequestUrl));
        var logoUrl=EncodeUrl(model.LogoUrl,nameof(model.LogoUrl));
        var validUntil=model.ValidUntil?.ToString("MMMM d, yyyy",CultureInfo.InvariantCulture)??"—";
        var total=model.TotalAmount.ToString("N2",CultureInfo.InvariantCulture);
        var totalQuantity=FormatTotalQuantity(model.Products);

        var html=new StringBuilder(9000);
        html.Append("<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\"><title>Your Quotation Is Ready</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:#f3f5f7;font-family:Arial,Helvetica,sans-serif;color:#273240;\">")
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;\">Your quotation ").Append(piNumber).Append(" is ready to review.</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;background:#f3f5f7;\"><tr><td align=\"center\" style=\"padding:32px 15px;\">")
            .Append("<table role=\"presentation\" width=\"680\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;max-width:680px;background:#ffffff;border:1px solid #e2e6ea;border-radius:8px;\">")
            .Append("<tr><td style=\"padding:26px 34px;border-bottom:1px solid #edf0f2;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
            .Append("<td><img src=\"").Append(logoUrl).Append("\" alt=\"EggRack\" height=\"36\" style=\"display:block;height:36px;border:0;\"></td>")
            .Append("<td align=\"right\" style=\"font-size:12px;color:#8a939e;\">Quotation</td></tr></table></td></tr>")
            .Append("<tr><td style=\"padding:36px 34px 24px;\"><div style=\"font-size:25px;font-weight:700;color:#18212f;margin-bottom:12px;\">Your Quotation Is Ready</div>")
            .Append("<div style=\"font-size:14px;line-height:1.8;color:#737d89;\">Hello ").Append(contactName).Append(",<br><br>")
            .Append("Thank you for giving us the opportunity to assist with your sourcing request. We are pleased to let you know that the quotation for your purchase request is now ready. Our team has reviewed the requirements you submitted and prepared the quotation based on your request.<br><br>")
            .Append("You can log in to your customer portal to review the quotation details and print or save a PDF copy for your records. If you have any questions or would like to discuss the quotation with us, please feel free to contact us at any time.</div></td></tr>")
            .Append("<tr><td style=\"padding:0 34px 28px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f7faf8;border:1px solid #dcebe2;border-radius:7px;\"><tr><td style=\"padding:20px 22px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">")
            .Append("<tr><td style=\"font-size:11px;color:#89938d;padding-bottom:6px;\">QUOTATION NUMBER</td><td align=\"right\" style=\"font-size:11px;color:#89938d;padding-bottom:6px;\">VALID UNTIL</td></tr>")
            .Append("<tr><td><a href=\"").Append(quotationUrl).Append("\" style=\"font-size:19px;font-weight:700;color:#16794c;text-decoration:none;\">").Append(piNumber).Append("</a></td>")
            .Append("<td align=\"right\" style=\"font-size:14px;font-weight:600;color:#374250;\">").Append(validUntil).Append("</td></tr></table></td></tr></table></td></tr>")
            .Append("<tr><td style=\"padding:0 34px 28px;\"><div style=\"font-size:15px;font-weight:700;color:#293441;padding-bottom:12px;border-bottom:1px solid #edf0f2;\">Quotation Summary</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">")
            .Append(SummaryRow("Based on Purchase Request","<a href=\""+requestUrl+"\" style=\"color:#16794c;text-decoration:none;\">"+requestNumber+"</a>",true))
            .Append(SummaryRow("Products",model.Products.Count.ToString(CultureInfo.InvariantCulture),false))
            .Append(SummaryRow("Total Quantity",Encode(totalQuantity),false))
            .Append("<tr><td style=\"padding:15px 0 4px;font-size:13px;color:#8a939e;\">Total Quotation</td><td align=\"right\" style=\"padding:15px 0 4px;font-size:20px;font-weight:700;color:#16794c;\">").Append(currency).Append(' ').Append(total).Append("</td></tr>")
            .Append("</table></td></tr>")
            .Append("<tr><td style=\"padding:0 34px 30px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#fafbfc;border:1px solid #e7eaee;border-radius:7px;\"><tr><td style=\"padding:18px 20px;\">")
            .Append("<div style=\"font-size:14px;font-weight:700;color:#394451;margin-bottom:8px;\">Review Your Quotation</div><div style=\"font-size:13px;line-height:1.8;color:#7b8490;\">Please review the quotation carefully, including the products, quantities, pricing and other quotation details. You can view the complete quotation online, then print or save it as a PDF for your records.</div>")
            .Append("</td></tr></table></td></tr>")
            .Append("<tr><td align=\"center\" style=\"padding:0 34px 34px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>")
            .Append("<td style=\"padding-right:8px;\"><a href=\"").Append(quotationUrl).Append("\" style=\"display:inline-block;padding:12px 24px;background:#16794c;border-radius:6px;color:#ffffff;text-decoration:none;font-size:14px;font-weight:600;\">View Quotation</a></td>")
            .Append("<td style=\"padding-left:8px;\"><a href=\"").Append(quotationUrl).Append("\" style=\"display:inline-block;padding:12px 24px;background:#ffffff;border:1px solid #d7dde3;border-radius:6px;color:#374250;text-decoration:none;font-size:14px;font-weight:600;\">Print / Save PDF</a></td>")
            .Append("</tr></table></td></tr>")
            .Append("<tr><td style=\"padding:20px 34px;background:#fafbfc;border-top:1px solid #edf0f2;\"><div style=\"font-size:12px;line-height:1.7;color:#929aa4;\">This quotation was prepared based on the purchase request referenced above. Please review it before proceeding.</div></td></tr>")
            .Append("<tr><td style=\"padding:22px 34px;text-align:center;background:#111827;\"><div style=\"font-size:12px;color:#cbd5e1;margin-bottom:7px;\">EggRack Wholesale</div><div style=\"font-size:11px;color:#7f8a98;\">This is an automated message. Please do not reply directly to this email.</div></td></tr>")
            .Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static string SummaryRow(string label,string value,bool valueContainsSafeHtml)
        =>"<tr><td style=\"padding:12px 0;border-bottom:1px solid #f0f2f4;font-size:13px;color:#8a939e;\">"+Encode(label)+"</td><td align=\"right\" style=\"padding:12px 0;border-bottom:1px solid #f0f2f4;font-size:13px;font-weight:600;color:#374250;\">"+(valueContainsSafeHtml?value:Encode(value))+"</td></tr>";

    private static string FormatTotalQuantity(IReadOnlyList<ProcurementQuotationMailProduct> products)
    {
        if(products.Count==0)return "0";
        return string.Join(", ",products
            .GroupBy(product=>string.IsNullOrWhiteSpace(product.Unit)?"units":product.Unit.Trim(),StringComparer.OrdinalIgnoreCase)
            .Select(group=>$"{group.Sum(product=>product.Quantity).ToString("#,##0.###",CultureInfo.InvariantCulture)} {group.Key}"));
    }

    private static string Encode(string? value)=>WebUtility.HtmlEncode(value??string.Empty);

    private static string EncodeUrl(string value,string fieldName)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||(uri.Scheme!=Uri.UriSchemeHttp&&uri.Scheme!=Uri.UriSchemeHttps))
            throw new ArgumentException("Mail URLs must use HTTP or HTTPS.",fieldName);
        return Encode(uri.AbsoluteUri);
    }
}
