using Tun.Server.Domain.Common;

namespace Tun.Server.Domain.Services;

public class TunnelValidationService
{
    private static readonly HashSet<string> ReservedSubdomains = new()
    {
        "www", "api", "admin", "dashboard", "console",
        "healthz", "health", "status", "metrics",
        "grpc", "ws", "websocket", "cdn", "static"
    };

    public Result Validate(string tunnelId, string localUrl)
    {
        if (string.IsNullOrWhiteSpace(tunnelId))
            return Result.Failure("隧道ID不能为空");

        if (ReservedSubdomains.Contains(tunnelId.ToLowerInvariant()))
            return Result.Failure($"隧道ID '{tunnelId}' 是保留子域名");

        if (string.IsNullOrWhiteSpace(localUrl))
            return Result.Failure("LocalUrl不能为空");

        if (!Uri.TryCreate(localUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return Result.Failure($"LocalUrl '{localUrl}' 格式无效,必须是 http 或 https");

        return Result.Success();
    }
}
