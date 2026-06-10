namespace Tun.Server.Domain.Services;

public interface ICredentialService
{
    Task<bool> IsInitializedAsync();
    Task InitializeAsync(string password);
    Task<bool> ValidateAsync(string password);
}
