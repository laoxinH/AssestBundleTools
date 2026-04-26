namespace AssestBundleTools.Domain.Common.Errors;

/// <summary>
/// 表示错误对当前操作或当前会话的严重程度。
/// </summary>
public enum ErrorSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
