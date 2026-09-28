using Eggrack.Operations.Application.Navigation;

namespace eggrack_operations.Tests;

public class NavigationServiceTests
{
    [Fact]
    public void Main_navigation_contains_required_platform_modules()
    {
        var items = new NavigationService().GetMainNavigation();
        var codes = items.SelectMany(item => item.Children ?? [item]).Select(item => item.Code).ToHashSet();

        Assert.Contains("files", codes);
        Assert.Contains("tasks", codes);
        Assert.Contains("logs", codes);
        Assert.Contains("system.menus", codes);
        Assert.Contains("system.email-templates", codes);
    }
}
