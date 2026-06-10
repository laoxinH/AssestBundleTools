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

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合批处理页中的保存、批量提取、批量替换与变更摘要 Handler。
/// </summary>
public sealed class WorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
{
    private readonly GetWorkspaceChangeSummaryHandler _getWorkspaceChangeSummaryHandler;
    private readonly GetWorkspaceSavePreviewHandler _getWorkspaceSavePreviewHandler;
    private readonly SaveWorkspaceAsNewBundleHandler _saveWorkspaceAsNewBundleHandler;
    private readonly GetWorkspaceBatchExtractionPreviewHandler _getWorkspaceBatchExtractionPreviewHandler;
    private readonly ExtractWorkspaceAssetsHandler _extractWorkspaceAssetsHandler;
    private readonly GetWorkspaceBatchReplacementPreviewHandler _getWorkspaceBatchReplacementPreviewHandler;
    private readonly GetWorkspaceBatchReplacementPlanHandler _getWorkspaceBatchReplacementPlanHandler;
    private readonly ExecuteWorkspaceBatchReplacementHandler _executeWorkspaceBatchReplacementHandler;

    public WorkspaceBatchOperationsGateway(
        GetWorkspaceChangeSummaryHandler getWorkspaceChangeSummaryHandler,
        GetWorkspaceSavePreviewHandler getWorkspaceSavePreviewHandler,
        SaveWorkspaceAsNewBundleHandler saveWorkspaceAsNewBundleHandler,
        GetWorkspaceBatchExtractionPreviewHandler getWorkspaceBatchExtractionPreviewHandler,
        ExtractWorkspaceAssetsHandler extractWorkspaceAssetsHandler,
        GetWorkspaceBatchReplacementPreviewHandler getWorkspaceBatchReplacementPreviewHandler,
        GetWorkspaceBatchReplacementPlanHandler getWorkspaceBatchReplacementPlanHandler,
        ExecuteWorkspaceBatchReplacementHandler executeWorkspaceBatchReplacementHandler)
    {
        _getWorkspaceChangeSummaryHandler = getWorkspaceChangeSummaryHandler ?? throw new ArgumentNullException(nameof(getWorkspaceChangeSummaryHandler));
        _getWorkspaceSavePreviewHandler = getWorkspaceSavePreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceSavePreviewHandler));
        _saveWorkspaceAsNewBundleHandler = saveWorkspaceAsNewBundleHandler ?? throw new ArgumentNullException(nameof(saveWorkspaceAsNewBundleHandler));
        _getWorkspaceBatchExtractionPreviewHandler = getWorkspaceBatchExtractionPreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceBatchExtractionPreviewHandler));
        _extractWorkspaceAssetsHandler = extractWorkspaceAssetsHandler ?? throw new ArgumentNullException(nameof(extractWorkspaceAssetsHandler));
        _getWorkspaceBatchReplacementPreviewHandler = getWorkspaceBatchReplacementPreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceBatchReplacementPreviewHandler));
        _getWorkspaceBatchReplacementPlanHandler = getWorkspaceBatchReplacementPlanHandler ?? throw new ArgumentNullException(nameof(getWorkspaceBatchReplacementPlanHandler));
        _executeWorkspaceBatchReplacementHandler = executeWorkspaceBatchReplacementHandler ?? throw new ArgumentNullException(nameof(executeWorkspaceBatchReplacementHandler));
    }

    public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceChangeSummaryHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceSavePreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default) =>
        _saveWorkspaceAsNewBundleHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceBatchExtractionPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default) =>
        _extractWorkspaceAssetsHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchReplacementPreviewDto>> GetWorkspaceBatchReplacementPreviewAsync(
        GetWorkspaceBatchReplacementPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceBatchReplacementPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceBatchReplacementPlanDto>> GetWorkspaceBatchReplacementPlanAsync(
        GetWorkspaceBatchReplacementPlanQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceBatchReplacementPlanHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default) =>
        _executeWorkspaceBatchReplacementHandler.HandleAsync(command, cancellationToken);
}
