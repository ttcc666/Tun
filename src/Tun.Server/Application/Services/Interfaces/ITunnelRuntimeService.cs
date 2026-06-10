namespace Tun.Server.Application.Services.Interfaces;

public interface ITunnelRuntimeService
{
    Task NotifyConfigChangedAsync();
}
