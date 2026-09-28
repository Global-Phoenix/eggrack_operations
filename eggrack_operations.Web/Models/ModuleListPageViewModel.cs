namespace eggrack_operations.Models;

public sealed record ModuleListColumn(string Key, string Title, string? Width = null);
public sealed record ModuleListRow(string Id, IReadOnlyDictionary<string, string> Values, string Status, string StatusTone = "neutral");
public sealed record ModuleListPageViewModel(
    string Title,
    string Subtitle,
    string ParentTitle,
    string Icon,
    string SearchPlaceholder,
    string PrimaryText,
    IReadOnlyList<string> StatusOptions,
    IReadOnlyList<ModuleListColumn> Columns,
    IReadOnlyList<ModuleListRow> Rows);