namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示一次音频载荷转码请求。
/// </summary>
public sealed record AudioPayloadTranscodeRequestDto(
    byte[] Payload,
    AudioContainerKind ContainerKind,
    string SourceFileName,
    string WorkspaceKey,
    string WorkspaceDataRootPath);
