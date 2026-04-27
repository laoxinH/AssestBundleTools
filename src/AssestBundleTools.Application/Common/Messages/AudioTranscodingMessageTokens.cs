namespace AssestBundleTools.Application.Common.Messages;

/// <summary>
/// 集中定义音频转码链路使用的稳定消息令牌。
/// </summary>
/// <remarks>
/// 外部转码工具的原始 stderr / stdout 允许作为回退文本透传，
/// 但主程序自身产生的“超时 / 启动失败 / 无退出详情”等情况
/// 应统一收敛到稳定令牌，避免把宿主异常消息直接暴露到展示层。
/// </remarks>
public static class AudioTranscodingMessageTokens
{
    public const string ProcessExited = "AudioTranscoding.Detail.ProcessExited";
    public const string ProcessTimedOut = "AudioTranscoding.Detail.ProcessTimedOut";
    public const string ExecutionFailed = "AudioTranscoding.Detail.ExecutionFailed";
}
