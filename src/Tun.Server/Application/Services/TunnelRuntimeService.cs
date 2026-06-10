using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Repositories;
using Tun.Server.Tunnels;

namespace Tun.Server.Application.Services;

public class TunnelRuntimeService : ITunnelRuntimeService
{
    private readonly ITunnelRepository _repository;
    private readonly TunnelRegistry _registry;
    private readonly ILogger<TunnelRuntimeService> _logger;

    public TunnelRuntimeService(
        ITunnelRepository repository,
        TunnelRegistry registry,
        ILogger<TunnelRuntimeService> logger)
    {
        _repository = repository;
        _registry = registry;
        _logger = logger;
    }

    public async Task NotifyConfigChangedAsync()
    {
        var configs = await _repository.GetAllAsync();
        var enabledTunnels = configs
            .Where(tunnel => tunnel.Enabled)
            .Select(tunnel => (tunnel.TunnelId, tunnel.ClientId, tunnel.LocalUrl));

        _registry.ReconcileConfiguredTunnels(enabledTunnels);
        var notifiedConnections = _registry.NotifyConfigChanged();

        _logger.LogInformation(
            "Notified {ConnectionCount} tunnel client connection(s) about configuration changes.",
            notifiedConnections);
    }
}
