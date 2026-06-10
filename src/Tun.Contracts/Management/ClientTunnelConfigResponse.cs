namespace Tun.Contracts.Management;

public sealed record ClientTunnelConfigResponse
{
    public IReadOnlyList<ClientTunnelConfig> Tunnels { get; init; } = [];
}
