using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 从工作区操作事实流构建真实写包需要消费的对象级载荷。
/// </summary>
internal sealed class WorkspaceBundleAssetWritePlanBuilder
{
    private const string SourceKindFile = "file";
    private const string SourceKindWorkspace = "workspace";
    private const string SourceKindSpriteEdit = "sprite-edit";
    private const string SourceKindTextDraft = "text-draft";

    private readonly IWorkspaceDataStoreFactory? _workspaceDataStoreFactory;
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly AssestBundleTools.Domain.Workspace.WorkspaceId _workspaceId;
    private readonly WorkspaceReplacementStateMaterializer _replacementStateMaterializer = new();
    private readonly WorkspaceSpriteEditStateMaterializer _spriteEditStateMaterializer = new();
    private readonly WorkspaceTextEditStateMaterializer _textEditStateMaterializer = new();

    public WorkspaceBundleAssetWritePlanBuilder(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        AssestBundleTools.Domain.Workspace.WorkspaceId workspaceId,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceId = workspaceId;
        _workspaceDataStoreFactory = workspaceDataStoreFactory;
    }

    public IReadOnlyList<WorkspaceBundleAssetWriteItemDto> Build(
        PlannedWorkspaceBundleSave selectedPlan,
        string? workspaceKey,
        string? workspaceDataRootPath,
        IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> processorResults)
    {
        ArgumentNullException.ThrowIfNull(selectedPlan);

        var items = TryLoadChangeSet(workspaceKey, workspaceDataRootPath, out var changeSet) && changeSet is not null
            ? BuildFromChangeSet(selectedPlan, changeSet, workspaceKey, workspaceDataRootPath)
            : BuildFromChangeItems(selectedPlan);
        return ApplyProcessorOutputs(items, processorResults);
    }

    private bool TryLoadChangeSet(
        string? workspaceKey,
        string? workspaceDataRootPath,
        out WorkspaceChangeSetDto? changeSet)
    {
        changeSet = null;
        if (_workspaceDataStoreFactory is null ||
            string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return false;
        }

        try
        {
            var store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath.Trim());
            changeSet = store.Load(workspaceKey.Trim());
            return changeSet is not null;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private IReadOnlyList<WorkspaceBundleAssetWriteItemDto> BuildFromChangeSet(
        PlannedWorkspaceBundleSave selectedPlan,
        WorkspaceChangeSetDto changeSet,
        string? workspaceKey,
        string? workspaceDataRootPath)
    {
        var items = new List<WorkspaceBundleAssetWriteItemDto>();
        foreach (var replacement in _replacementStateMaterializer.Materialize(changeSet).EffectiveReplacements)
        {
            if (!TargetBelongsToBundle(replacement.Target, selectedPlan.BundleId) ||
                !TryCreateReplacementItem(replacement, out var item))
            {
                continue;
            }

            items.Add(item);
        }

        foreach (var edit in _spriteEditStateMaterializer.Materialize(changeSet).EffectiveEdits)
        {
            if (!TargetBelongsToBundle(edit.Target, selectedPlan.BundleId) ||
                !TryCreateSpriteEditItem(edit, out var item))
            {
                continue;
            }

            items.Add(item);
        }

        foreach (var edit in _textEditStateMaterializer.Materialize(changeSet).EffectiveEdits)
        {
            if (!TargetBelongsToBundle(edit.Target, selectedPlan.BundleId) ||
                !TryCreateTextEditItem(edit, workspaceKey, workspaceDataRootPath, out var item))
            {
                continue;
            }

            items.Add(item);
        }

        return NormalizeItems(items);
    }

    private IReadOnlyList<WorkspaceBundleAssetWriteItemDto> BuildFromChangeItems(
        PlannedWorkspaceBundleSave selectedPlan)
    {
        return selectedPlan.ChangeItems
            .Where(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourcePath))
            .Where(static item => !string.IsNullOrWhiteSpace(item.AssetId.Value))
            .Select(TryCreateFileReplacementItemFromChangeItem)
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();
    }

    private WorkspaceBundleAssetWriteItemDto? TryCreateFileReplacementItemFromChangeItem(WorkspaceChangeItemDto changeItem)
    {
        if (!_workspaceSessionRegistry.TryGetAssetDetails(_workspaceId, changeItem.BundleId, changeItem.AssetId, out var assetDetails) ||
            assetDetails is null ||
            string.IsNullOrWhiteSpace(assetDetails.SerializedFileName) ||
            assetDetails.PathId == 0)
        {
            return null;
        }

        return new WorkspaceBundleAssetWriteItemDto(
            BundleId: changeItem.BundleId,
            AssetId: changeItem.AssetId,
            AssetName: changeItem.AssetName,
            AssetType: changeItem.AssetType,
            AssetTypeDisplayName: assetDetails.AssetTypeDisplayName ?? changeItem.AssetType.ToString(),
            SerializedFileName: assetDetails.SerializedFileName,
            PathId: assetDetails.PathId,
            SourceKind: SourceKindFile,
            SourceFilePath: changeItem.FileReplacementSourcePath);
    }

