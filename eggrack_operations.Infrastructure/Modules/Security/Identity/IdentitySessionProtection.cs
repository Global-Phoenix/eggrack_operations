using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Eggrack.Operations.Infrastructure.Modules.Security.Identity;

internal static class IdentitySessionProtection
{
    private const string ClientClaim = "eggrack:session:client";
    private const string AbsoluteClaim = "eggrack:session:absolute";
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(4);

    public static Task SigningIn(CookieSigningInContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
            return Task.CompletedTask;

        if (!identity.HasClaim(x => x.Type == ClientClaim))
            identity.AddClaim(new Claim(ClientClaim, CreateFingerprint(context.HttpContext)));
        if (!identity.HasClaim(x => x.Type == AbsoluteClaim))
            identity.AddClaim(new Claim(AbsoluteClaim, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        return Task.CompletedTask;
    }

    public static async Task Validate(CookieValidatePrincipalContext context)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        var client = context.Principal.FindFirstValue(ClientClaim);
        var issuedValue = context.Principal.FindFirstValue(AbsoluteClaim);
        var fingerprintMatches = !string.IsNullOrWhiteSpace(client) &&
                                 string.Equals(client, CreateFingerprint(context.HttpContext), StringComparison.Ordinal);
        var withinAbsoluteLifetime = long.TryParse(issuedValue, out var issuedSeconds) &&
            DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedSeconds) <= AbsoluteLifetime;

        if (fingerprintMatches && withinAbsoluteLifetime)
            return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }

    private static string CreateFingerprint(HttpContext context)
    {
        var network = GetNetworkPrefix(context.Connection.RemoteIpAddress);
        var userAgent = context.Request.Headers.UserAgent.ToString().Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{network}|{userAgent}"));
        return Convert.ToHexString(bytes);
    }

    private static string GetNetworkPrefix(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4)
            return $"v4:{bytes[0]}.{bytes[1]}.{bytes[2]}";
        return "v6:" + Convert.ToHexString(bytes.AsSpan(0, 8));
    }
}
