using FluentAssertions;
using Tun.Contracts.Grpc;
using Tun.Server.Tunnels;

namespace Tun.UnitTests.Server;

public class TunnelRegistryTests
{
    [Fact]
    public void Register_AddsStatus()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client-a");

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }
        });

        var status = registry.GetStatuses().Should().ContainSingle().Subject;
        status.TunnelId.Should().Be("demo");
        status.ClientId.Should().Be("client-a");
        status.LocalUrl.Should().Be("http://localhost:5000");
    }

    [Fact]
    public void Register_ReplacesSameTunnelWithoutOldDisconnectRemovingNewOwner()
    {
        var registry = new TunnelRegistry();
        var first = new TunnelConnection("client-a");
        var second = new TunnelConnection("client-b");

        registry.Register(first, new[]
        {
            new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }
        });
        registry.Register(second, new[]
        {
            new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:6000" }
        });

        registry.Remove(first);

        var status = registry.GetStatuses().Should().ContainSingle().Subject;
        status.ClientId.Should().Be("client-b");
        status.LocalUrl.Should().Be("http://localhost:6000");
    }

    [Fact]
    public void Register_WhenSameConnectionRegistersAgain_ShouldRemovePreviousTunnels()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client1");

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "old", LocalUrl = "http://localhost:5000" }
        });

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "new", LocalUrl = "http://localhost:5001" }
        });

        registry.TryGet("old", out _).Should().BeFalse();
        registry.TryGet("new", out var registered).Should().BeTrue();
        registered.Connection.Should().BeSameAs(connection);
    }

    [Fact]
    public void Register_WhenSameClientReconnects_ShouldReplaceOldConnection()
    {
        var registry = new TunnelRegistry();
        var first = new TunnelConnection("client-a");
        var second = new TunnelConnection("client-a");

        registry.Register(first, new[]
        {
            new TunnelRegistration { TunnelId = "old", LocalUrl = "http://localhost:5000" }
        });

        registry.Register(second, new[]
        {
            new TunnelRegistration { TunnelId = "new", LocalUrl = "http://localhost:6000" }
        });

        registry.Remove(first);

        registry.TryGet("old", out _).Should().BeFalse();
        registry.TryGet("new", out var registered).Should().BeTrue();
        registered.Connection.Should().BeSameAs(second);
        registry.GetConnectionByClientId("client-a").Should().BeSameAs(second);
        registry.NotifyConfigChanged().Should().Be(1);
        first.Outbound.TryRead(out _).Should().BeFalse();
        second.Outbound.TryRead(out var frame).Should().BeTrue();
        frame!.KindCase.Should().Be(TunnelServerFrame.KindOneofCase.ConfigUpdate);
    }

    [Fact]
    public void Remove_ClearsConnectionTunnels()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client-a");

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "demo", LocalUrl = "http://localhost:5000" }
        });

        registry.Remove(connection);

        registry.GetStatuses().Should().BeEmpty();
    }

    [Fact]
    public void ReconcileConfiguredTunnels_WhenRegisteredTunnelWasDeleted_ShouldRemoveItImmediately()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client1");

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "keep", LocalUrl = "http://localhost:5000" },
            new TunnelRegistration { TunnelId = "deleted", LocalUrl = "http://localhost:5001" }
        });

        registry.ReconcileConfiguredTunnels(new[]
        {
            (TunnelId: "keep", ClientId: "client1", LocalUrl: "http://localhost:5000")
        });

        registry.TryGet("keep", out _).Should().BeTrue();
        registry.TryGet("deleted", out _).Should().BeFalse();
    }

    [Fact]
    public void ReconcileConfiguredTunnels_WhenLocalUrlChanged_ShouldRemoveOldRuntimeMapping()
    {
        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client1");

        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "app", LocalUrl = "http://localhost:5000" }
        });

        registry.ReconcileConfiguredTunnels(new[]
        {
            (TunnelId: "app", ClientId: "client1", LocalUrl: "http://localhost:5001")
        });

        registry.TryGet("app", out _).Should().BeFalse();
    }
}
