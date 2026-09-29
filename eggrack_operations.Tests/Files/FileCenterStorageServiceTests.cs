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
    public void RejectsUnapprovedPublicHost()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FileCenterStorageService.BuildPublicCandidates(
                "https://example.com/", "u_file/example.jpg", "request"));
    }

    [Fact]
    public void DoesNotExposeEncryptedRequestBlobAsPublicFile()
    {
        var urls = FileCenterStorageService.BuildPublicCandidates(
            "https://test.eggracks.com/",
            "ab/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.bin",
            "request");

        Assert.Empty(urls);
    }
}