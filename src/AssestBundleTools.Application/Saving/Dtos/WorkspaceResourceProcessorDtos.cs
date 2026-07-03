using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

public sealed record WorkspaceResourceProcessorWorkItemDto(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string OperationId,
    BundleId BundleId,
    string BundleDisplayName,
    string? BundleCacheKey,
    AssetId AssetId,
    string AssetName,
    AssetType AssetType,
    string ResourceKind,
    string InputPayloadPath,
    string StagingDirectoryPath,
    ResourceProcessorCatalogEntryDto Processor);

public sealed record WorkspaceResourceProcessorExecutionResultDto(
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    string ResourceKind,
    string ProcessorId,
    WorkspaceResourceProcessorStatusDto Status,
    string InputPath,
    string? OutputPath,
    string? ResultPath,
    IReadOnlyList<WorkspaceResourceProcessorDiagnosticDto> Diagnostics,
    string? ContentType = null);

public sealed record WorkspaceResourceProcessorDiagnosticDto(
    string Code,
    string Severity,
    string? Message = null);

public enum WorkspaceResourceProcessorStatusDto
{
    Succeeded,
    Skipped,
    Failed
}

public sealed record WorkspaceBundleProcessorExecutionSummaryDto(
    string ResourceKind,
    string ProcessorId,
    WorkspaceResourceProcessorStatusDto Status,
    int InputCount,
    int ProcessedCount,
    int FailedCount,
    IReadOnlyList<string> ResultPaths,
    IReadOnlyList<WorkspaceResourceProcessorDiagnosticDto> Diagnostics);
