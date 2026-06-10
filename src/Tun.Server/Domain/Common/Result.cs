namespace Tun.Server.Domain.Common;

public record Result
{
    public bool IsSuccess { get; init; }
    public string Error { get; init; } = "";

    protected Result() { }

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error) => new() { Error = error };
}

public record Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    public static new Result<T> Failure(string error) =>
        new() { Error = error };
}
