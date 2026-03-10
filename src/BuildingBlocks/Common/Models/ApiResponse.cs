namespace YiPix.BuildingBlocks.Common.Models;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null, int code = 200) => new()
    {
        Code = code,
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null, int code = 400) => new()
    {
        Code = code,
        Success = false,
        Message = message,
        Errors = errors
    };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string? message = null, int code = 200) => new()
    {
        Code = code,
        Success = true,
        Message = message
    };

    public new static ApiResponse Fail(string message, List<string>? errors = null, int code = 400) => new()
    {
        Code = code,
        Success = false,
        Message = message,
        Errors = errors
    };
}
