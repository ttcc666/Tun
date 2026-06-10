namespace Tun.Server.Domain.Configuration;

public class ForwardedHeadersOptions
{
    public bool Enabled { get; set; } = false;
    public int ForwardLimit { get; set; } = 1;
    public bool ForwardHost { get; set; } = false;
    public List<string> AllowedHosts { get; set; } = new();
}
