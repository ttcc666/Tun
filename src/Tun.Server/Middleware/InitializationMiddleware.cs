using Tun.Server.Domain.Services;

namespace Tun.Server.Middleware;

public class InitializationMiddleware
{
    private readonly RequestDelegate _next;

    public InitializationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICredentialService credentialService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Allow static files and auth endpoints
        if (path.StartsWith("/api/auth/setup") ||
            path.StartsWith("/api/auth/status") ||
            path.StartsWith("/dashboard/") ||
            path == "/dashboard" ||
            path == "/")
        {
            await _next(context);
            return;
        }

        var isInitialized = await credentialService.IsInitializedAsync();
        if (!isInitialized)
        {
            context.Response.StatusCode = 503;
            context.Response.Headers["X-Initialization-Required"] = "true";
            await context.Response.WriteAsJsonAsync(new { needsSetup = true });
            return;
        }

        await _next(context);
    }
}
