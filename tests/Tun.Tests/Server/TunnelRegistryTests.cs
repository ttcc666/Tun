using Tun.Contracts.Grpc;
using Tun.Server.Tunnels;

namespace Tun.Tests.Server;

public sealed class TunnelRegistryTests
{
    [Fact]
    public void Register_AddsStatus()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client-a");

        registry.Register(connection, [new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }]);

        var status = Assert.Single(registry.GetStatuses());
        Assert.Equal("demo", status.TunnelId);
        Assert.Equal("client-a", status.ClientId);
        Assert.Equal("http://localhost:5000", status.LocalUrl);
    }

    [Fact]
    public void Register_ReplacesSameTunnelWithoutOldDisconnectRemovingNewOwner()
    {
        var registry = new TunnelRegistry();
        var first = new TunnelConnection("client-a");
        var second = new TunnelConnection("client-b");

        registry.Register(first, [new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }]);
        registry.Register(second, [new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:6000" }]);
        registry.Remove(first);

        var status = Assert.Single(registry.GetStatuses());
        Assert.Equal("client-b", status.ClientId);
        Assert.Equal("http://localhost:6000", status.LocalUrl);
    }

    [Fact]
    public void Remove_ClearsConnectionTunnels()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client-a");

        registry.Register(connection, [new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }]);
        registry.Remove(connection);

        Assert.Empty(registry.GetStatuses());
    }
}
