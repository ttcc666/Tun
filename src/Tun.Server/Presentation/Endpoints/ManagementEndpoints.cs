using Microsoft.Extensions.Options;
using Tun.Server.Application.DTOs;
using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Configuration;
using Tun.Server.Tunnels;

namespace Tun.Server.Presentation.Endpoints;

public static class ManagementEndpoints
{
    public static RouteGroupBuilder MapManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/config/tunnels", GetAllTunnels);
        group.MapPost("/config/tunnels", UpsertTunnel);
        group.MapDelete("/config/tunnels/{tunnelId}", DeleteTunnel);

        group.MapGet("/client-config/{clientId}", GetClientConfig);

        return group;
    }

    private static async Task<IResult> GetAllTunnels(
        HttpContext context,
        ITunnelManagementService service,
        TunnelRegistry registry,
        IOptions<ServerOptions> options)
    {
        if (!IsAuthorized(context, options.Value.ManagementToken))
            return Results.Unauthorized();

        var result = await service.GetAllAsync();
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        var publicOrigin = GetPublicOrigin(context);
        var baseDomain = options.Value.BaseDomain;
        var scheme = context.Request.Scheme;

        var configured = result.Value!.Select(t => new
        {
            t.TunnelId,
            t.ClientId,
            t.LocalUrl,
            t.Enabled,
            t.Description,
            t.CreatedAt,
            t.UpdatedAt,
            publicUrl = $"{scheme}://{t.TunnelId}.{baseDomain}"
        }).ToArray();

        return Results.Ok(new
        {
            publicOrigin,
            baseDomain,
            configured,
            online = registry.GetStatuses()
        });
    }

    private static async Task<IResult> UpsertTunnel(
        HttpContext context,
        UpsertTunnelRequest request,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!IsAuthorized(context, options.Value.ManagementToken))
            return Results.Unauthorized();

        var result = await service.UpsertAsync(request);
        return result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> DeleteTunnel(
        HttpContext context,
        string tunnelId,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!IsAuthorized(context, options.Value.ManagementToken))
            return Results.Unauthorized();

        var result = await service.DeleteAsync(tunnelId);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> GetClientConfig(
        HttpContext context,
        string clientId,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!IsAuthorized(context, options.Value.Token))
            return Results.Unauthorized();

        var result = await service.GetAllAsync();
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        var clientTunnels = result.Value!
            .Where(t => t.ClientId == clientId && t.Enabled)
            .Select(t => new { t.TunnelId, t.LocalUrl })
            .ToArray();

        return Results.Ok(new { Tunnels = clientTunnels });
    }

    private static bool IsAuthorized(HttpContext context, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
            return true;

        var token = context.Request.Headers["X-Tun-Token"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(token) && token == expectedToken;
    }

    private static string GetPublicOrigin(HttpContext context)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;
        return $"{scheme}://{host}";
    }
}
