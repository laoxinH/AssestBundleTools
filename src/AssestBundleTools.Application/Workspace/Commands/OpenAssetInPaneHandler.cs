using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 处理“在当前窗格打开资源”命令。
/// </summary>
public sealed class OpenAssetInPaneHandler : ICommandHandler<OpenAssetInPaneCommand, Result<AssetPaneContextDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public OpenAssetInPaneHandler(IWorkspaceSessionRegistry workspaceSessionRegistry)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
    }

    public Task<Result<AssetPaneContextDto>> HandleAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.PaneId.Value))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.PaneIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(command.WorkspaceId, command.BundleId))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.BundleNotFound(command.BundleId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(command.WorkspaceId, command.BundleId, command.AssetId))
        {
            return Task.FromResult(Result<AssetPaneContextDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        _workspaceSessionRegistry.TryOpenAssetInPane(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            command.PaneId,
            out var paneContext);

        return Task.FromResult(Result<AssetPaneContextDto>.Success(paneContext!));
    }
}
