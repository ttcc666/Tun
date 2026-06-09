namespace Tun.Server.Tunnels;

public sealed record TunnelStatus(
    string TunnelId,
    string ClientId,
    string LocalUrl,
    long RequestCount,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastActivityAt);
