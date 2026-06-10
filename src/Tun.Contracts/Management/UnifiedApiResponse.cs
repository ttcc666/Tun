namespace Tun.Contracts.Management;

public sealed record UnifiedApiResponse<T>
{
    public int Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
}
