using Eggrack.Operations.Infrastructure.Modules.Files;

namespace Eggrack.Operations.Tests.Files;

public sealed class FileCenterStorageServiceTests
{
    [Fact]
    public void BuildsTestSiteUrlForPublicUFilePath()
    {
        var urls = FileCenterStorageService.BuildPublicCandidates(
            "https://test.eggracks.com/",
            "/u_file/2025-08/3cf49325-5200-4405-818d-0353f3b0bed0.jpg",
            "request");

        Assert.Single(urls);
        Assert.Equal(
            "https://test.eggracks.com/u_file/2025-08/3cf49325-5200-4405-818d-0353f3b0bed0.jpg",
            urls[0].AbsoluteUri);
    }

    [Fact]
    public void UsesProductionHostForRelativePublicPath()
    {
        var urls = FileCenterStorageService.BuildPublicCandidates(
            "https://www.eggracks.com/", "2025-08/example file.pdf", "request");

        Assert.Equal("https://www.eggracks.com/u_file/2025-08/example%20file.pdf", urls[0].AbsoluteUri);
    }

    [Fact]
    public void RejectsInsecurePublicAddressPrefix()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FileCenterStorageService.BuildPublicCandidates(
                "http://example.com/", "u_file/example.jpg", "request"));
    }

    [Fact]
    public void SupportsConfiguredPublicFileHost()
    {
        var urls=FileCenterStorageService.BuildPublicCandidates(
            "https://files.example.com/", "customer/example.jpg", "request");

        Assert.Equal("https://files.example.com/u_file/customer/example.jpg",urls[0].AbsoluteUri);
    }

    [Fact]
    public void BuildsPlainPurchaseRequestAddressFromDatabasePath()
    {
        var urls = FileCenterStorageService.BuildPublicCandidates(
            "https://test.eggracks.com/",
            "/u_file/purchase_requests/2026-09/01234567-89ab-cdef-0123-456789abcdef.png",
            "request");

        Assert.Single(urls);
        Assert.Equal(
            "https://test.eggracks.com/u_file/purchase_requests/2026-09/01234567-89ab-cdef-0123-456789abcdef.png",
            urls[0].AbsoluteUri);
    }
}
