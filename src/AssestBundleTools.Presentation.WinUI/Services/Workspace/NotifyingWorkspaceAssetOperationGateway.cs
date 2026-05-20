using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 为资产操作网关补充工作区读模型失效通知，避免页面手动互相刷新。
/// </summary>
internal sealed class NotifyingWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
{
    private readonly IWorkspaceAssetOperationGateway _inner;
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;

    public NotifyingWorkspaceAssetOperationGateway(
        IWorkspaceAssetOperationGateway inner,
        IWorkspaceOperationChangeNotifier operationChangeNotifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
    }

    public async Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractAssetAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Extraction);
        return result;
    }

    public async Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractSpineAssetGroupAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Extraction);
        return result;
    }

    public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetFileReplacementCandidateAsync(query, cancellationToken);

    public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceReplacementCandidatesAsync(query, cancellationToken);

    public async Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReplaceAssetFromFileAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Replacement);
        return result;
    }

    public async Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReplaceAssetFromWorkspaceAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Replacement);
        return result;
    }

    public async Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.UndoAssetReplacementAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Replacement);
        return result;
    }

    public async Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
        RedoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.RedoAssetReplacementAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Replacement);
        return result;
    }

    private void NotifyIfSucceeded<T>(
        Result<T> result,
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind)
    {
        if (result.IsSuccess)
        {
            _operationChangeNotifier.NotifyWorkspaceOperationsChanged(workspaceId, changeKind);
        }
    }
}
