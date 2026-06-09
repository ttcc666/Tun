namespace Tun.Contracts.Management;

public sealed record ManagedTunnelConfig
{
    public string TunnelId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string LocalUrl { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public string? Description { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
