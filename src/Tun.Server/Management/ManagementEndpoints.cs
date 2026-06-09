using Microsoft.Extensions.Options;
using Tun.Contracts.Management;
using Tun.Server.Configuration;
using Tun.Server.Tunnels;

namespace Tun.Server.Management;

public static class ManagementEndpoints
{
    public static RouteGroupBuilder MapManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/config/tunnels", (
            HttpContext context,
            ManagedTunnelStore store,
            TunnelRegistry registry,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                publicOrigin = GetPublicOrigin(context),
                forwardedHeadersEnabled = options.Value.ForwardedHeaders.Enabled,
                configured = store.GetAll(),
                online = registry.GetStatuses()
            });
        });

        group.MapPost("/config/tunnels", (
            HttpContext context,
            UpsertTunnelConfigRequest request,
            ManagedTunnelStore store,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            try
            {
                var saved = store.Upsert(request);
                return Results.Ok(saved);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError(ex.Message));
            }
        });

        group.MapDelete("/config/tunnels/{tunnelId}", (
            HttpContext context,
            string tunnelId,
            ManagedTunnelStore store,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            return store.Delete(tunnelId)
                ? Results.NoContent()
                : Results.NotFound(new ApiError($"Tunnel '{tunnelId}' was not found."));
        });

        group.MapGet("/client-config/{clientId}", (
            HttpContext context,
            string clientId,
            ManagedTunnelStore store,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            var tunnels = store.GetEnabledForClient(clientId)
                .Select(tunnel => new ClientTunnelConfig
                {
                    TunnelId = tunnel.TunnelId,
                    LocalUrl = tunnel.LocalUrl
                })
                .ToArray();

            return Results.Ok(new ClientTunnelConfigResponse
            {
                ClientId = clientId,
                Tunnels = tunnels
            });
        });

        return group;
    }

    private static string GetPublicOrigin(HttpContext context)
    {
        var host = context.Request.Host.ToUriComponent();
        return string.IsNullOrWhiteSpace(host)
            ? string.Empty
            : $"{context.Request.Scheme}://{host}";
    }
}
