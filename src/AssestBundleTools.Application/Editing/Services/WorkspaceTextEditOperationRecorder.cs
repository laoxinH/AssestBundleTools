using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 维护文本编辑草稿描述与操作事实流。
/// </summary>
public sealed class WorkspaceTextEditOperationRecorder
{
    public const string TextDraftMode = "text-content";

    private const string SourceKindDraft = "draft";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;

    public WorkspaceTextEditOperationRecorder(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
    }

    public void RecordDraftCreated(
        CreateTextAssetEditDraftCommand command,
        AssetDetailsDto targetDetails,
        AssetTextEditDraftDto draft)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var timestamp = draft.UpdatedAtUtc;
        var sourceFingerprint = string.IsNullOrWhiteSpace(draft.SourceFingerprint)
            ? CreateSourceFingerprint(targetDetails, draft.Reference.ContentLength)
            : draft.SourceFingerprint.Trim();
        var updatedChangeSet = UpsertDraftDescriptor(
            changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey),
            draft.DraftId,
            draft.DraftMode,
            sourceFingerprint,
            timestamp,
            targetDetails.Name,
            draft.Summary,
            draft.IsMaterialized
                ? null
                : TextAssetEditDraftSourceReferenceMapper.ToWorkspaceReference(draft.Reference),
            isPayloadMaterialized: draft.IsMaterialized);

        updatedChangeSet = _operationLogService.AppendOperation(
            updatedChangeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: workspaceKey,
                OperationKind: WorkspaceOperationKind.EditDraftCreated,
                Status: WorkspaceOperationStatus.Applied,
                Target: CreateTarget(targetDetails),
                Source: new WorkspaceOperationSourceDto(
                    SourceKind: SourceKindDraft,
                    DraftId: draft.DraftId,
                    DraftMode: draft.DraftMode),
                Result: new WorkspaceOperationResultDto(
                    DraftId: draft.DraftId,
                    AffectedBundleIds: [targetDetails.BundleId.Value],
                    AffectedAssetIds: [targetDetails.AssetId.Value]),
                CreatedAtUtc: timestamp,
                AppliedAtUtc: timestamp,
                CompletedAtUtc: timestamp));

        store.Save(updatedChangeSet);
    }

    public void RecordDraftApplied(
        ApplyTextAssetEditDraftCommand command,
        AssetDetailsDto targetDetails,
        AssetTextEditAppliedResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var normalizedChangeSet = changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey);
        var sourceFingerprint = ResolveDraftSourceFingerprint(
            normalizedChangeSet,
            result.DraftId,
            result.DraftMode) ??
            CreateSourceFingerprint(targetDetails, targetDetails.PreferredDisplaySizeInBytes ?? targetDetails.SizeInBytes);
        var updatedChangeSet = UpsertDraftDescriptor(
            normalizedChangeSet,
            result.DraftId,
            result.DraftMode,
            sourceFingerprint,
            result.AppliedAtUtc,
            targetDetails.Name,
            targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString(),
            isPayloadMaterialized: true);

        updatedChangeSet = _operationLogService.AppendOperation(
            updatedChangeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: workspaceKey,
                OperationKind: WorkspaceOperationKind.EditDraftApplied,
                Status: WorkspaceOperationStatus.Applied,
                Target: CreateTarget(targetDetails),
                Source: new WorkspaceOperationSourceDto(
                    SourceKind: SourceKindDraft,
                    DraftId: result.DraftId,
                    DraftMode: result.DraftMode),
                PreviousState: CreatePreviousState(targetDetails),
                Result: new WorkspaceOperationResultDto(
                    DraftId: result.DraftId,
                    AffectedBundleIds: [targetDetails.BundleId.Value],
                    AffectedAssetIds: [targetDetails.AssetId.Value]),
                CreatedAtUtc: result.AppliedAtUtc,
                AppliedAtUtc: result.AppliedAtUtc,
                CompletedAtUtc: result.AppliedAtUtc));

        store.Save(updatedChangeSet);
    }

    public void RecordDraftUpdated(
        UpdateTextAssetEditDraftCommand command,
        AssetDetailsDto targetDetails,
        AssetTextEditDraftUpdatedResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var normalizedChangeSet = changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey);
        var sourceFingerprint = ResolveDraftSourceFingerprint(
            normalizedChangeSet,
            result.DraftId,
            result.DraftMode) ??
            CreateSourceFingerprint(targetDetails, targetDetails.PreferredDisplaySizeInBytes ?? targetDetails.SizeInBytes);
        var updatedChangeSet = UpsertDraftDescriptor(
            normalizedChangeSet,
            result.DraftId,
            result.DraftMode,
            sourceFingerprint,
            result.UpdatedAtUtc,
            targetDetails.Name,
            targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString(),
            isPayloadMaterialized: true);

        updatedChangeSet = _operationLogService.AppendOperation(
            updatedChangeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: workspaceKey,
                OperationKind: WorkspaceOperationKind.EditDraftUpdated,
                Status: WorkspaceOperationStatus.Applied,
                Target: CreateTarget(targetDetails),
                Source: new WorkspaceOperationSourceDto(
                    SourceKind: SourceKindDraft,
                    DraftId: result.DraftId,
                    DraftMode: result.DraftMode),
                Result: new WorkspaceOperationResultDto(
                    DraftId: result.DraftId,
                    AffectedBundleIds: [targetDetails.BundleId.Value],
                    AffectedAssetIds: [targetDetails.AssetId.Value]),
                CreatedAtUtc: result.UpdatedAtUtc,
                AppliedAtUtc: result.UpdatedAtUtc,
                CompletedAtUtc: result.UpdatedAtUtc));

        store.Save(updatedChangeSet);
    }

    public void RecordDraftDiscarded(
        DiscardTextAssetEditDraftCommand command,
        AssetDetailsDto targetDetails,
        AssetTextEditDraftDiscardedResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var timestamp = result.DiscardedAtUtc;
        var updatedChangeSet = RemoveDraftDescriptor(
            changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey),
            result.DraftId,
            result.DraftMode);

        updatedChangeSet = _operationLogService.AppendOperation(
            updatedChangeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: workspaceKey,
                OperationKind: WorkspaceOperationKind.EditDraftDiscarded,
                Status: WorkspaceOperationStatus.Applied,
                Target: CreateTarget(targetDetails),
                Source: new WorkspaceOperationSourceDto(
                    SourceKind: SourceKindDraft,
                    DraftId: result.DraftId,
                    DraftMode: result.DraftMode),
                Result: new WorkspaceOperationResultDto(
                    DraftId: result.DraftId,
                    AffectedBundleIds: [targetDetails.BundleId.Value],
                    AffectedAssetIds: [targetDetails.AssetId.Value]),
                CreatedAtUtc: timestamp,
                AppliedAtUtc: timestamp,
                CompletedAtUtc: timestamp));

        store.Save(updatedChangeSet);
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

    private static WorkspaceChangeSetDto UpsertDraftDescriptor(
        WorkspaceChangeSetDto changeSet,
        string draftId,
        string draftMode,
        string sourceFingerprint,
        DateTimeOffset timestamp,
        string sourceDescription,
        string? summary,
        WorkspaceAssetEditDraftSourceReferenceDto? sourceReference = null,
        bool? isPayloadMaterialized = null)
    {
        var normalizedDraftId = draftId.Trim();
        var normalizedDraftMode = draftMode.Trim();
        var existingDescriptor = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .FirstOrDefault(descriptor =>
                string.Equals(descriptor.DraftId?.Trim(), normalizedDraftId, StringComparison.Ordinal) &&
                string.Equals(descriptor.DraftMode?.Trim(), normalizedDraftMode, StringComparison.Ordinal));
        var descriptors = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .Where(descriptor =>
                !string.Equals(descriptor.DraftId?.Trim(), normalizedDraftId, StringComparison.Ordinal) ||
                !string.Equals(descriptor.DraftMode?.Trim(), normalizedDraftMode, StringComparison.Ordinal))
            .Append(new WorkspaceAssetEditDraftDescriptorDto(
                DraftId: normalizedDraftId,
                DraftMode: normalizedDraftMode,
                SourceFingerprint: sourceFingerprint.Trim(),
                UpdatedAtUtc: timestamp,
                SourceDescription: sourceDescription,
                Summary: summary,
                SourceReference: sourceReference ?? existingDescriptor?.SourceReference,
                IsPayloadMaterialized: isPayloadMaterialized ?? existingDescriptor?.IsPayloadMaterialized))
            .ToArray();

        return changeSet with
        {
            EditDrafts = descriptors
        };
    }

    private static string? ResolveDraftSourceFingerprint(
        WorkspaceChangeSetDto changeSet,
        string draftId,
        string draftMode)
    {
        var normalizedDraftId = draftId.Trim();
        var normalizedDraftMode = draftMode.Trim();
        return (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .FirstOrDefault(descriptor =>
                string.Equals(descriptor.DraftId?.Trim(), normalizedDraftId, StringComparison.Ordinal) &&
                string.Equals(descriptor.DraftMode?.Trim(), normalizedDraftMode, StringComparison.Ordinal))
            ?.SourceFingerprint;
    }

    private static WorkspaceChangeSetDto RemoveDraftDescriptor(
        WorkspaceChangeSetDto changeSet,
        string draftId,
        string draftMode)
    {
        var normalizedDraftId = draftId.Trim();
        var normalizedDraftMode = draftMode.Trim();
        var descriptors = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .Where(descriptor =>
                !string.Equals(descriptor.DraftId?.Trim(), normalizedDraftId, StringComparison.Ordinal) ||
                !string.Equals(descriptor.DraftMode?.Trim(), normalizedDraftMode, StringComparison.Ordinal))
            .ToArray();

        return changeSet with
        {
            EditDrafts = descriptors
        };
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

    private static WorkspaceOperationStateDto CreatePreviousState(AssetDetailsDto details) =>
        new(
            WasModified: details.IsModified,
            LastExtractedAtUtc: details.LastExtractedAtUtc,
            ReplacementSource: CreatePreviousReplacementSource(details),
            DraftId: ResolveEffectiveEditDraftId(details),
            SpriteEditGeometry: details.SpriteEditGeometry);

    private static WorkspaceOperationSourceDto? CreatePreviousReplacementSource(AssetDetailsDto details)
    {
        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourcePath))
        {
            return new WorkspaceOperationSourceDto(
                SourceKind: "file",
                SourceFilePath: details.FileReplacementSourcePath,
                SourceFileName: details.FileReplacementSourceFileName,
                SourceAssetType: details.FileReplacementSourceAssetType?.ToString());
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName))
        {
            return new WorkspaceOperationSourceDto(
                SourceKind: "workspace",
                SourceBundleId: details.WorkspaceReplacementSourceBundleId?.Value,
                SourceBundleDisplayName: details.WorkspaceReplacementSourceBundleDisplayName,
                SourceAssetId: details.WorkspaceReplacementSourceAssetId?.Value,
                SourceAssetName: details.WorkspaceReplacementSourceAssetName,
                SourceAssetType: details.WorkspaceReplacementSourceAssetType?.ToString());
        }

        return null;
    }

    private static string? ResolveEffectiveEditDraftId(AssetDetailsDto details) =>
        details.ChangeRecords?
            .Where(static record => record.Kind is AssetChangeRecordKind.SpriteEdit or AssetChangeRecordKind.TextEdit &&
                                    record.IsEffectiveEdit)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => record.DraftId)
            .FirstOrDefault(static draftId => !string.IsNullOrWhiteSpace(draftId));

    public static string CreateSourceFingerprint(AssetDetailsDto targetDetails, long contentLength) =>
        string.Concat(
            targetDetails.BundleId.Value,
            ":",
            targetDetails.AssetId.Value,
            ":",
            targetDetails.PathId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ":",
            contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
