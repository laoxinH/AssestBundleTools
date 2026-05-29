using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 负责把替换链路中已经成功应用的动作追加到工作区操作记录。
/// </summary>
public sealed class WorkspaceReplacementOperationRecorder
{
    private const string SourceKindFile = "file";
    private const string SourceKindWorkspace = "workspace";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;
    private readonly WorkspaceReplacementStateMaterializer _replacementStateMaterializer;

    public WorkspaceReplacementOperationRecorder(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService,
        WorkspaceReplacementStateMaterializer? replacementStateMaterializer = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
        _replacementStateMaterializer = replacementStateMaterializer ?? new WorkspaceReplacementStateMaterializer();
    }

    public void RecordFileReplacement(
        ReplaceAssetFromFileCommand command,
        AssetDetailsDto targetDetails,
        AssetFileReplacementResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.ReplaceFromFile,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindFile,
                SourceFilePath: result.SourceFilePath,
                SourceFileName: result.SourceFileName,
                SourceAssetType: result.SourceAssetType.ToString()),
            PreviousState: CreatePreviousState(targetDetails),
            Result: new WorkspaceOperationResultDto(ReplacementSourceKind: SourceKindFile),
            CreatedAtUtc: result.ReplacedAtUtc,
            AppliedAtUtc: result.ReplacedAtUtc,
            CompletedAtUtc: result.ReplacedAtUtc,
            SupersedesOperationId: FindLatestActiveReplacementOperationId(changeSet, targetDetails.BundleId.Value, targetDetails.AssetId.Value));

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    public void RecordWorkspaceReplacement(
        ReplaceAssetFromWorkspaceCommand command,
        AssetDetailsDto targetDetails,
        AssetDetailsDto sourceDetails,
        AssetReplacementResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.ReplaceFromWorkspace,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindWorkspace,
                SourceBundleId: result.SourceBundleId.Value,
                SourceBundleDisplayName: result.SourceBundleDisplayName,
                SourceAssetId: result.SourceAssetId.Value,
                SourceAssetName: result.SourceAssetName,
                SourceAssetType: result.SourceAssetType.ToString()),
            PreviousState: CreatePreviousState(targetDetails),
            Result: new WorkspaceOperationResultDto(ReplacementSourceKind: SourceKindWorkspace),
            CreatedAtUtc: result.ReplacedAtUtc,
            AppliedAtUtc: result.ReplacedAtUtc,
            CompletedAtUtc: result.ReplacedAtUtc,
            SupersedesOperationId: FindLatestActiveReplacementOperationId(changeSet, targetDetails.BundleId.Value, targetDetails.AssetId.Value));

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    public void RecordUndoReplacement(
        UndoAssetReplacementCommand command,
        AssetDetailsDto targetDetails,
        AssetReplacementUndoResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var recordedAtUtc = DateTimeOffset.UtcNow;
        var latestReplacementOperation = FindLatestActiveReplacementOperation(
            changeSet,
            targetDetails.BundleId.Value,
            targetDetails.AssetId.Value);
        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.UndoReplacement,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            PreviousState: CreatePreviousState(targetDetails),
            CreatedAtUtc: recordedAtUtc,
            AppliedAtUtc: recordedAtUtc,
            CompletedAtUtc: recordedAtUtc,
            SupersedesOperationId: latestReplacementOperation?.OperationId,
            Relations: latestReplacementOperation?.Relations);

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    public void RecordRedoReplacement(
        RedoAssetReplacementCommand command,
        AssetDetailsDto targetDetails,
        AssetReplacementRedoResultDto result,
        WorkspaceOperationRecordDto? replacementOperation = null)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var recordedAtUtc = result.RedoneAtUtc;
        var source = replacementOperation?.Source;
        var latestUndoOperation = replacementOperation is null
            ? FindLatestUndoOperation(changeSet, targetDetails.BundleId.Value, targetDetails.AssetId.Value)
            : null;
        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.RedoReplacement,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            Source: source,
            PreviousState: CreatePreviousState(targetDetails),
            Result: source is null
                ? null
                : new WorkspaceOperationResultDto(ReplacementSourceKind: source.SourceKind),
            CreatedAtUtc: recordedAtUtc,
            AppliedAtUtc: recordedAtUtc,
            CompletedAtUtc: recordedAtUtc,
            SupersedesOperationId: replacementOperation?.OperationId ??
                                  latestUndoOperation?.OperationId,
            Relations: replacementOperation?.Relations ?? latestUndoOperation?.Relations);

        store.Save(_operationLogService.AppendOperation(changeSet, appendRequest));
    }

    public bool TryFindReplacementOperation(
        RedoAssetReplacementCommand command,
        out WorkspaceOperationRecordDto? replacementOperation)
    {
        return TryFindReplacementOperation(
            command.ReplacementOperationId,
            command.WorkspaceKey,
            command.WorkspaceDataRootPath,
            out replacementOperation);
    }

    public bool TryFindReplacementOperation(
        UndoAssetReplacementCommand command,
        out WorkspaceOperationRecordDto? replacementOperation)
    {
        return TryFindReplacementOperation(
            command.ReplacementOperationId,
            command.WorkspaceKey,
            command.WorkspaceDataRootPath,
            out replacementOperation);
    }

    private bool TryFindReplacementOperation(
        string? replacementOperationId,
        string? workspaceKey,
        string? workspaceDataRootPath,
        out WorkspaceOperationRecordDto? replacementOperation)
    {
        replacementOperation = null;
        if (string.IsNullOrWhiteSpace(replacementOperationId) ||
            !TryLoadChangeSet(workspaceKey, workspaceDataRootPath, out _, out _, out var changeSet))
        {
            return false;
        }

        var operationId = replacementOperationId.Trim();
        replacementOperation = (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace)
            .FirstOrDefault(operation =>
                string.Equals(operation.OperationId, operationId, StringComparison.Ordinal));

        return replacementOperation is not null;
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

    private string? FindLatestActiveReplacementOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId) =>
        _replacementStateMaterializer.FindLatestEffectiveReplacementOperationId(changeSet, bundleId, assetId);

    private WorkspaceOperationRecordDto? FindLatestActiveReplacementOperation(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        var operationId = FindLatestActiveReplacementOperationId(changeSet, bundleId, assetId);
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .FirstOrDefault(operation => string.Equals(operation.OperationId, operationId, StringComparison.Ordinal));
    }

    private static WorkspaceOperationRecordDto? FindLatestUndoOperation(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId) =>
        (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.UndoReplacement)
            .Where(operation => WorkspaceOperationRelationMatcher.TargetMatches(operation.Target, bundleId, assetId))
            .OrderByDescending(static operation => operation.Sequence)
            .FirstOrDefault();

    private static WorkspaceOperationTargetDto CreateTarget(AssetDetailsDto details) =>
        new(
            BundleId: details.BundleId.Value,
            AssetId: details.AssetId.Value,
            AssetName: details.Name,
            AssetType: details.AssetType.ToString(),
            AssetTypeDisplayName: details.AssetTypeDisplayName,
            SerializedFileName: details.SerializedFileName,
            PathId: details.PathId);

    private static WorkspaceOperationStateDto CreatePreviousState(AssetDetailsDto details) =>
        new(
            WasModified: details.IsModified,
            LastExtractedAtUtc: details.LastExtractedAtUtc,
            ReplacementSource: CreatePreviousReplacementSource(details));

    private static WorkspaceOperationSourceDto? CreatePreviousReplacementSource(AssetDetailsDto details)
    {
        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourcePath))
        {
            return new WorkspaceOperationSourceDto(
                SourceKind: SourceKindFile,
                SourceFilePath: details.FileReplacementSourcePath,
                SourceFileName: details.FileReplacementSourceFileName,
                SourceAssetType: details.FileReplacementSourceAssetType?.ToString());
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName))
        {
            return new WorkspaceOperationSourceDto(
                SourceKind: SourceKindWorkspace,
                SourceBundleId: details.WorkspaceReplacementSourceBundleId?.Value,
                SourceBundleDisplayName: details.WorkspaceReplacementSourceBundleDisplayName,
                SourceAssetId: details.WorkspaceReplacementSourceAssetId?.Value,
                SourceAssetName: details.WorkspaceReplacementSourceAssetName,
                SourceAssetType: details.WorkspaceReplacementSourceAssetType?.ToString());
        }

        return null;
    }
}
