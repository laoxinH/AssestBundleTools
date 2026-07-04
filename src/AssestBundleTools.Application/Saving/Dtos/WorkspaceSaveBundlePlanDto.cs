using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示某个资源包在“保存为新包”阶段的单项输出计划。
/// </summary>
public sealed record WorkspaceSaveBundlePlanDto(
    BundleId BundleId,
    string BundleDisplayName,
    string OutputFilePath,
    int ChangeCount,
    int ModifiedAssetCount,
    int ExtractedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    string? BundleCacheKey = null,
    bool HasStandardizedArtifact = false,
    string? ArtifactBundlePath = null,
    string? BundleSessionCacheKey = null,
    string? ArtifactDiagnosticCode = null,
    WorkspaceSaveOutputReadiness OutputReadiness = WorkspaceSaveOutputReadiness.NotProbed,
    bool IsOutputWriteBlocked = false,
    string? OutputDiagnosticCode = null,
    string? OriginalCompression = null);
