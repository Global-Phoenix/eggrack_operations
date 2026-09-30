using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Eggrack.Operations.Domain.Modules.Security;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eggrack.Operations.Tests.Http;

public sealed class AuthenticatedAuthorizationHttpTests
{
    [Theory]
    [InlineData("/wholesale/procurement/requests")]
    [InlineData("/security/staff")]
    [InlineData("/files")]
    public async Task AuthenticatedStaffWithoutRequiredPermissionReceivesForbidden(string path)
    {
        using var factory=CreateFactory();
        using var client=CreateClient(factory);

        var response=await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Fact]
    public async Task ProcurementWriteRequiresActionPermissionBeforeAntiforgeryValidation()
    {
        using var factory=CreateFactory("wholesale.purchase-plan.view");
        using var client=CreateClient(factory);
        using var content=new FormUrlEncodedContent([]);

        var response=await client.PostAsync("/wholesale/procurement/plans/1/quote/submit",content);

        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PermittedWrites))]
    public async Task PermittedWritesStillRequireValidAntiforgeryToken(string path,string[] permissions)
    {
        using var factory=CreateFactory(permissions);
        using var client=CreateClient(factory);
        using var content=new FormUrlEncodedContent([]);

        var response=await client.PostAsync(path,content);

        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }

    public static IEnumerable<object[]> PermittedWrites()
    {
        yield return ["/wholesale/procurement/plans/1/quote/submit",
            new[]{"wholesale.purchase-plan.view","wholesale.purchase-quote.submit"}];
        yield return ["/wholesale/procurement/plans/1/complete-workflow",
            new[]{"wholesale.purchase-plan.view","wholesale.procurement.execute"}];
        yield return ["/wholesale/procurement/plans/1/files/upload",
            new[]{"wholesale.purchase-plan.view","wholesale.purchase-document.internal"}];
        yield return ["/security/staff/status",new[]{"auth.staff.disable"}];
        yield return ["/security/departments/status",new[]{"auth.department.manage"}];
    }

    private static WebApplicationFactory<global::Program> CreateFactory(params string[] permissions) =>
        new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder=>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services=>
            {
                services.RemoveAll<ICurrentStaffAccessor>();
                services.AddSingleton<ICurrentStaffAccessor>(new FixedStaffAccessor(permissions));
                services.AddAuthentication(options=>
                {
                    options.DefaultAuthenticateScheme=TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme=TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme=TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions,TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,_=>{});
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<global::Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect=false,
            BaseAddress=new Uri("https://localhost")
        });

    private sealed class FixedStaffAccessor : ICurrentStaffAccessor
    {
        private readonly StaffAuthorization authorization;

        public FixedStaffAccessor(IEnumerable<string> permissions)
        {
            authorization=new StaffAuthorization(1,"test-staff",1,permissions
                .Select(code=>new PermissionGrant(code,"ALLOW",DataScope.All,null)).ToArray());
        }

        public string GetStaffRef() => authorization.StaffRef;
        public Task<StaffAuthorization?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<StaffAuthorization?>(authorization);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        public const string SchemeName="IntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity=new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier,"test-staff"),
                new Claim(ClaimTypes.Name,"Integration Test"),
                new Claim("eggrack_staff_id","1")
            ],SchemeName);
            var principal=new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal,SchemeName)));
        }
    }
}
