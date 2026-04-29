using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合资产详情、预览与文档页签装载相关 Handler。
/// </summary>
public sealed class WorkspaceAssetInspectionGateway : IWorkspaceAssetInspectionGateway
{
    private readonly GetAssetDetailsHandler _getAssetDetailsHandler;
    private readonly GetAssetPreviewHandler _getAssetPreviewHandler;
    private readonly OpenAssetInPaneHandler _openAssetInPaneHandler;

    public WorkspaceAssetInspectionGateway(
        GetAssetDetailsHandler getAssetDetailsHandler,
        GetAssetPreviewHandler getAssetPreviewHandler,
        OpenAssetInPaneHandler openAssetInPaneHandler)
    {
        _getAssetDetailsHandler = getAssetDetailsHandler ?? throw new ArgumentNullException(nameof(getAssetDetailsHandler));
        _getAssetPreviewHandler = getAssetPreviewHandler ?? throw new ArgumentNullException(nameof(getAssetPreviewHandler));
        _openAssetInPaneHandler = openAssetInPaneHandler ?? throw new ArgumentNullException(nameof(openAssetInPaneHandler));
    }

    public Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default) =>
        _getAssetDetailsHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetPreviewDto>> GetAssetPreviewAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getAssetPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetPaneContextDto>> OpenAssetInPaneAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default) =>
        _openAssetInPaneHandler.HandleAsync(command, cancellationToken);
}
