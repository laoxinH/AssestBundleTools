using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Common.Messages;

/// <summary>
/// 负责把应用层标准错误映射为可供展示层本地化的结构化消息。
/// </summary>
/// <remarks>
/// 这里不做任何最终语言格式化，只保留错误令牌和参数顺序，
/// 便于检查器、导入日志和后续诊断页复用同一套错误语义。
/// </remarks>
public static class AppErrorLocalizedMessageMapper
{
    public static LocalizedMessageDto? CreateFromFirstError(IReadOnlyList<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors.Count == 0
            ? null
            : Create(errors[0]);
    }

    public static LocalizedMessageDto Create(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var arguments = error.Parameters.Count == 0
            ? null
            : error.Parameters.Values
                .Select(static value => value?.ToString() ?? string.Empty)
                .ToArray();

        return new LocalizedMessageDto(
            MessageToken: error.MessageToken,
            Arguments: arguments);
    }
}
