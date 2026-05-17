namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区中某个资源包的标准化物理缓存 manifest。
/// </summary>
/// <remarks>
/// 该 manifest 描述的是可删除、可重建的派生 artifact，
/// 不代表用户源文件，也不参与操作记录事实语义。
/// </remarks>
public sealed record BundleArtifactManifestDto(
    string WorkspaceKey,
    string BundleCacheKey,
    string ResolvedBundlePath,
    bool IsMaterialized,
    WorkspaceFileFingerprintDto SourceFingerprint,
    WorkspaceFileFingerprintDto BundleFingerprint,
    string DecryptorId,
    string? DecryptorVersion,
    string NormalizerVersion,
    DateTimeOffset CreatedAtUtc);
