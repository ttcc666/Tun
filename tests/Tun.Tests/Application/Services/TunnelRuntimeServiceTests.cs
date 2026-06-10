using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Tun.Contracts.Grpc;
using Tun.Server.Application.Services;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Repositories;
using Tun.Server.Tunnels;

namespace Tun.UnitTests.Application.Services;

public class TunnelRuntimeServiceTests
{
    [Fact]
    public async Task NotifyConfigChangedAsync_ShouldPruneRemovedRuntimeTunnelsAndNotifyConnections()
    {
        var repository = Substitute.For<ITunnelRepository>();
        repository.GetAllAsync().Returns(new[]
        {
            new TunnelConfig
            {
                TunnelId = "keep",
                ClientId = "client1",
                LocalUrl = "http://localhost:5000",
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TunnelConfig
            {
                TunnelId = "disabled",
                ClientId = "client1",
                LocalUrl = "http://localhost:5001",
                Enabled = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        var registry = new TunnelRegistry();
        var connection = new TunnelConnection("client1");
        registry.Register(connection, new[]
        {
            new TunnelRegistration { TunnelId = "keep", LocalUrl = "http://localhost:5000" },
            new TunnelRegistration { TunnelId = "deleted", LocalUrl = "http://localhost:5001" },
            new TunnelRegistration { TunnelId = "disabled", LocalUrl = "http://localhost:5001" }
        });

        var service = new TunnelRuntimeService(
            repository,
            registry,
            Substitute.For<ILogger<TunnelRuntimeService>>());

        await service.NotifyConfigChangedAsync();

        registry.TryGet("keep", out _).Should().BeTrue();
        registry.TryGet("deleted", out _).Should().BeFalse();
        registry.TryGet("disabled", out _).Should().BeFalse();

        connection.Outbound.TryRead(out var frame).Should().BeTrue();
        frame!.KindCase.Should().Be(TunnelServerFrame.KindOneofCase.ConfigUpdate);
    }
}
