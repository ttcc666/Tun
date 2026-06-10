using SqlSugar;

namespace Tun.Server.Data;

/// <summary>
/// 隧道配置数据库实体
/// </summary>
[SugarTable("tun_tunnels")]
public sealed class TunnelEntity
{
    /// <summary>
    /// 隧道 ID（主键）
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, Length = 100)]
    public string TunnelId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端 ID
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 本地服务地址
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string LocalUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 描述信息
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
