using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed record AssetEditDraftReferenceKey(
    string DraftId,
    string DraftMode,
    string? SourceFingerprint)
{
    public static AssetEditDraftReferenceKey? Create(AssetEditDraftReferenceDto? draftReference)
    {
        if (draftReference is null)
        {
            return null;
        }

        return Create(
            draftReference.DraftId,
            draftReference.DraftMode,
            draftReference.SourceFingerprint);
    }

    public static AssetEditDraftReferenceKey? Create(WorkspaceAssetEditDraftReferenceDto? draftReference)
    {
        if (draftReference is null)
        {
            return null;
        }

        return Create(
            draftReference.DraftId,
            draftReference.DraftMode,
            draftReference.SourceFingerprint);
    }

    private static AssetEditDraftReferenceKey? Create(
        string? draftId,
        string? draftMode,
        string? sourceFingerprint)
    {
        var normalizedDraftId = NormalizeRequired(draftId);
        var normalizedDraftMode = NormalizeRequired(draftMode);
        if (string.IsNullOrEmpty(normalizedDraftId) ||
            string.IsNullOrEmpty(normalizedDraftMode))
        {
            return null;
        }

        return new AssetEditDraftReferenceKey(
            normalizedDraftId,
            normalizedDraftMode,
            NormalizeOptional(sourceFingerprint));
    }

    private static string NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
