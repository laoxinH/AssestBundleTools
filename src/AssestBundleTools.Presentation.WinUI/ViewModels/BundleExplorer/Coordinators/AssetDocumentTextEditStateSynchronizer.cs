using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 同步资产详情投影和资产标签中的文本编辑草稿页面状态。
/// </summary>
internal static class AssetDocumentTextEditStateSynchronizer
{
    public static void Align(AssetDocumentTabViewModel assetTab, AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);

        AlignEditDraftReferenceWithDetails(assetTab, details);

        var draft = assetTab.TextEditDraft;
        if (draft is null ||
            !draft.IsAppliedBaseline)
        {
            return;
        }

        var isSameDraft = DoesDetailsPointToDraft(details, draft.DraftId, draft.DraftMode);
        if (!draft.HasUnsavedChanges &&
            (!isSameDraft ||
            draft.Reference.DraftRevision != details.TextEditDraftRevision))
        {
            assetTab.ExitTextEditMode(clearDraftReference: false);
        }
    }

    private static void AlignEditDraftReferenceWithDetails(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details)
    {
        var draftReference = assetTab.EditDraftReference;
        if (draftReference is null)
        {
            return;
        }

        if (DoesDetailsPointToDraft(details, draftReference.DraftId, draftReference.DraftMode))
        {
            assetTab.SetEditDraftReference(draftReference with
            {
                DraftMode = NormalizeTextDraftMode(details.TextEditDraftMode),
                IsAppliedBaseline = true,
                DraftRevision = details.TextEditDraftRevision
            });
            return;
        }

        if (draftReference.IsAppliedBaseline ||
            assetTab.TextEditDraft?.IsAppliedBaseline == true)
        {
            assetTab.ClearEditDraftReference();
        }
    }

    private static bool DoesDetailsPointToDraft(
        AssetDetailsDto details,
        string draftId,
        string draftMode) =>
        !string.IsNullOrWhiteSpace(details.TextEditDraftId) &&
        string.Equals(details.TextEditDraftId.Trim(), draftId, StringComparison.Ordinal) &&
        string.Equals(
            NormalizeTextDraftMode(details.TextEditDraftMode),
            NormalizeTextDraftMode(draftMode),
            StringComparison.Ordinal);

    private static string NormalizeTextDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();
}
