using Eggrack.Operations.Application.Navigation;

namespace eggrack_operations.Tests;

public class NavigationServiceTests
{
    [Fact]
    public void Main_navigation_contains_only_implemented_modules()
    {
        var items = new NavigationService().GetMainNavigation();
        var codes = items.SelectMany(item => item.Children ?? [item]).Select(item => item.Code).ToHashSet();

        Assert.Contains("files", codes);
        Assert.Contains("wholesale.purchase-requests", codes);
        Assert.Contains("wholesale.sourcing", codes);
        Assert.DoesNotContain("wholesale.purchase-plans", codes);
        Assert.Contains("system.users", codes);
        Assert.Contains("system.roles", codes);
        Assert.DoesNotContain("tasks", codes);
        Assert.DoesNotContain("logs", codes);
        Assert.DoesNotContain("system.menus", codes);
        Assert.DoesNotContain("system.email-templates", codes);
    }
}
