using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Updates.Errors;

public static class ApplicationUpdateErrors
{
    public static AppError NetworkUnavailable(string? detail = null) =>
        Create("Update.Check.NetworkUnavailable", ErrorCategory.ExternalDependency,
            "Error.Update.Check.NetworkUnavailable", detail, isRetryable: true);

    public static AppError RateLimited(string? detail = null) =>
        Create("Update.Check.RateLimited", ErrorCategory.ExternalDependency,
            "Error.Update.Check.RateLimited", detail, isRetryable: true);

    public static AppError SourceUnavailable(string? detail = null) =>
        Create("Update.Check.SourceUnavailable", ErrorCategory.ExternalDependency,
            "Error.Update.Check.SourceUnavailable", detail, isRetryable: true);

    public static AppError InvalidFeed(string? detail = null) =>
        Create("Update.Check.InvalidFeed", ErrorCategory.ParseFailure,
            "Error.Update.Check.InvalidFeed", detail, isRetryable: true);

    public static AppError ArchitectureMismatch(string? detail = null) =>
        Create("Update.Check.ArchitectureMismatch", ErrorCategory.Configuration,
            "Error.Update.Check.ArchitectureMismatch", detail, isRetryable: false);

    public static AppError ChecksumMismatch(string? detail = null) =>
        Create("Update.Download.ChecksumMismatch", ErrorCategory.ParseFailure,
            "Error.Update.Download.ChecksumMismatch", detail, isRetryable: true);

    public static AppError InsufficientDiskSpace(string? detail = null) =>
        Create("Update.Download.InsufficientDiskSpace", ErrorCategory.IoFailure,
            "Error.Update.Download.InsufficientDiskSpace", detail, isRetryable: true);

    public static AppError ActiveOperations() =>
        Create("Update.Apply.ActiveOperations", ErrorCategory.Conflict,
            "Error.Update.Apply.ActiveOperations", detail: null, isRetryable: true);

    public static AppError OtherInstancesRunning() =>
        Create("Update.Apply.OtherInstancesRunning", ErrorCategory.Conflict,
            "Error.Update.Apply.OtherInstancesRunning", detail: null, isRetryable: true);

    public static AppError DirectoryNotWritable(string? detail = null) =>
        Create("Update.Apply.DirectoryNotWritable", ErrorCategory.PermissionDenied,
            "Error.Update.Apply.DirectoryNotWritable", detail, isRetryable: true);

    public static AppError UpdaterLaunchFailed(string? detail = null) =>
        Create("Update.Apply.UpdaterLaunchFailed", ErrorCategory.ExternalDependency,
            "Error.Update.Apply.UpdaterLaunchFailed", detail, isRetryable: true);

    public static AppError CheckFailed(string? detail = null) =>
        Create(
            "Application.Update.CheckFailed",
            ErrorCategory.ExternalDependency,
            "Error.Application.Update.CheckFailed",
            detail,
            isRetryable: true);

    public static AppError DownloadFailed(string? detail = null) =>
        Create(
            "Application.Update.DownloadFailed",
            ErrorCategory.ExternalDependency,
            "Error.Application.Update.DownloadFailed",
            detail,
            isRetryable: true);

    public static AppError ApplyFailed(string? detail = null) =>
        Create(
            "Application.Update.ApplyFailed",
            ErrorCategory.IoFailure,
            "Error.Application.Update.ApplyFailed",
            detail,
            isRetryable: true);

    public static AppError UpdateStateExpired(string targetVersion) =>
        new(
            code: "Application.Update.StateExpired",
            category: ErrorCategory.Conflict,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Application.Update.StateExpired",
            parameters: new Dictionary<string, object?> { ["TargetVersion"] = targetVersion },
            isRetryable: true);

    private static AppError Create(
        string code,
        ErrorCategory category,
        string messageToken,
        string? detail,
        bool isRetryable) =>
        new(
            code,
            category,
            ErrorSeverity.Error,
            messageToken,
            new Dictionary<string, object?> { ["Detail"] = detail },
            isUserActionable: true,
            isRetryable: isRetryable);
}
