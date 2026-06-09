namespace Tun.Contracts.Management;

public sealed record ClientTunnelConfig
{
    public string TunnelId { get; init; } = string.Empty;

    public string LocalUrl { get; init; } = string.Empty;
}
