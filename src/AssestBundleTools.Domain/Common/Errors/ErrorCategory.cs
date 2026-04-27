namespace AssestBundleTools.Domain.Common.Errors;

/// <summary>
/// 标识错误的大类来源，便于分层处理和日志归档。
/// </summary>
public enum ErrorCategory
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Unsupported = 3,
    Configuration = 4,
    PermissionDenied = 5,
    ExternalDependency = 6,
    IoFailure = 7,
    ParseFailure = 8,
    SerializationFailure = 9,
    Concurrency = 10,
    Cancelled = 11,
    Timeout = 12,
    Unknown = 13
}
