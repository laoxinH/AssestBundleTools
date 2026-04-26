using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Common.Results;

/// <summary>
/// 表示带返回载荷的应用层执行结果。
/// </summary>
public sealed class Result<T> : Result
{
    private Result(T value)
        : base(true, false, Array.Empty<AppError>())
    {
        Value = value;
    }

    private Result(bool isCancelled)
        : base(false, isCancelled, Array.Empty<AppError>())
    {
        Value = default;
    }

    private Result(IEnumerable<AppError> errors)
        : base(false, false, errors.ToArray())
    {
        Value = default;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Cancelled() => new(true);

    public static new Result<T> Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(new[] { error });
    }

    public static new Result<T> Failure(IEnumerable<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new Result<T>(errors);
    }
}
