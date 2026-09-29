namespace Eggrack.Operations.Common.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);

public sealed record Result(bool Succeeded, string? Message = null)
{
    public static Result Success(string? message = null) => new(true, message);
    public static Result Failure(string message) => new(false, message);
}

public sealed record Result<T>(bool Succeeded, T? Data = default, string? Message = null)
{
    public static Result<T> Success(T data, string? message = null) => new(true, data, message);
    public static Result<T> Failure(string message) => new(false, default, message);
}