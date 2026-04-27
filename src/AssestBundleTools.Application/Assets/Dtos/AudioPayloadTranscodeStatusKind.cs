namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示音频载荷转码尝试的结果状态。
/// </summary>
public enum AudioPayloadTranscodeStatusKind
{
    Succeeded = 0,
    Unavailable = 1,
    Unsupported = 2,
    Failed = 3
}
