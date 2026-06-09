using Microsoft.Extensions.Options;
using System.Text.Json;
using Tun.Contracts.Management;
using Tun.Server.Configuration;

namespace Tun.Server.Management;

public sealed class ManagedTunnelStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<ManagedTunnelStore> _logger;
    private List<ManagedTunnelConfig> _tunnels;

    public ManagedTunnelStore(
        IOptions<TunnelServerOptions> options,
        IHostEnvironment environment,
        ILogger<ManagedTunnelStore> logger)
    {
        _logger = logger;
        _path = Path.IsPathRooted(options.Value.ConfigPath)
            ? options.Value.ConfigPath
            : Path.Combine(environment.ContentRootPath, options.Value.ConfigPath);
        _tunnels = LoadOrSeed(options.Value.ConfiguredTunnels);
    }

    public IReadOnlyList<ManagedTunnelConfig> GetAll()
    {
        lock (_gate)
        {
            return _tunnels
                .OrderBy(tunnel => tunnel.ClientId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tunnel => tunnel.TunnelId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyList<ManagedTunnelConfig> GetEnabledForClient(string clientId)
    {
        lock (_gate)
        {
            return _tunnels
                .Where(tunnel => tunnel.Enabled && string.Equals(tunnel.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tunnel => tunnel.TunnelId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public bool TryGet(string tunnelId, out ManagedTunnelConfig tunnel)
    {
        lock (_gate)
        {
            tunnel = _tunnels.FirstOrDefault(item => string.Equals(item.TunnelId, tunnelId, StringComparison.OrdinalIgnoreCase))!;
            return tunnel is not null;
        }
    }

    public ManagedTunnelConfig Upsert(UpsertTunnelConfigRequest request)
    {
        Validate(request);

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var index = _tunnels.FindIndex(tunnel => string.Equals(tunnel.TunnelId, request.TunnelId, StringComparison.OrdinalIgnoreCase));
            var createdAt = index >= 0 ? _tunnels[index].CreatedAt : now;
            var updated = new ManagedTunnelConfig
            {
                TunnelId = request.TunnelId.Trim(),
                ClientId = request.ClientId.Trim(),
                LocalUrl = request.LocalUrl.Trim(),
                Enabled = request.Enabled,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedAt = createdAt,
                UpdatedAt = now
            };

            if (index >= 0)
            {
                _tunnels[index] = updated;
            }
            else
            {
                _tunnels.Add(updated);
            }

            SaveLocked();
            return updated;
        }
    }

    public bool Delete(string tunnelId)
    {
        lock (_gate)
        {
            var removed = _tunnels.RemoveAll(tunnel => string.Equals(tunnel.TunnelId, tunnelId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                SaveLocked();
            }

            return removed;
        }
    }

    private List<ManagedTunnelConfig> LoadOrSeed(IReadOnlyList<ManagedTunnelConfig> seed)
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<List<ManagedTunnelConfig>>(json, JsonOptions) ?? [];
                var normalized = Normalize(loaded);

                if (normalized.Count != loaded.Count)
                {
                    _logger.LogWarning(
                        "Tunnel config {Path} contained duplicate or invalid entries. Normalized from {LoadedCount} to {NormalizedCount} entries.",
                        _path,
                        loaded.Count,
                        normalized.Count);
                    WriteConfig(normalized);
                }

                return normalized;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load tunnel config from {Path}. Falling back to configured defaults.", _path);
        }

        var now = DateTimeOffset.UtcNow;
        var initial = seed.Count == 0
            ? new List<ManagedTunnelConfig>()
            : seed.Select(tunnel => tunnel with
            {
                TunnelId = tunnel.TunnelId.Trim(),
                ClientId = tunnel.ClientId.Trim(),
                LocalUrl = tunnel.LocalUrl.Trim(),
                CreatedAt = tunnel.CreatedAt == default ? now : tunnel.CreatedAt,
                UpdatedAt = tunnel.UpdatedAt == default ? now : tunnel.UpdatedAt
            }).ToList();

        WriteConfig(Normalize(initial));

        return Normalize(initial);
    }

    private void SaveLocked()
    {
        _tunnels = Normalize(_tunnels);
        WriteConfig(_tunnels);
    }

    private void WriteConfig(IReadOnlyList<ManagedTunnelConfig> tunnels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(tunnels, JsonOptions));
    }

    private static List<ManagedTunnelConfig> Normalize(IEnumerable<ManagedTunnelConfig> tunnels)
    {
        var now = DateTimeOffset.UtcNow;
        var byTunnelId = new Dictionary<string, ManagedTunnelConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var tunnel in tunnels)
        {
            if (string.IsNullOrWhiteSpace(tunnel.TunnelId) ||
                string.IsNullOrWhiteSpace(tunnel.ClientId) ||
                string.IsNullOrWhiteSpace(tunnel.LocalUrl) ||
                !Uri.TryCreate(tunnel.LocalUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var tunnelId = tunnel.TunnelId.Trim();
            var normalized = tunnel with
            {
                TunnelId = tunnelId,
                ClientId = tunnel.ClientId.Trim(),
                LocalUrl = tunnel.LocalUrl.Trim(),
                Description = string.IsNullOrWhiteSpace(tunnel.Description) ? null : tunnel.Description.Trim(),
                CreatedAt = tunnel.CreatedAt == default ? now : tunnel.CreatedAt,
                UpdatedAt = tunnel.UpdatedAt == default ? now : tunnel.UpdatedAt
            };

            if (byTunnelId.TryGetValue(tunnelId, out var existing))
            {
                normalized = normalized with { CreatedAt = existing.CreatedAt };
            }

            byTunnelId[tunnelId] = normalized;
        }

        return byTunnelId.Values
            .OrderBy(tunnel => tunnel.ClientId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tunnel => tunnel.TunnelId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Validate(UpsertTunnelConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TunnelId))
        {
            throw new ArgumentException("TunnelId is required.");
        }

        if (request.TunnelId.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')))
        {
            throw new ArgumentException("TunnelId can contain only letters, digits, '-', '_' and '.'.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new ArgumentException("ClientId is required.");
        }

        if (!Uri.TryCreate(request.LocalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("LocalUrl must be an absolute http or https URL.");
        }
    }
}
