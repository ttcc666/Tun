using Microsoft.AspNetCore.HttpOverrides;
using SqlSugar;
using Tun.Server.Domain.Configuration;
using Tun.Server.Infrastructure.HealthChecks;
using Tun.Server.Infrastructure.Persistence.Repositories;
using Tun.Server.Domain.Repositories;

namespace Tun.Server.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration.GetSection("Tun:Database").Get<DatabaseOptions>();

        if (dbOptions?.Enabled == true)
        {
            services.AddScoped<ISqlSugarClient>(sp =>
            {
                var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = dbOptions.ConnectionString,
                    DbType = DbType.PostgreSQL,
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute
                });

                if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    db.Aop.OnLogExecuting = (sql, pars) =>
                        Console.WriteLine($"[SQL] {sql}");
                }

                return db;
            });

            services.AddScoped<ITunnelRepository, DatabaseTunnelRepository>();
        }
        else
        {
            services.AddScoped<ITunnelRepository, JsonTunnelRepository>();
        }

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<TunnelRegistryHealthCheck>("tunnels");

        var fwdOptions = configuration.GetSection("Tun:ForwardedHeaders").Get<Domain.Configuration.ForwardedHeadersOptions>();
        if (fwdOptions?.Enabled == true)
        {
            services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = Math.Max(1, fwdOptions.ForwardLimit);

                if (fwdOptions.ForwardHost)
                    options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;

                if (fwdOptions.AllowedHosts.Count > 0)
                {
                    options.AllowedHosts.Clear();
                    foreach (var host in fwdOptions.AllowedHosts.Where(h => !string.IsNullOrWhiteSpace(h)))
                        options.AllowedHosts.Add(host.Trim());
                }
            });
        }

        return services;
    }
}
