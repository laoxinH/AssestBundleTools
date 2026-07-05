using AssestBundleTools.Application.Assets.Dtos;
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

        if (!draft.HasUnsavedChanges &&
            !AssetDocumentTextEditEffectiveReferenceResolver.DetailsPointToDraftRevision(
                details,
                draft.DraftId,
                draft.DraftMode,
                draft.Reference.DraftRevision))
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

        if (AssetDocumentTextEditEffectiveReferenceResolver.DetailsPointToDraft(
                details,
                draftReference.DraftId,
                draftReference.DraftMode))
        {
            assetTab.SetEditDraftReference(draftReference with
            {
                DraftMode = AssetDocumentTextEditEffectiveReferenceResolver.NormalizeDraftMode(details.TextEditDraftMode),
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
}
