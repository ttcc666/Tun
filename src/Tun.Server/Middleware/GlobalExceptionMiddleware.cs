using System.Text.Json;
using Tun.Server.Application.DTOs;

namespace Tun.Server.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理的异常: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = exception switch
        {
            ArgumentException or ArgumentNullException => 400,
            UnauthorizedAccessException => 401,
            InvalidOperationException when exception.Message.Contains("无权限") => 403,
            _ => 500
        };

        var response = ApiResponse<object?>.Error(code, exception.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
