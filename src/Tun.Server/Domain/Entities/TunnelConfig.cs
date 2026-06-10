namespace Tun.Server.Domain.Entities;

public record TunnelConfig
{
    public required string TunnelId { get; init; }
    public required string ClientId { get; init; }
    public required string LocalUrl { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
