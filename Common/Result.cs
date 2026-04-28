namespace SPRMS.Common;

public sealed class Result<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public List<string> Errors { get; init; } = [];
    public DateTime At { get; init; } = DateTime.UtcNow;

    public static Result<T> Ok(T data, string? msg = null) => new() { Success = true, Data = data, Message = msg };
    public static Result<T> Fail(string msg, string? code = null) => new() { Success = false, Message = msg, ErrorCode = code };
    public static Result<T> NotFound(string entity) => new() { Success = false, Message = $"{entity} not found.", ErrorCode = "NOT_FOUND" };
    public static Result<T> Forbidden(string msg = "Access denied.") => new() { Success = false, Message = msg, ErrorCode = "FORBIDDEN" };
    public static Result<T> Conflict(string msg) => new() { Success = false, Message = msg, ErrorCode = "CONFLICT" };
    public static Result<T> ValidationFail(List<string> errs) => new() { Success = false, Message = "Validation failed.", Errors = errs };
}

public sealed class Result
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public List<string> Errors { get; init; } = [];
    public DateTime At { get; init; } = DateTime.UtcNow;

    public static Result Ok(string? msg = null) => new() { Success = true, Message = msg };
    public static Result Fail(string msg, string? code = null) => new() { Success = false, Message = msg, ErrorCode = code };
    public static Result ValidationFail(List<string> errs) => new() { Success = false, Message = "Validation failed.", Errors = errs };
}

