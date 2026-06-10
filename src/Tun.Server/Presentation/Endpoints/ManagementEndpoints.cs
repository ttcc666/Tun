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
        if (!await IsAuthorizedAsync(context, options.Value.ManagementToken))
            return Results.Ok(ApiResponse<object?>.Error(401, "未认证"));

        var result = await service.GetAllAsync();
        if (!result.IsSuccess)
            return Results.Ok(ApiResponse<object?>.Error(400, result.Error));

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

        var data = new
        {
            publicOrigin,
            baseDomain,
            configured,
            online = registry.GetStatuses()
        };

        return Results.Ok(ApiResponse<object>.Success(data));
    }

    private static async Task<IResult> UpsertTunnel(
        HttpContext context,
        UpsertTunnelRequest request,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!await IsAuthorizedAsync(context, options.Value.ManagementToken))
            return Results.Ok(ApiResponse<object?>.Error(401, "未认证"));

        var result = await service.UpsertAsync(request);
        return result.IsSuccess
            ? Results.Ok(ApiResponse<object?>.Success(message: "操作成功"))
            : Results.Ok(ApiResponse<object?>.Error(400, result.Error));
    }

    private static async Task<IResult> DeleteTunnel(
        HttpContext context,
        string tunnelId,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!await IsAuthorizedAsync(context, options.Value.ManagementToken))
            return Results.Ok(ApiResponse<object?>.Error(401, "未认证"));

        var result = await service.DeleteAsync(tunnelId);
        return result.IsSuccess
            ? Results.Ok(ApiResponse<object?>.Success(message: "删除成功"))
            : Results.Ok(ApiResponse<object?>.Error(400, result.Error));
    }

    private static async Task<IResult> GetClientConfig(
        HttpContext context,
        string clientId,
        ITunnelManagementService service,
        IOptions<ServerOptions> options)
    {
        if (!await IsAuthorizedAsync(context, options.Value.Token))
            return Results.Ok(ApiResponse<object?>.Error(401, "未认证"));

        var result = await service.GetAllAsync();
        if (!result.IsSuccess)
            return Results.Ok(ApiResponse<object?>.Error(400, result.Error));

        var clientTunnels = result.Value!
            .Where(t => t.ClientId == clientId && t.Enabled)
            .Select(t => new { tunnelId = t.TunnelId, localUrl = t.LocalUrl })
            .ToArray();

        return Results.Ok(ApiResponse<object>.Success(new { tunnels = clientTunnels }));
    }

    private static async Task<bool> IsAuthorizedAsync(HttpContext context, string expectedToken)
    {
        // Check session authentication
        await context.Session.LoadAsync();
        if (context.Session.GetString("IsAuthenticated") == "true")
            return true;

        // Check token authentication
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
