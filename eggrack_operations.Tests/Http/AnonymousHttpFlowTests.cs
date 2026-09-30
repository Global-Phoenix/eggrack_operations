using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Eggrack.Operations.Tests.Http;

public sealed class AnonymousHttpFlowTests : IClassFixture<AnonymousHttpFlowTests.OperationsFactory>
{
    private readonly OperationsFactory factory;

    public AnonymousHttpFlowTests(OperationsFactory factory) => this.factory=factory;

    [Fact]
    public async Task LoginPageLoadsAndContainsAntiforgeryToken()
    {
        using var client=CreateClient();

        var response=await client.GetAsync("/account/login");
        var html=await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("__RequestVerificationToken",html,StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/wholesale/procurement/requests")]
    [InlineData("/wholesale/procurement/plans")]
    [InlineData("/security/staff")]
    [InlineData("/security/departments")]
    [InlineData("/files")]
    [InlineData("/files/content/plan/1")]
    public async Task ProtectedReadRoutesRedirectAnonymousUsersToLogin(string path)
    {
        using var client=CreateClient();

        var response=await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        AssertLoginRedirect(response,path);
    }

    [Theory]
    [InlineData("/wholesale/procurement/plans/1/quote/submit")]
    [InlineData("/wholesale/procurement/plans/1/complete-workflow")]
    [InlineData("/wholesale/procurement/plans/1/files/upload")]
    [InlineData("/security/staff/status")]
    [InlineData("/security/departments/status")]
    public async Task ProtectedWriteRoutesRejectAnonymousUsersBeforeExecuting(string path)
    {
        using var client=CreateClient();
        using var content=new FormUrlEncodedContent([]);

        var response=await client.PostAsync(path,content);

        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        AssertLoginRedirect(response,path);
    }

    [Theory]
    [InlineData("/css/pages/file-center.css","text/css")]
    [InlineData("/js/pages/file-center.js","text/javascript")]
    [InlineData("/js/layout/workspace-tabs.js","text/javascript")]
    public async Task PublicShellAssetsLoadWithoutAuthentication(string path,string expectedMediaType)
    {
        using var client=CreateClient();

        var response=await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(expectedMediaType,response.Content.Headers.ContentType?.MediaType);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect=false,
        BaseAddress=new Uri("https://localhost")
    });

    private static void AssertLoginRedirect(HttpResponseMessage response,string returnUrl)
    {
        var location=Assert.IsType<Uri>(response.Headers.Location);
        var value=location.IsAbsoluteUri?location.PathAndQuery:location.OriginalString;
        Assert.StartsWith("/account/login",value,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReturnUrl=",value,StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Uri.EscapeDataString(returnUrl),value,StringComparison.OrdinalIgnoreCase);
    }

    public sealed class OperationsFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
