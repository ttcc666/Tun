using Tun.Contracts.Grpc;
using Tun.Server.Domain.Services;

namespace Tun.Server.Tunnels;

public sealed class TunnelRegistry
{
    private readonly object _gate = new();
    private readonly HashSet<TunnelConnection> _connections = [];
    private readonly Dictionary<string, RegisteredTunnel> _tunnels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TunnelValidationService Validator = new();

    public void Register(TunnelConnection connection, IEnumerable<TunnelRegistration> registrations)
    {
        var tunnels = registrations
            .Where(tunnel => !string.IsNullOrWhiteSpace(tunnel.TunnelId) && !string.IsNullOrWhiteSpace(tunnel.LocalUrl))
            .Select(tunnel => (TunnelId: tunnel.TunnelId.Trim(), LocalUrl: tunnel.LocalUrl.Trim()))
            .ToArray();

        foreach (var tunnel in tunnels)
        {
            var validation = Validator.Validate(tunnel.TunnelId, tunnel.LocalUrl);
            if (!validation.IsSuccess)
            {
                throw new ArgumentException(validation.Error);
            }
        }

        connection.SetTunnels(tunnels);

        TunnelConnection[] replacedConnections;

        lock (_gate)
        {
            replacedConnections = _connections
                .Where(existing =>
                    !ReferenceEquals(existing, connection) &&
                    string.Equals(existing.ClientId, connection.ClientId, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var replacedConnection in replacedConnections)
            {
                RemoveTunnelsForConnection(replacedConnection);
                _connections.Remove(replacedConnection);
            }

            _connections.Add(connection);
            RemoveTunnelsForConnection(connection);

            foreach (var tunnel in tunnels)
            {
                _tunnels[tunnel.TunnelId] = new RegisteredTunnel(tunnel.TunnelId, tunnel.LocalUrl, connection);
            }
        }

        foreach (var replacedConnection in replacedConnections)
        {
            replacedConnection.Complete();
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
            RemoveTunnelsForConnection(connection);
            _connections.Remove(connection);
        }
    }

    public TunnelConnection? GetConnectionByClientId(string clientId)
    {
        lock (_gate)
        {
            return _connections.FirstOrDefault(connection =>
                string.Equals(connection.ClientId, clientId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void ReconcileConfiguredTunnels(IEnumerable<(string TunnelId, string ClientId, string LocalUrl)> configuredTunnels)
    {
        var configured = configuredTunnels
            .Where(tunnel =>
                !string.IsNullOrWhiteSpace(tunnel.TunnelId) &&
                !string.IsNullOrWhiteSpace(tunnel.ClientId) &&
                !string.IsNullOrWhiteSpace(tunnel.LocalUrl))
            .ToDictionary(
                tunnel => tunnel.TunnelId.Trim(),
                tunnel => (ClientId: tunnel.ClientId.Trim(), LocalUrl: tunnel.LocalUrl.Trim()),
                StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            foreach (var (tunnelId, registered) in _tunnels.ToArray())
            {
                if (!configured.TryGetValue(tunnelId, out var expected) ||
                    !string.Equals(registered.Connection.ClientId, expected.ClientId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(registered.LocalUrl, expected.LocalUrl, StringComparison.OrdinalIgnoreCase))
                {
                    _tunnels.Remove(tunnelId);
                }
            }
        }
    }

    public int NotifyConfigChanged()
    {
        TunnelConnection[] connections;
        lock (_gate)
        {
            connections = _connections.ToArray();
        }

        foreach (var connection in connections)
        {
            connection.SendConfigUpdateNotification();
        }

        return connections.Length;
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

    private void RemoveTunnelsForConnection(TunnelConnection connection)
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
