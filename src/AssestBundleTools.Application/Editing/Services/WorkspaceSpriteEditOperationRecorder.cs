using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
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
    private const string SourceKindFile = "file";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;
    private readonly WorkspaceReplacementStateMaterializer _replacementStateMaterializer;

    public WorkspaceSpriteEditOperationRecorder(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService,
        WorkspaceReplacementStateMaterializer? replacementStateMaterializer = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
        _replacementStateMaterializer = replacementStateMaterializer ?? new WorkspaceReplacementStateMaterializer();
    }

    public void RecordSpriteEdit(
        ApplySpriteEditCommand command,
        AssetDetailsDto targetDetails,
        AssetSpriteEditAppliedResultDto result,
        AssetDetailsDto? associatedFileReplacementTargetDetails = null,
        AssetFileReplacementResultDto? associatedFileReplacementResult = null)
    {
        if (!TryLoadChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath, out var workspaceKey, out var store, out var changeSet))
        {
            return;
        }

        var baseChangeSet = UpsertDraftDescriptor(
            changeSet ?? WorkspaceChangeSetDto.Empty(workspaceKey),
            result,
            targetDetails,
            CreateGeometryFingerprint(result.Geometry));
        var shouldRecordAssociatedReplacement =
            associatedFileReplacementTargetDetails is not null &&
            associatedFileReplacementResult is not null;
        var relationId = shouldRecordAssociatedReplacement
            ? CreateRelationId()
            : null;
        var textureReplacementOperationId = shouldRecordAssociatedReplacement
            ? CreateOperationId("op_texture_replace")
            : null;
        var spriteEditOperationId = shouldRecordAssociatedReplacement
            ? CreateOperationId("op_sprite_edit")
            : null;
        var updatedChangeSet = baseChangeSet;

        if (shouldRecordAssociatedReplacement)
        {
            updatedChangeSet = _operationLogService.AppendOperation(
                updatedChangeSet,
                CreateAssociatedFileReplacementAppendRequest(
                    workspaceKey,
                    command,
                    targetDetails,
                    associatedFileReplacementTargetDetails!,
                    associatedFileReplacementResult!,
                    relationId!,
                    textureReplacementOperationId!,
                    spriteEditOperationId!));
        }

        updatedChangeSet = _operationLogService.AppendOperation(
            updatedChangeSet,
            CreateSpriteEditAppendRequest(
                workspaceKey,
                targetDetails,
                result,
                relationId,
                spriteEditOperationId,
                associatedFileReplacementTargetDetails,
                textureReplacementOperationId));

        store.Save(updatedChangeSet);
    }

    private WorkspaceOperationAppendRequestDto CreateAssociatedFileReplacementAppendRequest(
        string workspaceKey,
        ApplySpriteEditCommand command,
        AssetDetailsDto spriteDetails,
        AssetDetailsDto textureDetails,
        AssetFileReplacementResultDto replacementResult,
        string relationId,
        string textureReplacementOperationId,
        string spriteEditOperationId)
    {
        return new WorkspaceOperationAppendRequestDto(
            WorkspaceKey: workspaceKey,
            OperationKind: WorkspaceOperationKind.ReplaceFromFile,
            Status: WorkspaceOperationStatus.Applied,
            Target: CreateTarget(textureDetails),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: SourceKindFile,
                SourceFilePath: replacementResult.SourceFilePath,
                SourceFileName: replacementResult.SourceFileName,
                SourceAssetType: replacementResult.SourceAssetType.ToString()),
            PreviousState: CreatePreviousState(textureDetails),
            Result: new WorkspaceOperationResultDto(
                ReplacementSourceKind: SourceKindFile,
                AffectedBundleIds: CreateAffectedBundleIds(textureDetails, spriteDetails),
                AffectedAssetIds: CreateAffectedAssetIds(textureDetails, spriteDetails)),
            OperationId: textureReplacementOperationId,
            CreatedAtUtc: replacementResult.ReplacedAtUtc,
            AppliedAtUtc: replacementResult.ReplacedAtUtc,
            CompletedAtUtc: replacementResult.ReplacedAtUtc,
            BatchId: relationId,
            SupersedesOperationId: _replacementStateMaterializer.FindLatestEffectiveReplacementOperationId(
                LoadCurrentChangeSet(command.WorkspaceKey, command.WorkspaceDataRootPath),
                textureDetails.BundleId.Value,
                textureDetails.AssetId.Value),
            Relations:
            [
                new WorkspaceOperationRelationDto(
                    RelationId: relationId,
                    RelationKind: WorkspaceOperationRelationNames.LinkedResourceOperation,
                    Role: WorkspaceOperationRelationNames.SubjectRole,
                    RelatedTarget: CreateTarget(spriteDetails),
                    RelatedOperationId: spriteEditOperationId)
            ]);
    }

    private static WorkspaceOperationAppendRequestDto CreateSpriteEditAppendRequest(
        string workspaceKey,
        AssetDetailsDto targetDetails,
        AssetSpriteEditAppliedResultDto result,
        string? relationId,
        string? spriteEditOperationId,
        AssetDetailsDto? associatedFileReplacementTargetDetails,
        string? textureReplacementOperationId)
    {
        IReadOnlyList<WorkspaceOperationRelationDto>? relations =
            !string.IsNullOrWhiteSpace(relationId) &&
            associatedFileReplacementTargetDetails is not null &&
            !string.IsNullOrWhiteSpace(textureReplacementOperationId)
            ? new[]
            {
                new WorkspaceOperationRelationDto(
                    RelationId: relationId!,
                    RelationKind: WorkspaceOperationRelationNames.LinkedResourceOperation,
                    Role: WorkspaceOperationRelationNames.OwnerRole,
                    RelatedTarget: CreateTarget(associatedFileReplacementTargetDetails),
                    RelatedOperationId: textureReplacementOperationId)
            }
            : null;

        return new WorkspaceOperationAppendRequestDto(
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
                AffectedBundleIds: associatedFileReplacementTargetDetails is null
                    ? [targetDetails.BundleId.Value]
                    : CreateAffectedBundleIds(targetDetails, associatedFileReplacementTargetDetails),
                AffectedAssetIds: associatedFileReplacementTargetDetails is null
                    ? [targetDetails.AssetId.Value]
                    : CreateAffectedAssetIds(targetDetails, associatedFileReplacementTargetDetails),
                SpriteEditGeometry: result.Geometry),
            OperationId: spriteEditOperationId,
            CreatedAtUtc: result.AppliedAtUtc,
            AppliedAtUtc: result.AppliedAtUtc,
            CompletedAtUtc: result.AppliedAtUtc,
            BatchId: relationId,
            SupersedesOperationId: null,
            Relations: relations);
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

    private static WorkspaceOperationStateDto CreatePreviousState(AssetDetailsDto details) =>
        new(
            WasModified: details.IsModified,
            LastExtractedAtUtc: details.LastExtractedAtUtc,
            ReplacementSource: CreatePreviousReplacementSource(details),
            DraftId: ResolveEffectiveSpriteEditDraftId(details),
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

    private WorkspaceChangeSetDto? LoadCurrentChangeSet(
        string? workspaceKey,
        string? workspaceDataRootPath)
    {
        if (!TryLoadChangeSet(workspaceKey, workspaceDataRootPath, out _, out _, out var changeSet))
        {
            return null;
        }

        return changeSet;
    }

    private static IReadOnlyList<string> CreateAffectedBundleIds(
        AssetDetailsDto first,
        AssetDetailsDto second) =>
        new[] { first.BundleId.Value, second.BundleId.Value }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> CreateAffectedAssetIds(
        AssetDetailsDto first,
        AssetDetailsDto second) =>
        new[] { first.AssetId.Value, second.AssetId.Value }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string CreateRelationId() => $"rel_{Guid.NewGuid():N}";

    private static string CreateOperationId(string prefix)
    {
        var ticks = DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}_{ticks}_{suffix}";
    }
}
