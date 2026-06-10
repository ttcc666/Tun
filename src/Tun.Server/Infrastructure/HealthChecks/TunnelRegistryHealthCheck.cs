using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tun.Server.Tunnels;

namespace Tun.Server.Infrastructure.HealthChecks;

public class TunnelRegistryHealthCheck : IHealthCheck
{
    private readonly TunnelRegistry _registry;

    public TunnelRegistryHealthCheck(TunnelRegistry registry)
    {
        _registry = registry;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var statuses = _registry.GetStatuses();
        var onlineCount = statuses.Count;
        var totalCount = statuses.Count;

        return Task.FromResult(HealthCheckResult.Healthy(
            $"{onlineCount}/{totalCount} tunnels online",
            new Dictionary<string, object>
            {
                ["online"] = onlineCount,
                ["total"] = totalCount
            }
        ));
    }
}
