using Tun.Server.Application.DTOs;
using Tun.Server.Domain.Common;

namespace Tun.Server.Application.Services.Interfaces;

public interface ITunnelManagementService
{
    Task<Result<IReadOnlyList<TunnelDto>>> GetAllAsync();
    Task<Result<TunnelDto>> GetByIdAsync(string tunnelId);
    Task<Result> UpsertAsync(UpsertTunnelRequest request);
    Task<Result> DeleteAsync(string tunnelId);
}
