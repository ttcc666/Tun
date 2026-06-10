using SqlSugar;

namespace Tun.Server.Infrastructure.Persistence.Entities;

[SugarTable("tun_tunnels")]
public sealed class TunnelEntity
{
    [SugarColumn(IsPrimaryKey = true, Length = 100)]
    public string TunnelId { get; set; } = string.Empty;

    [SugarColumn(Length = 100, IsNullable = false)]
    public string ClientId { get; set; } = string.Empty;

    [SugarColumn(Length = 500, IsNullable = false)]
    public string LocalUrl { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; } = true;

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [SugarColumn(IsNullable = false)]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
