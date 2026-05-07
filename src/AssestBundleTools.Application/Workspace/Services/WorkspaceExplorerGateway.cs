using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区打开、导入与资源浏览列表相关 Handler。
/// </summary>
public sealed class WorkspaceExplorerGateway : IWorkspaceExplorerGateway
{
    private readonly OpenWorkspaceHandler _openWorkspaceHandler;
    private readonly RegisterWorkspaceImportHandler _registerWorkspaceImportHandler;
    private readonly ProcessWorkspaceImportsHandler _processWorkspaceImportsHandler;
    private readonly RemoveWorkspaceBundleHandler _removeWorkspaceBundleHandler;
    private readonly LoadBundleTreeHandler _loadBundleTreeHandler;
    private readonly LoadAssetListHandler _loadAssetListHandler;
    private readonly LoadSpineAssetGroupsHandler _loadSpineAssetGroupsHandler;

    public WorkspaceExplorerGateway(
        OpenWorkspaceHandler openWorkspaceHandler,
        RegisterWorkspaceImportHandler registerWorkspaceImportHandler,
        ProcessWorkspaceImportsHandler processWorkspaceImportsHandler,
        RemoveWorkspaceBundleHandler removeWorkspaceBundleHandler,
        LoadBundleTreeHandler loadBundleTreeHandler,
        LoadAssetListHandler loadAssetListHandler,
        LoadSpineAssetGroupsHandler loadSpineAssetGroupsHandler)
    {
        _openWorkspaceHandler = openWorkspaceHandler ?? throw new ArgumentNullException(nameof(openWorkspaceHandler));
        _registerWorkspaceImportHandler = registerWorkspaceImportHandler ?? throw new ArgumentNullException(nameof(registerWorkspaceImportHandler));
        _processWorkspaceImportsHandler = processWorkspaceImportsHandler ?? throw new ArgumentNullException(nameof(processWorkspaceImportsHandler));
        _removeWorkspaceBundleHandler = removeWorkspaceBundleHandler ?? throw new ArgumentNullException(nameof(removeWorkspaceBundleHandler));
        _loadBundleTreeHandler = loadBundleTreeHandler ?? throw new ArgumentNullException(nameof(loadBundleTreeHandler));
        _loadAssetListHandler = loadAssetListHandler ?? throw new ArgumentNullException(nameof(loadAssetListHandler));
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler ?? throw new ArgumentNullException(nameof(loadSpineAssetGroupsHandler));
    }

    public Task<Result<WorkspaceSummaryDto>> OpenWorkspaceAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default) =>
        _openWorkspaceHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceImportRegistrationResultDto>> RegisterWorkspaceImportAsync(
        RegisterWorkspaceImportCommand command,
        CancellationToken cancellationToken = default) =>
        _registerWorkspaceImportHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceImportProcessingResultDto>> ProcessWorkspaceImportsAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default) =>
        _processWorkspaceImportsHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBundleRemovalResultDto>> RemoveWorkspaceBundleAsync(
        RemoveWorkspaceBundleCommand command,
        CancellationToken cancellationToken = default) =>
        _removeWorkspaceBundleHandler.HandleAsync(command, cancellationToken);

    public Task<Result<IReadOnlyList<BundleTreeNodeDto>>> LoadBundleTreeAsync(
        LoadBundleTreeQuery query,
        CancellationToken cancellationToken = default) =>
        _loadBundleTreeHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetListResultDto>> LoadAssetListAsync(
        LoadAssetListQuery query,
        CancellationToken cancellationToken = default) =>
        _loadAssetListHandler.HandleAsync(query, cancellationToken);

    public Task<Result<SpineAssetGroupListResultDto>> LoadSpineAssetGroupsAsync(
        LoadSpineAssetGroupsQuery query,
        CancellationToken cancellationToken = default) =>
        _loadSpineAssetGroupsHandler.HandleAsync(query, cancellationToken);
}
