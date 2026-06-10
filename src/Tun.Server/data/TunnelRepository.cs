using SqlSugar;
using Tun.Contracts.Management;

namespace Tun.Server.Data;

/// <summary>
/// 隧道配置数据库仓储
/// </summary>
public sealed class TunnelRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<TunnelRepository> _logger;

    public TunnelRepository(ISqlSugarClient db, ILogger<TunnelRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 初始化数据库表结构
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _db.DbMaintenance.CreateDatabase();
            _db.CodeFirst.InitTables<TunnelEntity>();
            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
    }

    /// <summary>
    /// 查询所有隧道配置
    /// </summary>
    public async Task<List<ManagedTunnelConfig>> GetAllAsync()
    {
        var entities = await _db.Queryable<TunnelEntity>()
            .OrderBy(t => t.ClientId)
            .OrderBy(t => t.TunnelId)
            .ToListAsync();

        return entities.Select(ToConfig).ToList();
    }

    /// <summary>
    /// 查询指定客户端的已启用隧道
    /// </summary>
    public async Task<List<ManagedTunnelConfig>> GetEnabledByClientAsync(string clientId)
    {
        var entities = await _db.Queryable<TunnelEntity>()
            .Where(t => t.Enabled && t.ClientId == clientId)
            .OrderBy(t => t.TunnelId)
            .ToListAsync();

        return entities.Select(ToConfig).ToList();
    }

    /// <summary>
    /// 根据 TunnelId 查询单个配置
    /// </summary>
    public async Task<ManagedTunnelConfig?> GetByIdAsync(string tunnelId)
    {
        var entity = await _db.Queryable<TunnelEntity>()
            .FirstAsync(t => t.TunnelId == tunnelId);

        return entity == null ? null : ToConfig(entity);
    }

    /// <summary>
    /// 插入或更新隧道配置
    /// </summary>
    public async Task<ManagedTunnelConfig> UpsertAsync(ManagedTunnelConfig config)
    {
        var entity = ToEntity(config);
        var existing = await _db.Queryable<TunnelEntity>()
            .FirstAsync(t => t.TunnelId == config.TunnelId);

        if (existing != null)
        {
            // 更新时保留创建时间
            entity.CreatedAt = existing.CreatedAt;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.Updateable(entity).ExecuteCommandAsync();
        }
        else
        {
            // 插入新记录
            entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.Insertable(entity).ExecuteCommandAsync();
        }

        return ToConfig(entity);
    }

    /// <summary>
    /// 删除隧道配置
    /// </summary>
    public async Task<bool> DeleteAsync(string tunnelId)
    {
        var count = await _db.Deleteable<TunnelEntity>()
            .Where(t => t.TunnelId == tunnelId)
            .ExecuteCommandAsync();

        return count > 0;
    }

    /// <summary>
    /// 批量插入（用于数据迁移）
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<ManagedTunnelConfig> configs)
    {
        var entities = configs.Select(ToEntity).ToList();
        return await _db.Insertable(entities).ExecuteCommandAsync();
    }

    private static TunnelEntity ToEntity(ManagedTunnelConfig config) => new()
    {
        TunnelId = config.TunnelId,
        ClientId = config.ClientId,
        LocalUrl = config.LocalUrl,
        Enabled = config.Enabled,
        Description = config.Description,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };

    private static ManagedTunnelConfig ToConfig(TunnelEntity entity) => new()
    {
        TunnelId = entity.TunnelId,
        ClientId = entity.ClientId,
        LocalUrl = entity.LocalUrl,
        Enabled = entity.Enabled,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
