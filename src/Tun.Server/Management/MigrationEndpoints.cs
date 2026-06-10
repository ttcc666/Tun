using Microsoft.Extensions.Options;
using Tun.Server.Configuration;
using Tun.Server.Data;

namespace Tun.Server.Management;

/// <summary>
/// 数据迁移相关的管理端点
/// </summary>
public static class MigrationEndpoints
{
    /// <summary>
    /// 注册迁移相关的 API 端点
    /// </summary>
    public static void MapMigrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/migration");

        // POST /api/migration/from-json - 从 JSON 迁移到数据库
        group.MapPost("/from-json", async (
            HttpContext context,
            DataMigrationService migrationService,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            var result = await migrationService.MigrateFromJsonAsync();
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("MigrateFromJson")
        .WithTags("Migration");

        // POST /api/migration/to-json - 从数据库导出到 JSON
        group.MapPost("/to-json", async (
            HttpContext context,
            DataMigrationService migrationService,
            IOptions<TunnelServerOptions> options) =>
        {
            if (!ManagementAuth.IsAuthorized(context, options))
            {
                return Results.Unauthorized();
            }

            var result = await migrationService.ExportToJsonAsync();
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ExportToJson")
        .WithTags("Migration");
    }
}
