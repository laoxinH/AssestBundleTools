namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示音频载荷转码的执行结果。
/// </summary>
public sealed record AudioPayloadTranscodeResultDto(
    AudioPayloadTranscodeStatusKind Status,
    AudioPayloadTranscodeIssueKind Issue,
    AudioPayloadTranscoderDescriptorDto? Transcoder,
    string? ContentType,
    byte[]? Content,
    string? Message = null)
{
    public bool IsSuccess =>
        Status == AudioPayloadTranscodeStatusKind.Succeeded &&
        !string.IsNullOrWhiteSpace(ContentType) &&
        Content is { Length: > 0 };
}
