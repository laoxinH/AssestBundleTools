using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示当前工作区在真实保存前需要展示的保存预览摘要。
/// </summary>
/// <remarks>
/// 当前阶段仍不执行真实写包，
/// 这个 DTO 的职责是为批处理页提供“将要写出什么”的稳定承载面，
/// 让后续 M10 的真实保存能力可以直接复用这一层读模型。
/// </remarks>
public sealed record WorkspaceSavePreviewDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    string OutputDirectory,
    int PlannedBundleCount,
    int TotalChangeCount,
    int ModifiedAssetCount,
    int ExtractedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    IReadOnlyList<WorkspaceSaveBundlePlanDto> BundlePlans,
    int ResolvedArtifactInputCount = 0,
    int UnresolvedArtifactInputCount = 0,
    int OutputReadyBundleCount = 0,
    int OutputBlockedBundleCount = 0,
    int OutputUnprobedBundleCount = 0,
    bool IsReadyForWrite = false);
