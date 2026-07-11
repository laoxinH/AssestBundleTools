namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示保存或更新标准化资源包物理缓存的请求。
/// </summary>
public sealed record BundleArtifactSaveRequestDto(
    string WorkspaceKey,
    string BundleCacheKey,
    WorkspaceFileFingerprintDto SourceFingerprint,
    string DecryptorId,
    string? DecryptorVersion,
    string NormalizerVersion,
    string ResolvedBundlePath,
    bool ProducedDerivedFile,
    WorkspaceFileFingerprintDto ResolvedBundleFingerprint,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string>? CodecParameters = null);
