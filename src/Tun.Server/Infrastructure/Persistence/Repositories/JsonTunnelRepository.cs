using System.Text.Json;
using Tun.Server.Domain.Configuration;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Repositories;

namespace Tun.Server.Infrastructure.Persistence.Repositories;

public sealed class JsonTunnelRepository : ITunnelRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JsonTunnelRepository> _logger;

    public JsonTunnelRepository(TunnelOptions options, ILogger<JsonTunnelRepository> logger)
    {
        _filePath = options.ConfigPath;
        _logger = logger;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task<IReadOnlyList<TunnelConfig>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return Array.Empty<TunnelConfig>();

            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<TunnelConfig>();

            var configs = JsonSerializer.Deserialize<List<TunnelConfig>>(json);
            return configs ?? new List<TunnelConfig>();
        }
        catch (JsonException)
        {
            return Array.Empty<TunnelConfig>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TunnelConfig?> GetByIdAsync(string tunnelId)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(t => t.TunnelId == tunnelId);
    }

    public async Task UpsertAsync(TunnelConfig config)
    {
        await _lock.WaitAsync();
        try
        {
            var all = (await GetAllAsync()).ToList();
            var index = all.FindIndex(t => t.TunnelId == config.TunnelId);

            if (index >= 0)
                all[index] = config with { UpdatedAt = DateTime.UtcNow };
            else
                all.Add(config with { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

            await SaveAllAsync(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string tunnelId)
    {
        await _lock.WaitAsync();
        try
        {
            var all = (await GetAllAsync()).ToList();
            all.RemoveAll(t => t.TunnelId == tunnelId);
            await SaveAllAsync(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAllAsync(List<TunnelConfig> configs)
    {
        var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
