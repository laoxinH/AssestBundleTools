using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 从工作区操作事实解析 TextAsset 当前有效的文本编辑草稿引用。
/// </summary>
public sealed class WorkspaceTextEditEffectiveReferenceResolver
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly WorkspaceTextEditStateMaterializer _textEditStateMaterializer = new();

    public WorkspaceTextEditEffectiveReferenceResolver(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public WorkspaceTextEditEffectiveReferenceResolution ResolveFromStore(
        string? workspaceKey,
        string? workspaceDataRootPath,
        string? bundleId,
        string? assetId,
        AssetDetailsDto? fallbackDetails = null)
    {
        if (TryLoadChangeSet(workspaceKey, workspaceDataRootPath, out var changeSet))
        {
            return Resolve(changeSet, bundleId, assetId, fallbackDetails);
        }

        return ResolveFromDetails(fallbackDetails);
    }

    public WorkspaceTextEditEffectiveReferenceResolution Resolve(
        WorkspaceChangeSetDto? changeSet,
        string? bundleId,
        string? assetId,
        AssetDetailsDto? fallbackDetails = null)
    {
        if (!string.IsNullOrWhiteSpace(bundleId) &&
            !string.IsNullOrWhiteSpace(assetId) &&
            changeSet is not null)
        {
            var normalizedBundleId = bundleId.Trim();
            var normalizedAssetId = assetId.Trim();
            var effectiveTextEdit = _textEditStateMaterializer.FindEffectiveTextEdit(
                changeSet,
                normalizedBundleId,
                normalizedAssetId);
            if (effectiveTextEdit is not null)
            {
                return new WorkspaceTextEditEffectiveReferenceResolution(
                    new WorkspaceTextEditEffectiveReferenceDto(
                        effectiveTextEdit.DraftId,
                        NormalizeDraftMode(effectiveTextEdit.DraftMode),
                        effectiveTextEdit.DraftRevision,
                        effectiveTextEdit.AppliedAtUtc,
                        effectiveTextEdit.OperationId),
                    HasTextEditOperationFact: true,
                    WorkspaceTextEditEffectiveReferenceSource.OperationFact);
            }

            if (_textEditStateMaterializer.ContainsTextEditOperation(
                    changeSet,
                    normalizedBundleId,
                    normalizedAssetId))
            {
                return new WorkspaceTextEditEffectiveReferenceResolution(
                    Reference: null,
                    HasTextEditOperationFact: true,
                    WorkspaceTextEditEffectiveReferenceSource.OperationFact);
            }

            if (HasOperationHistory(changeSet))
            {
                // 操作事实流已经接管后，避免用尚未刷新的会话详情复活已删除的文本编辑。
                return new WorkspaceTextEditEffectiveReferenceResolution(
                    Reference: null,
                    HasTextEditOperationFact: false,
                    WorkspaceTextEditEffectiveReferenceSource.OperationFact);
            }
        }

        return ResolveFromDetails(fallbackDetails);
    }

    private bool TryLoadChangeSet(
        string? workspaceKey,
        string? workspaceDataRootPath,
        out WorkspaceChangeSetDto? changeSet)
    {
        changeSet = null;
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return false;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        try
        {
            var store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath.Trim());
            changeSet = store.Load(normalizedWorkspaceKey);
            return changeSet is not null &&
                   string.Equals(
                       changeSet.WorkspaceKey?.Trim(),
                       normalizedWorkspaceKey,
                       StringComparison.Ordinal);
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static WorkspaceTextEditEffectiveReferenceResolution ResolveFromDetails(
        AssetDetailsDto? details)
    {
        if (details is null || string.IsNullOrWhiteSpace(details.TextEditDraftId))
        {
            return new WorkspaceTextEditEffectiveReferenceResolution(
                Reference: null,
                HasTextEditOperationFact: false,
                WorkspaceTextEditEffectiveReferenceSource.None);
        }

        return new WorkspaceTextEditEffectiveReferenceResolution(
            new WorkspaceTextEditEffectiveReferenceDto(
                details.TextEditDraftId.Trim(),
                NormalizeDraftMode(details.TextEditDraftMode),
                details.TextEditDraftRevision,
                details.TextEditAppliedAtUtc),
            HasTextEditOperationFact: false,
            WorkspaceTextEditEffectiveReferenceSource.AssetDetailsProjection);
    }

    private static string NormalizeDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();

    private static bool HasOperationHistory(WorkspaceChangeSetDto changeSet) =>
        changeSet.OperationSequence > 0 ||
        (changeSet.Operations?.Count ?? 0) > 0;
}

public sealed record WorkspaceTextEditEffectiveReferenceResolution(
    WorkspaceTextEditEffectiveReferenceDto? Reference,
    bool HasTextEditOperationFact,
    WorkspaceTextEditEffectiveReferenceSource Source);

public sealed record WorkspaceTextEditEffectiveReferenceDto(
    string DraftId,
    string DraftMode,
    int? DraftRevision,
    DateTimeOffset? AppliedAtUtc,
    string? OperationId = null);

public enum WorkspaceTextEditEffectiveReferenceSource
{
    None,
    OperationFact,
    AssetDetailsProjection
}
