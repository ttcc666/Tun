using Tun.Contracts.Grpc;
using Tun.Server.Configuration;

namespace Tun.Server.Tunnels;

public sealed class TunnelRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RegisteredTunnel> _tunnels = new(StringComparer.OrdinalIgnoreCase);

    public void Register(TunnelConnection connection, IEnumerable<TunnelRegistration> registrations)
    {
        var tunnels = registrations
            .Where(tunnel => !string.IsNullOrWhiteSpace(tunnel.TunnelId) && !string.IsNullOrWhiteSpace(tunnel.LocalUrl))
            .Select(tunnel => (TunnelId: tunnel.TunnelId.Trim(), LocalUrl: tunnel.LocalUrl.Trim()))
            .ToArray();

        foreach (var tunnel in tunnels)
        {
            if (TunnelServerOptions.ReservedSubdomains.Contains(tunnel.TunnelId))
            {
                throw new ArgumentException($"TunnelId '{tunnel.TunnelId}' is reserved and cannot be used.");
            }
        }

        connection.SetTunnels(tunnels);

        lock (_gate)
        {
            foreach (var tunnel in tunnels)
            {
                _tunnels[tunnel.TunnelId] = new RegisteredTunnel(tunnel.TunnelId, tunnel.LocalUrl, connection);
            }
        }
    }

    public bool TryGet(string tunnelId, out RegisteredTunnel registeredTunnel)
    {
        lock (_gate)
        {
            return _tunnels.TryGetValue(tunnelId, out registeredTunnel!);
        }
    }

    public void Remove(TunnelConnection connection)
    {
        lock (_gate)
        {
            foreach (var tunnelId in _tunnels
                         .Where(pair => ReferenceEquals(pair.Value.Connection, connection))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _tunnels.Remove(tunnelId);
            }
        }
    }

    public IReadOnlyList<TunnelStatus> GetStatuses()
    {
        lock (_gate)
        {
            return _tunnels.Values
                .OrderBy(tunnel => tunnel.TunnelId, StringComparer.OrdinalIgnoreCase)
                .Select(tunnel => new TunnelStatus(
                    tunnel.TunnelId,
                    tunnel.Connection.ClientId,
                    tunnel.LocalUrl,
                    tunnel.Connection.RequestCount,
                    tunnel.Connection.ConnectedAt,
                    tunnel.Connection.LastActivityAt))
                .ToArray();
        }
    }
}
