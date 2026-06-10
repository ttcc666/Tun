using Tun.Contracts.Management;

namespace Tun.Server.Configuration;

public sealed class TunnelServerOptions
{
    public string Token { get; init; } = "dev-token";

    public string? ManagementToken { get; init; }

    public string ConfigPath { get; init; } = "data/tunnels.json";

    public DatabaseOptions Database { get; init; } = new();

    public string BaseDomain { get; init; } = "localhost";

    public bool ValidateHostHeader { get; init; } = true;

    public TunnelForwardedHeadersOptions ForwardedHeaders { get; init; } = new();

    public List<ManagedTunnelConfig> ConfiguredTunnels { get; init; } =
    [
        new()
        {
            TunnelId = "demo",
            ClientId = "dev-client",
            LocalUrl = "http://localhost:5000",
            Enabled = true,
            Description = "Sample app tunnel"
        }
    ];

    public int ChunkSize { get; init; } = 64 * 1024;

    public int RequestTimeoutSeconds { get; init; } = 60;

    public int IdleTimeoutSeconds { get; init; } = 120;

    public string EffectiveManagementToken =>
        string.IsNullOrWhiteSpace(ManagementToken) ? Token : ManagementToken;

    public static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "dashboard", "console",
        "healthz", "health", "status", "metrics",
        "grpc", "ws", "websocket", "cdn", "static"
    };
}

public sealed class TunnelForwardedHeadersOptions
{
    public bool Enabled { get; init; } = true;

    public bool ForwardHost { get; init; } = true;

    public int ForwardLimit { get; init; } = 1;

    public List<string> AllowedHosts { get; init; } = [];
}

public sealed class DatabaseOptions
{
    /// <summary>
    /// 是否启用数据库存储（false 则使用 JSON 文件）
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// PostgreSQL 连接字符串
    /// </summary>
    public string ConnectionString { get; init; } = "Host=localhost;Port=5432;Database=tun;Username=postgres;Password=postgres";
}
