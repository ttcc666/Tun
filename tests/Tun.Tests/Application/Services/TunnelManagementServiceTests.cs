using FluentAssertions;
using NSubstitute;
using Tun.Server.Application.DTOs;
using Tun.Server.Application.Services;
using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Repositories;
using Tun.Server.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Tun.UnitTests.Application.Services;

public class TunnelManagementServiceTests
{
    private readonly ITunnelRepository _mockRepository;
    private readonly TunnelValidationService _validator;
    private readonly ITunnelRuntimeService _mockRuntime;
    private readonly ILogger<TunnelManagementService> _mockLogger;
    private readonly TunnelManagementService _service;

    public TunnelManagementServiceTests()
    {
        _mockRepository = Substitute.For<ITunnelRepository>();
        _validator = new TunnelValidationService();
        _mockRuntime = Substitute.For<ITunnelRuntimeService>();
        _mockLogger = Substitute.For<ILogger<TunnelManagementService>>();

        _service = new TunnelManagementService(
            _mockRepository,
            _validator,
            _mockRuntime,
            _mockLogger);
    }

    [Fact]
    public async Task GetAllAsync_WhenRepositoryReturnsData_ShouldReturnSuccess()
    {
        var configs = new List<TunnelConfig>
        {
            new() { TunnelId = "test1", ClientId = "client1", LocalUrl = "http://localhost:5000", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { TunnelId = "test2", ClientId = "client2", LocalUrl = "http://localhost:5001", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _mockRepository.GetAllAsync().Returns(configs);

        var result = await _service.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertAsync_WithValidRequest_ShouldReturnSuccess()
    {
        var request = new UpsertTunnelRequest
        {
            TunnelId = "mytunnel",
            ClientId = "client1",
            LocalUrl = "http://localhost:5000",
            Enabled = true
        };

        var result = await _service.UpsertAsync(request);

        result.IsSuccess.Should().BeTrue();
        await _mockRepository.Received(1).UpsertAsync(Arg.Any<TunnelConfig>());
        await _mockRuntime.Received(1).NotifyConfigChangedAsync();
    }

    [Fact]
    public async Task UpsertAsync_WithReservedSubdomain_ShouldReturnFailure()
    {
        var request = new UpsertTunnelRequest
        {
            TunnelId = "admin",
            ClientId = "client1",
            LocalUrl = "http://localhost:5000"
        };

        var result = await _service.UpsertAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("保留子域名");
        await _mockRepository.DidNotReceive().UpsertAsync(Arg.Any<TunnelConfig>());
    }

    [Fact]
    public async Task DeleteAsync_WhenTunnelExists_ShouldReturnSuccess()
    {
        var existingConfig = new TunnelConfig
        {
            TunnelId = "test",
            ClientId = "client1",
            LocalUrl = "http://localhost:5000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockRepository.GetByIdAsync("test").Returns(existingConfig);

        var result = await _service.DeleteAsync("test");

        result.IsSuccess.Should().BeTrue();
        await _mockRepository.Received(1).DeleteAsync("test");
        await _mockRuntime.Received(1).NotifyConfigChangedAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenTunnelNotExists_ShouldReturnFailure()
    {
        _mockRepository.GetByIdAsync("nonexistent").Returns((TunnelConfig?)null);

        var result = await _service.DeleteAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不存在");
        await _mockRepository.DidNotReceive().DeleteAsync(Arg.Any<string>());
    }
}
