using Tun.Server.Application.DTOs;
using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Common;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Repositories;
using Tun.Server.Domain.Services;

namespace Tun.Server.Application.Services;

public class TunnelManagementService : ITunnelManagementService
{
    private readonly ITunnelRepository _repository;
    private readonly TunnelValidationService _validator;
    private readonly ITunnelRuntimeService _runtime;
    private readonly ILogger<TunnelManagementService> _logger;

    public TunnelManagementService(
        ITunnelRepository repository,
        TunnelValidationService validator,
        ITunnelRuntimeService runtime,
        ILogger<TunnelManagementService> logger)
    {
        _repository = repository;
        _validator = validator;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<TunnelDto>>> GetAllAsync()
    {
        try
        {
            var configs = await _repository.GetAllAsync();
            var dtos = configs.Select(ToDto).ToList();
            return Result<IReadOnlyList<TunnelDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取隧道列表失败");
            return Result<IReadOnlyList<TunnelDto>>.Failure("获取隧道列表失败");
        }
    }

    public async Task<Result<TunnelDto>> GetByIdAsync(string tunnelId)
    {
        try
        {
            var config = await _repository.GetByIdAsync(tunnelId);
            if (config == null)
                return Result<TunnelDto>.Failure("隧道不存在");

            return Result<TunnelDto>.Success(ToDto(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取隧道失败: {TunnelId}", tunnelId);
            return Result<TunnelDto>.Failure("获取隧道失败");
        }
    }

    public async Task<Result> UpsertAsync(UpsertTunnelRequest request)
    {
        _logger.LogInformation(
            "Upserting tunnel {TunnelId} for client {ClientId}",
            request.TunnelId, request.ClientId);

        var validationResult = _validator.Validate(request.TunnelId, request.LocalUrl);
        if (!validationResult.IsSuccess)
        {
            _logger.LogWarning(
                "Tunnel validation failed: {TunnelId}, Error: {Error}",
                request.TunnelId, validationResult.Error);
            return validationResult;
        }

        try
        {
            var config = new TunnelConfig
            {
                TunnelId = request.TunnelId,
                ClientId = request.ClientId,
                LocalUrl = request.LocalUrl,
                Enabled = request.Enabled,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.UpsertAsync(config);
            await _runtime.NotifyConfigChangedAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存隧道配置失败: {TunnelId}", request.TunnelId);
            return Result.Failure("保存隧道配置失败");
        }
    }

    public async Task<Result> DeleteAsync(string tunnelId)
    {
        _logger.LogInformation("Deleting tunnel {TunnelId}", tunnelId);

        try
        {
            var existing = await _repository.GetByIdAsync(tunnelId);
            if (existing == null)
                return Result.Failure("隧道不存在");

            await _repository.DeleteAsync(tunnelId);
            await _runtime.NotifyConfigChangedAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除隧道失败: {TunnelId}", tunnelId);
            return Result.Failure("删除隧道失败");
        }
    }

    private static TunnelDto ToDto(TunnelConfig config) => new()
    {
        TunnelId = config.TunnelId,
        ClientId = config.ClientId,
        LocalUrl = config.LocalUrl,
        Enabled = config.Enabled,
        Description = config.Description,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };
}
