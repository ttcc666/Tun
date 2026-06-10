namespace Tun.Server.Application.DTOs;

public record UpsertTunnelRequest
{
    public required string TunnelId { get; init; }
    public required string ClientId { get; init; }
    public required string LocalUrl { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Description { get; init; }
}
