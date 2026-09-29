using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Eggrack.Operations.Web.Areas.Files.Services;

public static class XlsxPreviewRenderer
{
    private const int MaximumRows = 200;
    private const int MaximumColumns = 30;
    private const long MaximumXmlBytes = 20L * 1024 * 1024;

    public static string Render(Stream source)
    {
        if (!source.CanSeek)
        {
            var copy = new MemoryStream();
            source.CopyTo(copy);
            copy.Position = 0;
            source = copy;
        }
        else source.Position = 0;

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var shared = ReadSharedStrings(archive);
        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidDataException("XLSX 中没有可预览的工作表。");
        EnsureSafeEntry(worksheet);
        using var sheetStream = worksheet.Open();
        var document = XDocument.Load(sheetStream, LoadOptions.None);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<SortedDictionary<int, string>>();
        var largestColumn = 0;

        foreach (var row in document.Descendants(ns + "row").Take(MaximumRows))
        {
            var values = new SortedDictionary<int, string>();
            var sequentialColumn = 0;
            foreach (var cell in row.Elements(ns + "c"))
            {
                var column = ColumnIndex((string?)cell.Attribute("r")) ?? sequentialColumn;
                sequentialColumn = column + 1;
                if (column >= MaximumColumns) continue;
                values[column] = CellText(cell, ns, shared);
                largestColumn = Math.Max(largestColumn, column);
            }
            rows.Add(values);
        }

        var columnCount = Math.Max(1, largestColumn + 1);
        var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>body{margin:0;background:#fff;color:#334155;font:12px system-ui,-apple-system,Segoe UI,sans-serif}.note{position:sticky;top:0;padding:8px 10px;border-bottom:1px solid #e5e7eb;background:#f8fafc;color:#64748b}table{border-collapse:collapse;min-width:100%}th,td{max-width:260px;padding:7px 9px;border:1px solid #e5e7eb;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}th{position:sticky;top:33px;background:#f8fafc;color:#64748b;text-align:center}tbody th{left:0;z-index:1}td:hover{white-space:normal;overflow:visible;background:#eff6ff}</style></head><body><div class=\"note\">工作表预览 · 最多显示前 200 行、30 列</div><table><thead><tr><th>#</th>");
        for (var column = 0; column < columnCount; column++) html.Append("<th>").Append(ColumnName(column)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            html.Append("<tr><th>").Append(rowIndex + 1).Append("</th>");
            for (var column = 0; column < columnCount; column++)
            {
                rows[rowIndex].TryGetValue(column, out var value);
                html.Append("<td>").Append(WebUtility.HtmlEncode(value ?? string.Empty)).Append("</td>");
            }
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></body></html>").ToString();
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        EnsureSafeEntry(entry);
        using var stream = entry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string CellText(XElement cell, XNamespace ns, IReadOnlyList<string> shared)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr") return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value));
        var value = cell.Element(ns + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(value, out var index) && index >= 0 && index < shared.Count) return shared[index];
        if (type == "b") return value == "1" ? "是" : "否";
        return value;
    }

    private static int? ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        var value = 0; var letters = 0;
        foreach (var character in reference)
        {
            if (!char.IsLetter(character)) break;
            value = checked(value * 26 + char.ToUpperInvariant(character) - 'A' + 1);
            letters++;
        }
        return letters == 0 ? null : value - 1;
    }

    private static string ColumnName(int index)
    {
        var value = index + 1; var result = string.Empty;
        while (value > 0) { value--; result = (char)('A' + value % 26) + result; value /= 26; }
        return result;
    }

    private static void EnsureSafeEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumXmlBytes) throw new InvalidDataException("XLSX 工作表过大，无法在线预览。");
    }
}