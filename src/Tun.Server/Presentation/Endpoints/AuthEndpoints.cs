using Tun.Server.Application.DTOs;
using Tun.Server.Domain.Services;

namespace Tun.Server.Presentation.Endpoints;

public static class AuthEndpoints
{
    private const string SessionKey = "IsAuthenticated";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapGet("/status", GetStatus);
        group.MapPost("/setup", Setup);
        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout);

        return group;
    }

    private static async Task<IResult> GetStatus(
        HttpContext context,
        ICredentialService credentialService)
    {
        var isInitialized = await credentialService.IsInitializedAsync();

        await context.Session.LoadAsync();
        var isAuthenticated = context.Session.GetString(SessionKey) == "true";

        return Results.Ok(ApiResponse<AuthStatusResponse>.Success(new AuthStatusResponse
        {
            IsInitialized = isInitialized,
            IsAuthenticated = isAuthenticated
        }));
    }

    private static async Task<IResult> Setup(
        HttpContext context,
        SetupRequest request,
        ICredentialService credentialService)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Results.Ok(ApiResponse<object?>.Error(400, "密码至少 8 位"));

        var isInitialized = await credentialService.IsInitializedAsync();
        if (isInitialized)
            return Results.Ok(ApiResponse<object?>.Error(400, "系统已初始化"));

        await credentialService.InitializeAsync(request.Password);

        await context.Session.LoadAsync();
        context.Session.SetString(SessionKey, "true");
        await context.Session.CommitAsync();

        return Results.Ok(ApiResponse<object?>.Success(message: "初始化成功"));
    }

    private static async Task<IResult> Login(
        HttpContext context,
        LoginRequest request,
        ICredentialService credentialService)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return Results.Ok(ApiResponse<object?>.Error(400, "密码不能为空"));

        var isValid = await credentialService.ValidateAsync(request.Password);
        if (!isValid)
            return Results.Ok(ApiResponse<object?>.Error(401, "密码错误"));

        await context.Session.LoadAsync();
        context.Session.SetString(SessionKey, "true");
        await context.Session.CommitAsync();

        return Results.Ok(ApiResponse<object?>.Success(message: "登录成功"));
    }

    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.Session.LoadAsync();
        context.Session.Clear();
        await context.Session.CommitAsync();
        return Results.Ok(ApiResponse<object?>.Success(message: "已登出"));
    }
}
