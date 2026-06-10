namespace Tun.Server.Application.DTOs;

public class LoginRequest
{
    public string Password { get; set; } = "";
}

public class SetupRequest
{
    public string Password { get; set; } = "";
}

public class AuthStatusResponse
{
    public bool IsInitialized { get; set; }
    public bool IsAuthenticated { get; set; }
}
