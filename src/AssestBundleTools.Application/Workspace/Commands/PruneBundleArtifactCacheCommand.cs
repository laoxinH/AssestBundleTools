namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 按容量或数量上限清理标准化资源包 artifact 缓存。
/// </summary>
public sealed record PruneBundleArtifactCacheCommand(
    string? WorkspaceDataRootPath,
    string? WorkspaceKey,
    long? MaximumSizeInBytes,
    int? MaximumArtifactCount);
