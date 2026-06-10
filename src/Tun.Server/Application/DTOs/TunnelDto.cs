namespace Tun.Server.Application.DTOs;

public record TunnelDto
{
    public required string TunnelId { get; init; }
    public required string ClientId { get; init; }
    public required string LocalUrl { get; init; }
    public bool Enabled { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
