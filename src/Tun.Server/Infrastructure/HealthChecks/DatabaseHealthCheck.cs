using Microsoft.Extensions.Diagnostics.HealthChecks;
using SqlSugar;
using Tun.Server.Infrastructure.Persistence.Entities;

namespace Tun.Server.Infrastructure.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ISqlSugarClient? _db;

    public DatabaseHealthCheck(IServiceProvider serviceProvider)
    {
        _db = serviceProvider.GetService<ISqlSugarClient>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_db == null)
            return HealthCheckResult.Healthy("Database disabled");

        try
        {
            await _db.Queryable<TunnelEntity>().Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connected");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