    private bool TryCreateReplacementItem(
        AssestBundleTools.Application.Replacement.Dtos.WorkspaceEffectiveReplacementDto replacement,
        out WorkspaceBundleAssetWriteItemDto item)
    {
        item = default!;
        if (!TryCreateTargetIdentity(replacement.Target, out var targetIdentity))
        {
            return false;
        }

        var source = replacement.Source;
        var sourceKind = NormalizeSourceKind(source.SourceKind);
        if (sourceKind == SourceKindFile)
        {
            if (string.IsNullOrWhiteSpace(source.SourceFilePath))
            {
                return false;
            }

            item = new WorkspaceBundleAssetWriteItemDto(
                targetIdentity.BundleId,
                targetIdentity.AssetId,
                targetIdentity.AssetName,
                targetIdentity.AssetType,
                targetIdentity.AssetTypeDisplayName,
                targetIdentity.SerializedFileName,
                targetIdentity.PathId,
                SourceKindFile,
                SourceFilePath: source.SourceFilePath.Trim(),
                OperationId: replacement.OperationId,
                Sequence: replacement.Sequence);
            return true;
        }

        if (sourceKind == SourceKindWorkspace)
        {
            if (!TryParseBundleId(source.SourceBundleId, out var sourceBundleId) ||
                !TryParseAssetId(source.SourceAssetId, out var sourceAssetId) ||
                !_workspaceSessionRegistry.TryGetAssetDetails(_workspaceId, sourceBundleId, sourceAssetId, out var sourceDetails) ||
                sourceDetails is null ||
                string.IsNullOrWhiteSpace(sourceDetails.SerializedFileName) ||
                sourceDetails.PathId == 0)
            {
                return false;
            }

            item = new WorkspaceBundleAssetWriteItemDto(
                targetIdentity.BundleId,
                targetIdentity.AssetId,
                targetIdentity.AssetName,
                targetIdentity.AssetType,
                targetIdentity.AssetTypeDisplayName,
                targetIdentity.SerializedFileName,
                targetIdentity.PathId,
                SourceKindWorkspace,
                SourceBundleId: sourceBundleId,
                SourceAssetId: sourceAssetId,
                SourceSerializedFileName: sourceDetails.SerializedFileName,
                SourcePathId: sourceDetails.PathId,
                OperationId: replacement.OperationId,
                Sequence: replacement.Sequence);
            return true;
        }

        return false;
    }

    private static bool TryCreateSpriteEditItem(
        WorkspaceEffectiveSpriteEditDto edit,
        out WorkspaceBundleAssetWriteItemDto item)
    {
        item = default!;
        if (!TryCreateTargetIdentity(edit.Target, out var targetIdentity))
        {
            return false;
        }

        item = new WorkspaceBundleAssetWriteItemDto(
            targetIdentity.BundleId,
            targetIdentity.AssetId,
            targetIdentity.AssetName,
            targetIdentity.AssetType,
            targetIdentity.AssetTypeDisplayName,
            targetIdentity.SerializedFileName,
            targetIdentity.PathId,
            SourceKindSpriteEdit,
            SpriteEditGeometry: edit.Geometry,
            OperationId: edit.OperationId,
            Sequence: edit.Sequence);
        return true;
    }

