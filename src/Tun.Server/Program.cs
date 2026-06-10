using Microsoft.Extensions.Options;
using Tun.Server.Application.Extensions;
using Tun.Server.Domain.Configuration;
using Tun.Server.Infrastructure.Extensions;
using Tun.Server.Middleware;
using Tun.Server.Presentation.Endpoints;
using Tun.Server.Presentation.Extensions;
using Tun.Server.Tunnels;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPresentation(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<TunnelRegistry>();
builder.Services.AddSingleton<TunnelRequestDispatcher>();

var app = builder.Build();

var fwdOptions = app.Services.GetRequiredService<IOptions<Tun.Server.Domain.Configuration.ForwardedHeadersOptions>>().Value;
if (fwdOptions.Enabled)
    app.UseForwardedHeaders();

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

app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    var options = context.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;
    var baseDomain = options.BaseDomain;

    if (host.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(host, baseDomain, StringComparison.OrdinalIgnoreCase))
    {
        var tunnelId = host[..^(baseDomain.Length + 1)];
        var path = context.Request.Path.Value?.TrimStart('/');
        var dispatcher = context.RequestServices.GetRequiredService<TunnelRequestDispatcher>();
        await dispatcher.HandleAsync(context, tunnelId, path);
        return;
    }

    await next(context);
});

app.Run();

public partial class Program;
