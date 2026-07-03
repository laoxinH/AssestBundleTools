using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示当前工作区在真实保存前需要展示的保存预览摘要。
/// </summary>
/// <remarks>
/// 这个 DTO 只描述“将要写出什么”；真实写包由保存执行命令在后台任务中消费同一读模型。
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
