namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示音频转码失败或不可用时的稳定问题分类。
/// </summary>
public enum AudioPayloadTranscodeIssueKind
{
    None = 0,
    EmptyPayload = 1,
    BackendUnavailable = 2,
    ExecutionFailed = 3,
    OutputMissing = 4,
    OutputEmpty = 5
}
