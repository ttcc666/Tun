using Microsoft.Extensions.Options;
using Tun.Server.Configuration;

namespace Tun.Server.Management;

public static class ManagementAuth
{
    public const string TokenHeader = "X-Tun-Token";

    public static bool IsAuthorized(HttpContext context, IOptions<TunnelServerOptions> options)
    {
        var expected = options.Value.EffectiveManagementToken;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (context.Request.Headers.TryGetValue(TokenHeader, out var header) &&
            string.Equals(header.ToString(), expected, StringComparison.Ordinal))
        {
            return true;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(authorization[bearerPrefix.Length..], expected, StringComparison.Ordinal);
    }
}
