namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 请求按容量或数量上限清理标准化资源包 artifact。
/// </summary>
public sealed record BundleArtifactPruneRequestDto(
    string? WorkspaceKey,
    long? MaximumSizeInBytes,
    int? MaximumArtifactCount);
