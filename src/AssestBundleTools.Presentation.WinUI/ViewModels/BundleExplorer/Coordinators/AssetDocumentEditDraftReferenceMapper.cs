using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class AssetDocumentEditDraftReferenceMapper
{
    public static AssetDocumentEditDraftReference? ToViewModel(
        WorkspaceAssetEditDraftReferenceDto? draftReference)
    {
        if (draftReference is null ||
            string.IsNullOrWhiteSpace(draftReference.DraftId) ||
            string.IsNullOrWhiteSpace(draftReference.DraftMode))
        {
            return null;
        }

        return new AssetDocumentEditDraftReference(
            draftReference.DraftId.Trim(),
            draftReference.DraftMode.Trim(),
            Normalize(draftReference.SourceFingerprint),
            Normalize(draftReference.SourceDescription),
            draftReference.IsAppliedBaseline,
            draftReference.DraftRevision);
    }

    public static WorkspaceAssetEditDraftReferenceDto? ToDto(
        AssetDocumentEditDraftReference? draftReference)
    {
        if (draftReference is null ||
            string.IsNullOrWhiteSpace(draftReference.DraftId) ||
            string.IsNullOrWhiteSpace(draftReference.DraftMode))
        {
            return null;
        }

        return new WorkspaceAssetEditDraftReferenceDto(
            draftReference.DraftId.Trim(),
            draftReference.DraftMode.Trim(),
            Normalize(draftReference.SourceFingerprint),
            Normalize(draftReference.SourceDescription),
            draftReference.IsAppliedBaseline,
            draftReference.DraftRevision);
    }

    public static AssetEditDraftReferenceDto ToRestorePlanDto(
        WorkspaceAssetEditDraftReferenceDto draftReference)
    {
        ArgumentNullException.ThrowIfNull(draftReference);

        return new AssetEditDraftReferenceDto(
            draftReference.DraftId,
            draftReference.DraftMode,
            draftReference.SourceFingerprint,
            draftReference.SourceDescription);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
