namespace Tun.Server.Domain.Configuration;

public class TunnelOptions
{
    public string ConfigPath { get; set; } = "data/tunnels.json";
    public bool ValidateHostHeader { get; set; } = true;
    public int ChunkSize { get; set; } = 64 * 1024;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int IdleTimeoutSeconds { get; set; } = 120;
    public bool RequireServerConfig { get; set; } = false;
}
