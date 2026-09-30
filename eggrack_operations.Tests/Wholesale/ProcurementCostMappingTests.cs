using System.Data;
using System.Reflection;
using Dapper;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementCostMappingTests
{
    [Theory]
    [InlineData(1L, true)]
    [InlineData(0L, false)]
    public void ProductCostMapsMySqlIntegerCurrentFlag(long flag, bool expected)
    {
        var row = Read("ProductCostRow",
            ("PlanItemId", typeof(uint), 1u),
            ("ProductName", typeof(string), "音箱"),
            ("Quantity", typeof(decimal), 3000m),
            ("Unit", typeof(string), "pcs"),
            ("InquiryId", typeof(uint), 7u),
            ("SupplierName", typeof(string), "测试供应商"),
            ("Currency", typeof(string), "CNY"),
            ("UnitPrice", typeof(decimal), 10m),
            ("IsCurrent", typeof(long), flag),
            ("InquiryStatus", typeof(string), "quoted"));

        Assert.Equal(expected, Property<bool>(row, "IsCurrent"));
        Assert.Equal(7u, Property<uint?>(row, "InquiryId"));
        Assert.Equal(10m, Property<decimal?>(row, "UnitPrice"));
        Assert.Equal(3000m, Property<decimal>(row, "Quantity"));
    }

    [Fact]
    public void ProductWithoutSelectedQuoteKeepsNullValues()
    {
        var row = Read("ProductCostRow",
            ("PlanItemId", typeof(uint), 1u),
            ("ProductName", typeof(string), "音箱"),
            ("Quantity", typeof(decimal), 3000m),
            ("Unit", typeof(string), "pcs"),
            ("InquiryId", typeof(uint), DBNull.Value),
            ("SupplierName", typeof(string), DBNull.Value),
            ("Currency", typeof(string), DBNull.Value),
            ("UnitPrice", typeof(decimal), DBNull.Value),
            ("IsCurrent", typeof(long), 0L),
            ("InquiryStatus", typeof(string), DBNull.Value));

        Assert.False(Property<bool>(row, "IsCurrent"));
        Assert.Null(Property<uint?>(row, "InquiryId"));
        Assert.Null(Property<decimal?>(row, "UnitPrice"));
        Assert.Null(Property<string?>(row, "InquiryStatus"));
    }

    [Fact]
    public void CostPreviewMapsIntegerLiteralToDecimal()
    {
        var row = Read("PhaseOnePlan",
            ("Status", typeof(byte), (byte)2),
            ("CnyPerUsd", typeof(decimal), 7.12m),
            ("ProfitRate", typeof(decimal), .15m),
            ("TotalCostCny", typeof(decimal), 30000m),
            ("TotalCostUsd", typeof(decimal), 4213.48m),
            ("SuggestedQuoteUsd", typeof(int), 0));

        Assert.Equal(0m, Property<decimal>(row, "SuggestedQuoteUsd"));
        Assert.Equal(30000m, Property<decimal>(row, "TotalCostCny"));
    }

    [Theory]
    [InlineData(typeof(bool), true)]
    [InlineData(typeof(sbyte), (sbyte)1)]
    [InlineData(typeof(long), 1L)]
    public void InquiryRevisionMapsProviderBooleanRepresentations(Type fieldType, object value)
    {
        var row = Read("InquiryRevision",
            ("Id", typeof(uint), 7u),
            ("PlanItemId", typeof(uint), 1u),
            ("SupplierId", typeof(uint), 2u),
            ("RevisionNo", typeof(uint), 3u),
            ("IsCurrent", fieldType, value));

        Assert.True(Property<bool>(row, "IsCurrent"));
        Assert.Equal(3u, Property<uint>(row, "RevisionNo"));
    }

    [Fact]
    public void InvoiceItemSourceMapsNullableRequestItemAndStringProductKey()
    {
        var row = Read("InvoiceItemSource",
            ("Id", typeof(uint), 11u),
            ("RequestItemId", typeof(uint), DBNull.Value),
            ("ProductKey", typeof(string), "PLAN-0123456789abcdef0123456789abcdef"),
            ("SortOrder", typeof(uint), 1u),
            ("ProductName", typeof(string), "音箱"),
            ("Sku", typeof(string), DBNull.Value),
            ("Brand", typeof(string), DBNull.Value),
            ("Description", typeof(string), "户外音箱"),
            ("Quantity", typeof(decimal), 3000m),
            ("QuantityUnit", typeof(string), "pcs"),
            ("Specifications", typeof(string), DBNull.Value),
            ("Color", typeof(string), DBNull.Value),
            ("Size", typeof(string), DBNull.Value),
            ("PackagingRequirements", typeof(string), DBNull.Value),
            ("CustomizationRequirements", typeof(string), DBNull.Value),
            ("CustomerNote", typeof(string), DBNull.Value));

        Assert.Equal(11u, Property<uint>(row, "Id"));
        Assert.Null(Property<uint?>(row, "RequestItemId"));
        Assert.Equal("PLAN-0123456789abcdef0123456789abcdef", Property<string>(row, "ProductKey"));
        Assert.Equal(3000m, Property<decimal>(row, "Quantity"));
    }

    [Theory]
    [InlineData("PLAN-3b1e8b731a124fd1ba5afa90eda8f24")]
    [InlineData("legacy-product-key")]
    public void InvoiceProductKeyNormalizationProducesStableGuid(string source)
    {
        var method=typeof(ProcurementDataService).GetMethod(
            "NormalizeInvoiceProductKey",BindingFlags.NonPublic|BindingFlags.Static)!;

        var first=(string)method.Invoke(null,[source])!;
        var second=(string)method.Invoke(null,[source])!;

        Assert.True(Guid.TryParse(first,out _));
        Assert.Equal(first,second);
    }

    private static object Read(string name, params (string Name, Type Type, object Value)[] columns)
    {
        // Exercise Dapper itself against the same provider column types as MySQL.
        // Reflection keeps persistence-only row types private to the service.
        var rowType = typeof(ProcurementDataService).GetNestedType(name, BindingFlags.NonPublic)!;
        using var table = new DataTable();
        foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
        table.Rows.Add(columns.Select(column => column.Value).ToArray());
        using var reader = table.CreateDataReader();
        var parser = reader.GetRowParser(rowType);
        Assert.True(reader.Read());
        return parser(reader);
    }

    private static T Property<T>(object row, string name) =>
        (T)row.GetType().GetProperty(name)!.GetValue(row)!;
}
