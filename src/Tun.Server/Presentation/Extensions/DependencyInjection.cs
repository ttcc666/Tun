using Tun.Server.Domain.Configuration;

namespace Tun.Server.Presentation.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGrpc();

        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromDays(7);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        services.AddOptions<ServerOptions>()
            .Bind(configuration.GetSection("Tun:Server"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Tun:Database"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TunnelOptions>()
            .Bind(configuration.GetSection("Tun:Tunnel"))
            .ValidateOnStart();

        services.AddOptions<Domain.Configuration.ForwardedHeadersOptions>()
            .Bind(configuration.GetSection("Tun:ForwardedHeaders"))
            .ValidateOnStart();

        return services;
    }
}
