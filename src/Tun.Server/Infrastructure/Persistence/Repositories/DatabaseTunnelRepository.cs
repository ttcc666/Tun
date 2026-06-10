using SqlSugar;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Repositories;
using Tun.Server.Infrastructure.Persistence.Entities;

namespace Tun.Server.Infrastructure.Persistence.Repositories;

public sealed class DatabaseTunnelRepository : ITunnelRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<DatabaseTunnelRepository> _logger;

    public DatabaseTunnelRepository(ISqlSugarClient db, ILogger<DatabaseTunnelRepository> logger)
    {
        _db = db;
        _logger = logger;
        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _db.DbMaintenance.CreateDatabase();
            _db.CodeFirst.InitTables<TunnelEntity>();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public async Task<IReadOnlyList<TunnelConfig>> GetAllAsync()
    {
        var entities = await _db.Queryable<TunnelEntity>()
            .OrderBy(t => t.ClientId)
            .OrderBy(t => t.TunnelId)
            .ToListAsync();

        return entities.Select(ToConfig).ToList();
    }

    public async Task<TunnelConfig?> GetByIdAsync(string tunnelId)
    {
        var entity = await _db.Queryable<TunnelEntity>()
            .FirstAsync(t => t.TunnelId == tunnelId);

        return entity == null ? null : ToConfig(entity);
    }

    public async Task UpsertAsync(TunnelConfig config)
    {
        var entity = ToEntity(config);
        var existing = await _db.Queryable<TunnelEntity>()
            .FirstAsync(t => t.TunnelId == config.TunnelId);

        if (existing != null)
        {
            entity.CreatedAt = existing.CreatedAt;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.Updateable(entity).ExecuteCommandAsync();
        }
        else
        {
            entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.Insertable(entity).ExecuteCommandAsync();
        }
    }

    public async Task DeleteAsync(string tunnelId)
    {
        await _db.Deleteable<TunnelEntity>()
            .Where(t => t.TunnelId == tunnelId)
            .ExecuteCommandAsync();
    }

    private static TunnelEntity ToEntity(TunnelConfig config) => new()
    {
        TunnelId = config.TunnelId,
        ClientId = config.ClientId,
        LocalUrl = config.LocalUrl,
        Enabled = config.Enabled,
        Description = config.Description,
        CreatedAt = config.CreatedAt.ToUniversalTime(),
        UpdatedAt = config.UpdatedAt.ToUniversalTime()
    };

    private static TunnelConfig ToConfig(TunnelEntity entity) => new()
    {
        TunnelId = entity.TunnelId,
        ClientId = entity.ClientId,
        LocalUrl = entity.LocalUrl,
        Enabled = entity.Enabled,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt.UtcDateTime,
        UpdatedAt = entity.UpdatedAt.UtcDateTime
    };
}
