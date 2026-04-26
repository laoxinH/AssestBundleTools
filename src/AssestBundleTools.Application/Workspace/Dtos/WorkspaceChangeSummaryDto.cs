using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示当前工作区在批处理与保存前阶段需要展示的变更摘要。
/// </summary>
public sealed record WorkspaceChangeSummaryDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    string SourcePath,
    int BundleCount,
    int AssetCount,
    int TotalChangeCount,
    int ModifiedAssetCount,
    int ExtractedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    IReadOnlyList<WorkspaceChangeItemDto> Items);
