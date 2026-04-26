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
    PermissionDenied = 4,
    ExternalDependency = 5,
    IoFailure = 6,
    ParseFailure = 7,
    SerializationFailure = 8,
    Concurrency = 9,
    Cancelled = 10,
    Unknown = 11
}
