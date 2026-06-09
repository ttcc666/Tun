using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tun.Client.Configuration;
using Tun.Client.Tunnels;

namespace Tun.Tests.Client;

public sealed class LocalTunnelForwarderTests
{
    [Fact]
    public void Constructor_AllowsDuplicateTunnelIds()
    {
        var options = Options.Create(new TunnelClientOptions
        {
            Tunnels =
            [
                new() { TunnelId = "demo", LocalUrl = "http://localhost:5000" },
                new() { TunnelId = "demo", LocalUrl = "http://localhost:5001" }
            ]
        });

        var exception = Record.Exception(() =>
            new LocalTunnelForwarder(options, NullLogger<LocalTunnelForwarder>.Instance));

        Assert.Null(exception);
    }
}
