using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Common.Results;

/// <summary>
/// 表示不带返回载荷的应用层执行结果。
/// </summary>
public class Result
{
    private static readonly IReadOnlyList<AppError> EmptyErrors = Array.Empty<AppError>();

    protected Result(bool isSuccess, bool isCancelled, IReadOnlyList<AppError>? errors)
    {
        if (isSuccess && isCancelled)
        {
            throw new ArgumentException("A result cannot be both successful and cancelled.");
        }

        IsSuccess = isSuccess;
        IsCancelled = isCancelled;
        Errors = errors ?? EmptyErrors;

        if (!IsSuccess && !IsCancelled && Errors.Count == 0)
        {
            throw new ArgumentException("A failed result must include at least one error.");
        }

        if ((IsSuccess || IsCancelled) && Errors.Count > 0)
        {
            throw new ArgumentException("Successful or cancelled results cannot include errors.");
        }
    }

    public bool IsSuccess { get; }

    public bool IsCancelled { get; }

    public bool IsFailure => !IsSuccess && !IsCancelled;

    public IReadOnlyList<AppError> Errors { get; }

    public static Result Success() => new(true, false, EmptyErrors);

    public static Result Cancelled() => new(false, true, EmptyErrors);

    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, false, new[] { error });
    }

    public static Result Failure(IEnumerable<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors.Where(static error => error is not null).ToArray();
        if (errorList.Length == 0)
        {
            throw new ArgumentException("A failed result must include at least one error.", nameof(errors));
        }

        return new Result(false, false, errorList);
    }
}
