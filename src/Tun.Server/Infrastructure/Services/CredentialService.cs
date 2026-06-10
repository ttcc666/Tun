using System.Text.Json;
using Tun.Server.Domain.Entities;
using Tun.Server.Domain.Services;

namespace Tun.Server.Infrastructure.Services;

public class CredentialService : ICredentialService
{
    private readonly string _credentialsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CredentialService()
    {
        _credentialsPath = Path.Combine("data", "admin.json");
        var directory = Path.GetDirectoryName(_credentialsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task<bool> IsInitializedAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return File.Exists(_credentialsPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InitializeAsync(string password)
    {
        await _lock.WaitAsync();
        try
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var credentials = new AdminCredentials { PasswordHash = passwordHash };
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_credentialsPath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ValidateAsync(string password)
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_credentialsPath))
                return false;

            var json = await File.ReadAllTextAsync(_credentialsPath);
            var credentials = JsonSerializer.Deserialize<AdminCredentials>(json);

            if (credentials == null || string.IsNullOrEmpty(credentials.PasswordHash))
                return false;

            return BCrypt.Net.BCrypt.Verify(password, credentials.PasswordHash);
        }
        finally
        {
            _lock.Release();
        }
    }
}
