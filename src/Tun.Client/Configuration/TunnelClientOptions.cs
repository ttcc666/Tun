namespace Tun.Client.Configuration;

public sealed class TunnelClientOptions
{
    public string ClientId { get; init; } = Environment.MachineName;

    public string ServerUrl { get; init; } = "http://127.0.0.1:8081";

    public string ManagementUrl { get; init; } = "http://127.0.0.1:8080";

    public string Token { get; init; } = "dev-token";

    public bool UseServerConfig { get; init; } = true;

    public bool RequireServerConfig { get; init; }

    public int ChunkSize { get; init; } = 64 * 1024;

    public int InitialReconnectDelaySeconds { get; init; } = 1;

    public int MaxReconnectDelaySeconds { get; init; } = 30;

    public List<TunnelClientRegistration> Tunnels { get; init; } =
    [
        new() { TunnelId = "demo", LocalUrl = "http://localhost:5000" }
    ];
}
