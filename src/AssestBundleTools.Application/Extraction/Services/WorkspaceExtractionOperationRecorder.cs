using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 负责把已经完成的提取动作追加到工作区操作记录。
/// </summary>
public sealed class WorkspaceExtractionOperationRecorder
{
    private const string SourceKindWorkspace = "workspace";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;

    public WorkspaceExtractionOperationRecorder(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
    }

    public void RecordAssetExtraction(
        ExtractAssetCommand command,
        AssetDetailsDto targetDetails,
        AssetExtractionResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.ExtractAsset,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindWorkspace,
                OutputDirectory: result.OutputDirectory),
            PreviousState: new WorkspaceOperationStateDto(
                WasModified: targetDetails.IsModified,
                LastExtractedAtUtc: targetDetails.LastExtractedAtUtc),
            Result: new WorkspaceOperationResultDto(
                OutputPath: result.OutputPath,
                OutputDirectory: result.OutputDirectory,
                OutputFileExists: result.OutputFileExists,
                AffectedBundleIds: new[] { result.BundleId.Value },
                AffectedAssetIds: new[] { result.AssetId.Value },
                CompletedCount: 1,
                FailedCount: 0),
            CreatedAtUtc: result.ExtractedAtUtc,
            AppliedAtUtc: result.ExtractedAtUtc,
            CompletedAtUtc: result.ExtractedAtUtc);

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    public void RecordBatchExtraction(
        ExtractWorkspaceAssetsCommand command,
        WorkspaceBatchExtractionResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var assetResults = result.AssetResults ?? Array.Empty<AssetExtractionResultDto>();
        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.ExtractBatch,
            Status: WorkspaceOperationStatus.Applied,
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindWorkspace,
                OutputDirectory: result.SuggestedOutputDirectory),
            Result: new WorkspaceOperationResultDto(
                OutputDirectory: result.SuggestedOutputDirectory,
                AffectedBundleIds: assetResults
                    .Select(static asset => asset.BundleId.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                AffectedAssetIds: assetResults
                    .Select(static asset => asset.AssetId.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                CompletedCount: result.ExtractedAssetCount,
                FailedCount: result.FailedAssetCount),
            CreatedAtUtc: result.CompletedAtUtc,
            AppliedAtUtc: result.CompletedAtUtc,
            CompletedAtUtc: result.CompletedAtUtc);

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    private bool TryLoadChangeSet(
        string? workspaceKey,
        string? workspaceDataRootPath,
        out string normalizedWorkspaceKey,
        out IWorkspaceChangeStore store,
        out WorkspaceChangeSetDto? changeSet)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            normalizedWorkspaceKey = string.Empty;
            store = null!;
            changeSet = null;
            return false;
        }

        normalizedWorkspaceKey = workspaceKey.Trim();
        store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath.Trim());
        changeSet = store.Load(normalizedWorkspaceKey) ?? WorkspaceChangeSetDto.Empty(normalizedWorkspaceKey);
        return true;
    }

    private static WorkspaceOperationTargetDto CreateTarget(AssetDetailsDto details) =>
        new(
            BundleId: details.BundleId.Value,
            AssetId: details.AssetId.Value,
            AssetName: details.Name,
            AssetType: details.AssetType.ToString(),
            AssetTypeDisplayName: details.AssetTypeDisplayName,
            SerializedFileName: details.SerializedFileName,
            PathId: details.PathId);
}
