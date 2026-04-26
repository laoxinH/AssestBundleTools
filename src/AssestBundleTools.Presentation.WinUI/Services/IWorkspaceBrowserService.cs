using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 为 WinUI 页面提供统一的工作区浏览调用入口。
/// </summary>
/// <remarks>
/// 这个服务本身不承载业务规则，职责只是把页面需要的 Use Case 组织成
/// 一个更顺手的 UI 调用面，避免 ViewModel 直接散落持有太多 Handler 实例。
/// </remarks>
public interface IWorkspaceBrowserService
{
    Task<Result<WorkspaceSummaryDto>> OpenWorkspaceAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceImportRegistrationResultDto>> RegisterWorkspaceImportAsync(
        RegisterWorkspaceImportCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceImportProcessingResultDto>> ProcessWorkspaceImportsAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBundleRemovalResultDto>> RemoveWorkspaceBundleAsync(
        RemoveWorkspaceBundleCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BundleTreeNodeDto>>> LoadBundleTreeAsync(
        LoadBundleTreeQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetListResultDto>> LoadAssetListAsync(
        LoadAssetListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetPreviewDto>> GetAssetPreviewAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchReplacementPreviewDto>> GetWorkspaceBatchReplacementPreviewAsync(
        GetWorkspaceBatchReplacementPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetPaneContextDto>> OpenAssetInPaneAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default);
}
