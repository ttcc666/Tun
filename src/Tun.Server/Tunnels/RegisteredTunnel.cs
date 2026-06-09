namespace Tun.Server.Tunnels;

public sealed record RegisteredTunnel(
    string TunnelId,
    string LocalUrl,
    TunnelConnection Connection);
