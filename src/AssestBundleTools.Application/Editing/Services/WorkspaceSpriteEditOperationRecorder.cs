using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 将已应用的 Sprite 编辑几何追加到工作区操作记录。
/// </summary>
public sealed class WorkspaceSpriteEditOperationRecorder
{
    public const string SpriteGeometryDraftMode = "sprite-geometry";

    private const string SourceKindDraft = "draft";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;

    public WorkspaceSpriteEditOperationRecorder(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
    }

    public void RecordSpriteEdit(
        ApplySpriteEditCommand command,
        AssetDetailsDto targetDetails,
        AssetSpriteEditAppliedResultDto result)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var sourceFingerprint = CreateGeometryFingerprint(result.Geometry);
        var appendRequest = new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.EditDraftApplied,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(targetDetails),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindDraft,
                DraftId: result.DraftId,
                DraftMode: result.DraftMode),
            PreviousState: new WorkspaceOperationStateDto(
                WasModified: targetDetails.IsModified,
                LastExtractedAtUtc: targetDetails.LastExtractedAtUtc,
                ReplacementSource: CreatePreviousReplacementSource(targetDetails),
                DraftId: ResolveEffectiveSpriteEditDraftId(targetDetails),
                SpriteEditGeometry: targetDetails.SpriteEditGeometry),
            Result: new WorkspaceOperationResultDto(
                DraftId: result.DraftId,
                AffectedBundleIds: [targetDetails.BundleId.Value],
                AffectedAssetIds: [targetDetails.AssetId.Value],
                SpriteEditGeometry: result.Geometry),
            CreatedAtUtc: result.AppliedAtUtc,
            AppliedAtUtc: result.AppliedAtUtc,
            CompletedAtUtc: result.AppliedAtUtc,
            SupersedesOperationId: null);

        var updatedChangeSet = _operationLogService.AppendOperation(
            UpsertDraftDescriptor(
                changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey),
                result,
                targetDetails,
                sourceFingerprint),
            appendRequest);
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
        AssetSpriteEditAppliedResultDto result,
        AssetDetailsDto targetDetails,
        string sourceFingerprint)
    {
        var descriptors = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .Where(descriptor =>
                !string.Equals(descriptor.DraftId, result.DraftId, StringComparison.Ordinal) ||
                !string.Equals(descriptor.DraftMode, result.DraftMode, StringComparison.Ordinal))
            .Append(new WorkspaceAssetEditDraftDescriptorDto(
                DraftId: result.DraftId,
                DraftMode: result.DraftMode,
                SourceFingerprint: sourceFingerprint,
                UpdatedAtUtc: result.AppliedAtUtc,
                SourceDescription: targetDetails.Name,
                Summary: FormatGeometrySummary(result.Geometry)))
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

    private static string? ResolveEffectiveSpriteEditDraftId(AssetDetailsDto details) =>
        details.ChangeRecords?
            .Where(static record => record.Kind == AssetChangeRecordKind.SpriteEdit && record.IsEffectiveEdit)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => record.DraftId)
            .FirstOrDefault(static draftId => !string.IsNullOrWhiteSpace(draftId));

    private static string CreateGeometryFingerprint(WorkspaceSpriteEditGeometryDto geometry)
    {
        var text = string.Join(
            "|",
            geometry.CanvasWidth.ToString("R", CultureInfo.InvariantCulture),
            geometry.CanvasHeight.ToString("R", CultureInfo.InvariantCulture),
            geometry.RegionX.ToString("R", CultureInfo.InvariantCulture),
            geometry.RegionY.ToString("R", CultureInfo.InvariantCulture),
            geometry.RegionWidth.ToString("R", CultureInfo.InvariantCulture),
            geometry.RegionHeight.ToString("R", CultureInfo.InvariantCulture),
            geometry.PivotX.ToString("R", CultureInfo.InvariantCulture),
            geometry.PivotY.ToString("R", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string FormatGeometrySummary(WorkspaceSpriteEditGeometryDto geometry) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"x={geometry.RegionX:0.##}, y={geometry.RegionY:0.##}, w={geometry.RegionWidth:0.##}, h={geometry.RegionHeight:0.##}, pivot=({geometry.PivotX:0.##}, {geometry.PivotY:0.##})");
}
