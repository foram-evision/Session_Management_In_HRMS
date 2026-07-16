namespace HRMS.Application.Common;

/// <summary>
/// Generic API response wrapper for consistent API response format.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> SuccessResult(T data, string message = "Operation successful.")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Failure(string message, List<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new List<string>() };
}
