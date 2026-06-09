using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Tun.Server.Configuration;
using Tun.Server.Management;
using Tun.Server.Middleware;
using Tun.Server.Tunnels;

var builder = WebApplication.CreateBuilder(args);
var tunOptions = builder.Configuration.GetSection("Tun").Get<TunnelServerOptions>() ?? new TunnelServerOptions();

builder.Services.Configure<TunnelServerOptions>(builder.Configuration.GetSection("Tun"));
if (tunOptions.ForwardedHeaders.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Max(1, tunOptions.ForwardedHeaders.ForwardLimit);

        if (tunOptions.ForwardedHeaders.ForwardHost)
        {
            options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
        }

        if (tunOptions.ForwardedHeaders.AllowedHosts.Count > 0)
        {
            options.AllowedHosts.Clear();
            foreach (var host in tunOptions.ForwardedHeaders.AllowedHosts.Where(host => !string.IsNullOrWhiteSpace(host)))
            {
                options.AllowedHosts.Add(host.Trim());
            }
        }
    });
}

builder.Services.AddGrpc();
builder.Services.AddSingleton<ManagedTunnelStore>();
builder.Services.AddSingleton<TunnelRegistry>();
builder.Services.AddSingleton<TunnelRequestDispatcher>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<TunnelServerOptions>>().Value.ForwardedHeaders.Enabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<HostValidationMiddleware>();

var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles();

app.MapHealthChecks("/healthz");
app.MapGrpcService<TunnelGrpcService>();
app.MapManagementEndpoints();

app.MapGet("/api/tunnels", (TunnelRegistry registry) => Results.Ok(registry.GetStatuses()));

app.MapGet("/", () => Results.Redirect("/dashboard/"));

// Subdomain-based tunnel routing - only handle if it's a subdomain
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    var options = context.RequestServices.GetRequiredService<IOptions<TunnelServerOptions>>().Value;
    var baseDomain = options.BaseDomain;

    // If it's a subdomain, handle as tunnel
    if (host.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(host, baseDomain, StringComparison.OrdinalIgnoreCase))
    {
        var tunnelId = host[..^(baseDomain.Length + 1)];
        var path = context.Request.Path.Value?.TrimStart('/');
        var dispatcher = context.RequestServices.GetRequiredService<TunnelRequestDispatcher>();
        await dispatcher.HandleAsync(context, tunnelId, path);
        return;
    }

    // Otherwise, let other handlers process it
    await next(context);
});

app.Run();

public partial class Program;
