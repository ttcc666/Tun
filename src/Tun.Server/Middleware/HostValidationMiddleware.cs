using Microsoft.Extensions.Options;
using Tun.Server.Domain.Configuration;

namespace Tun.Server.Middleware;

public sealed class HostValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ServerOptions _options;
    private readonly bool _validateHostHeader = true;
    private readonly ILogger<HostValidationMiddleware> _logger;

    public HostValidationMiddleware(
        RequestDelegate next,
        IOptions<ServerOptions> options,
        ILogger<HostValidationMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;

        if (!IsValidHost(host))
        {
            _logger.LogWarning("Invalid Host header rejected: {Host}", host);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid Host header" });
            return;
        }

        await _next(context);
    }

    private bool IsValidHost(string host)
    {
        if (!_validateHostHeader)
        {
            return true;
        }

        // Allow localhost and loopback addresses
        if (IsLocalhostOrLoopback(host))
        {
            return true;
        }

        // Allow root domain
        if (string.Equals(host, _options.BaseDomain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow subdomains
        if (host.EndsWith($".{_options.BaseDomain}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsLocalhostOrLoopback(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
               string.Equals(host, "[::1]", StringComparison.Ordinal);
    }
}
