namespace Tun.Contracts.Management;

public sealed record ClientTunnelConfigResponse
{
    public string ClientId { get; init; } = string.Empty;

    public IReadOnlyList<ClientTunnelConfig> Tunnels { get; init; } = [];
}
