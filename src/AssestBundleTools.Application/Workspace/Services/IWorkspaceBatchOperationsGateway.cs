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
/// 聚合批处理页中的保存、批量提取、批量替换与变更摘要能力。
/// </summary>
public interface IWorkspaceBatchOperationsGateway
{
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

    Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchReplacementPlanDto>> GetWorkspaceBatchReplacementPlanAsync(
        GetWorkspaceBatchReplacementPlanQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default);
}
