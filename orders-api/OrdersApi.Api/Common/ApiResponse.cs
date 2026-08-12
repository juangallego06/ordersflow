namespace OrdersApi.Api.Common;

public class ApiResponse<T>
{
    public required bool Success { get; init; }
    public required int Code { get; init; }
    public required string Message { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, int code, string message) =>
        new() { Success = true, Code = code, Message = message, Data = data, Error = null };

    public static ApiResponse<T> Fail(int code, string message, string error) =>
        new() { Success = false, Code = code, Message = message, Data = default, Error = error };
}