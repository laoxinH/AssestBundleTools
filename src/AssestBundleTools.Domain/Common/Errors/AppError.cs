namespace AssestBundleTools.Domain.Common.Errors;

/// <summary>
/// 表示可跨层传递的结构化错误信息，面向本地化与统一处理流程。
/// </summary>
public sealed record AppError
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters =
        new Dictionary<string, object?>();

    public AppError(
        string code,
        ErrorCategory category,
        ErrorSeverity severity,
        string messageToken,
        IReadOnlyDictionary<string, object?>? parameters = null,
        bool isUserActionable = true,
        bool isRetryable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageToken);

        Code = code;
        Category = category;
        Severity = severity;
        MessageToken = messageToken;
        Parameters = parameters ?? EmptyParameters;
        IsUserActionable = isUserActionable;
        IsRetryable = isRetryable;
    }

    public string Code { get; }

    public ErrorCategory Category { get; }

    public ErrorSeverity Severity { get; }

    public string MessageToken { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public bool IsUserActionable { get; }

    public bool IsRetryable { get; }
}
