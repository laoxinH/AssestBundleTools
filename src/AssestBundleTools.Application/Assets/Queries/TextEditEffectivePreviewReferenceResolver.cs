using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 从操作事实流解析 TextAsset 当前有效文本编辑预览引用。
/// </summary>
internal sealed class TextEditEffectivePreviewReferenceResolver
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly WorkspaceTextEditStateMaterializer _textEditStateMaterializer = new();

    public TextEditEffectivePreviewReferenceResolver(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public bool TryResolve(
        GetAssetPreviewQuery query,
        AssetDetailsDto details,
        out TextEditEffectivePreviewReference? reference)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(details);

        if (TryLoadChangeSet(query, out var changeSet))
        {
            var effectiveTextEdit = _textEditStateMaterializer.FindEffectiveTextEdit(
                changeSet,
                query.BundleId.Value,
                query.AssetId.Value);
            if (effectiveTextEdit is not null)
            {
                reference = new TextEditEffectivePreviewReference(
                    effectiveTextEdit.DraftId,
                    effectiveTextEdit.DraftMode,
                    effectiveTextEdit.DraftRevision,
                    effectiveTextEdit.AppliedAtUtc);
                return true;
            }

            if (_textEditStateMaterializer.ContainsTextEditOperation(
                    changeSet,
                    query.BundleId.Value,
                    query.AssetId.Value))
            {
                reference = null;
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(details.TextEditDraftId))
        {
            reference = new TextEditEffectivePreviewReference(
                details.TextEditDraftId.Trim(),
                string.IsNullOrWhiteSpace(details.TextEditDraftMode)
                    ? WorkspaceTextEditOperationRecorder.TextDraftMode
                    : details.TextEditDraftMode.Trim(),
                details.TextEditDraftRevision,
                details.TextEditAppliedAtUtc);
            return true;
        }

        reference = null;
        return false;
    }

    private bool TryLoadChangeSet(
        GetAssetPreviewQuery query,
        out WorkspaceChangeSetDto? changeSet)
    {
        changeSet = null;
        if (string.IsNullOrWhiteSpace(query.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return false;
        }

        try
        {
            var store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(query.WorkspaceDataRootPath.Trim());
            changeSet = store.Load(query.WorkspaceKey.Trim());
            return changeSet is not null;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}

internal sealed record TextEditEffectivePreviewReference(
    string DraftId,
    string DraftMode,
    int? DraftRevision,
    DateTimeOffset? GeneratedAtUtc);