    private static bool TryCreateTextEditItem(
        WorkspaceEffectiveTextEditDto edit,
        string? workspaceKey,
        string? workspaceDataRootPath,
        out WorkspaceBundleAssetWriteItemDto item)
    {
        item = default!;
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath) ||
            string.IsNullOrWhiteSpace(edit.DraftId) ||
            !TryCreateTargetIdentity(edit.Target, out var targetIdentity))
        {
            return false;
        }

        item = new WorkspaceBundleAssetWriteItemDto(
            targetIdentity.BundleId,
            targetIdentity.AssetId,
            targetIdentity.AssetName,
            targetIdentity.AssetType,
            targetIdentity.AssetTypeDisplayName,
            targetIdentity.SerializedFileName,
            targetIdentity.PathId,
            SourceKindTextDraft,
            DraftId: edit.DraftId,
            DraftMode: edit.DraftMode ?? WorkspaceTextEditOperationRecorder.TextDraftMode,
            WorkspaceKey: workspaceKey.Trim(),
            WorkspaceDataRootPath: workspaceDataRootPath.Trim(),
            OperationId: edit.OperationId,
            Sequence: edit.Sequence);
        return true;
    }

    private static IReadOnlyList<WorkspaceBundleAssetWriteItemDto> ApplyProcessorOutputs(
        IReadOnlyList<WorkspaceBundleAssetWriteItemDto> items,
        IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> processorResults)
    {
        if (items.Count == 0 || processorResults.Count == 0)
        {
            return items;
        }

        var processorResultByAsset = processorResults
            .Where(static result => result.Status == WorkspaceResourceProcessorStatusDto.Succeeded)
            .Where(static result => !string.IsNullOrWhiteSpace(result.OutputPath))
            .GroupBy(static result => new ProcessorOutputKey(result.BundleId.Value, result.AssetId.Value))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(static result => result.ResultPath, StringComparer.Ordinal)
                    .First());

        return items
            .Select(item =>
            {
                var key = new ProcessorOutputKey(item.BundleId.Value, item.AssetId.Value);
                if (!processorResultByAsset.TryGetValue(key, out var processorResult))
                {
                    return item;
                }

                return item with
                {
                    ProcessorOutputPath = processorResult.OutputPath,
                    ProcessorId = processorResult.ProcessorId,
                    ResourceKind = processorResult.ResourceKind
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceBundleAssetWriteItemDto> NormalizeItems(
        IEnumerable<WorkspaceBundleAssetWriteItemDto> items)
    {
        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item.SerializedFileName) && item.PathId != 0)
            .GroupBy(static item => string.Concat(item.BundleId.Value, "\n", item.AssetId.Value), StringComparer.Ordinal)
            .Select(static group => group
                .OrderBy(static item => GetSourcePriority(item.SourceKind))
                .ThenByDescending(static item => item.Sequence)
                .First())
            .OrderBy(static item => item.SerializedFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.PathId)
            .ToArray();
    }

    private static int GetSourcePriority(string sourceKind) =>
        NormalizeSourceKind(sourceKind) switch
        {
            SourceKindSpriteEdit => 0,
            SourceKindTextDraft => 0,
            SourceKindFile => 1,
            SourceKindWorkspace => 1,
            _ => 9
        };

    private static bool TargetBelongsToBundle(
        WorkspaceOperationTargetDto target,
        BundleId bundleId) =>
        string.Equals(target.BundleId, bundleId.Value, StringComparison.Ordinal);

    private static bool TryCreateTargetIdentity(
        WorkspaceOperationTargetDto? target,
        out TargetIdentity identity)
    {
        identity = default;
        if (target is null ||
            !TryParseBundleId(target.BundleId, out var bundleId) ||
            !TryParseAssetId(target.AssetId, out var assetId) ||
            string.IsNullOrWhiteSpace(target.SerializedFileName) ||
            !target.PathId.HasValue ||
            target.PathId.Value == 0)
        {
            return false;
        }

        identity = new TargetIdentity(
            bundleId,
            assetId,
            string.IsNullOrWhiteSpace(target.AssetName) ? assetId.Value : target.AssetName.Trim(),
            ParseAssetType(target.AssetType),
            target.AssetTypeDisplayName,
            target.SerializedFileName.Trim().Replace('\\', '/'),
            target.PathId.Value);
        return true;
    }

    private static bool TryParseBundleId(string? value, out BundleId bundleId)
    {
        if (!string.IsNullOrWhiteSpace(value) && BundleId.TryParse(value, out bundleId))
        {
            return true;
        }

        bundleId = default;
        return false;
    }

    private static bool TryParseAssetId(string? value, out AssetId assetId)
    {
        if (!string.IsNullOrWhiteSpace(value) && AssetId.TryParse(value, out assetId))
        {
            return true;
        }

        assetId = default;
        return false;
    }

    private static AssetType ParseAssetType(string? value) =>
        Enum.TryParse<AssetType>(value, ignoreCase: true, out var assetType)
            ? assetType
            : AssetType.Unknown;

    private static string NormalizeSourceKind(string? sourceKind) =>
        string.IsNullOrWhiteSpace(sourceKind)
            ? string.Empty
            : sourceKind.Trim().ToLowerInvariant();

    private readonly record struct TargetIdentity(
        BundleId BundleId,
        AssetId AssetId,
        string AssetName,
        AssetType AssetType,
        string? AssetTypeDisplayName,
        string SerializedFileName,
        long PathId);

    private readonly record struct ProcessorOutputKey(string BundleId, string AssetId);
}
