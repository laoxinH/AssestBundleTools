namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示音频载荷当前识别出的容器类型。
/// </summary>
public enum AudioContainerKind
{
    Unknown = 0,
    Wav = 1,
    Ogg = 2,
    Mp3 = 3,
    Flac = 4,
    Mp4 = 5,
    Aac = 6,
    Fsb5 = 7
}
