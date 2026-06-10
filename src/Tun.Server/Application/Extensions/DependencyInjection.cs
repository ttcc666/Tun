using Tun.Server.Application.Services;
using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Services;

namespace Tun.Server.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITunnelManagementService, TunnelManagementService>();
        services.AddScoped<ITunnelRuntimeService, TunnelRuntimeService>();
        services.AddSingleton<TunnelValidationService>();

        return services;
    }
}
