using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区打开、导入以及资源浏览树/列表相关能力。
/// </summary>
public interface IWorkspaceExplorerGateway
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

    Task<Result<SpineAssetGroupListResultDto>> LoadSpineAssetGroupsAsync(
        LoadSpineAssetGroupsQuery query,
        CancellationToken cancellationToken = default);
}
