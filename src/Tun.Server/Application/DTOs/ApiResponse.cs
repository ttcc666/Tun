namespace Tun.Server.Application.DTOs;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T? data = default, string message = "操作成功")
    {
        return new ApiResponse<T>
        {
            Code = 200,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<object?> Error(int code, string message)
    {
        return new ApiResponse<object?>
        {
            Code = code,
            Message = message,
            Data = null
        };
    }
}
