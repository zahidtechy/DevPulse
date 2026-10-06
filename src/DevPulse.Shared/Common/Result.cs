namespace DevPulse.Shared.Common;

/// <summary>
/// Result pattern for explicit success/failure without exceptions for business rules.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? error, IReadOnlyList<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? (error is null ? [] : [error]);
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    public IReadOnlyList<string> Errors { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string error) => new(false, error);

    public static Result Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new Result(false, list.FirstOrDefault(), list);
    }
}

public sealed class Result<T> : Result
{
    private Result(T value) : base(true, null)
    {
        Value = value;
    }

    private Result(string error) : base(false, error)
    {
        Value = default;
    }

    private Result(IEnumerable<string> errors) : base(false, errors.FirstOrDefault(), errors.ToList())
    {
        Value = default;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(string error) => new(error);

    public new static Result<T> Failure(IEnumerable<string> errors) => new(errors);
}
