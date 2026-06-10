using Tun.Server.Domain.Entities;

namespace Tun.Server.Domain.Repositories;

public interface ITunnelRepository
{
    Task<IReadOnlyList<TunnelConfig>> GetAllAsync();
    Task<TunnelConfig?> GetByIdAsync(string tunnelId);
    Task UpsertAsync(TunnelConfig config);
    Task DeleteAsync(string tunnelId);
}
