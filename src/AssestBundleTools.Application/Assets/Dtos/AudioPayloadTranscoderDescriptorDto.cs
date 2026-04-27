namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示某个音频载荷转码器的稳定身份。
/// </summary>
public sealed record AudioPayloadTranscoderDescriptorDto(
    string Id,
    string DisplayName,
    string Version,
    string? Description = null);
