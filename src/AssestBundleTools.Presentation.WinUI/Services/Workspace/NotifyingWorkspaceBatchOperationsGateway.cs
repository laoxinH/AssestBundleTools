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
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 为批处理操作网关补充工作区读模型失效通知。
/// </summary>
internal sealed class NotifyingWorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
{
    private readonly IWorkspaceBatchOperationsGateway _inner;
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;

    public NotifyingWorkspaceBatchOperationsGateway(
        IWorkspaceBatchOperationsGateway inner,
        IWorkspaceOperationChangeNotifier operationChangeNotifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
    }

    public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceChangeSummaryAsync(query, cancellationToken);

    public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceSavePreviewAsync(query, cancellationToken);

    public async Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.SaveWorkspaceAsNewBundleAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Save);
        return result;
    }

    public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceBatchExtractionPreviewAsync(query, cancellationToken);

    public async Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractWorkspaceAssetsAsync(command, cancellationToken);
        NotifyIfSucceeded(result, command.WorkspaceId, WorkspaceOperationChangeKind.Extraction);
        return result;
    }

    public Task<Result<WorkspaceBatchReplacementPreviewDto>> GetWorkspaceBatchReplacementPreviewAsync(
        GetWorkspaceBatchReplacementPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceBatchReplacementPreviewAsync(query, cancellationToken);

    public async Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExecuteWorkspaceBatchReplacementAsync(command, cancellationToken);
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
