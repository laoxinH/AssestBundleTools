using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 从资产详情投影解析当前有效的文本编辑草稿引用。
/// </summary>
internal static class AssetDocumentTextEditEffectiveReferenceResolver
{
    public static AssetDocumentEditDraftReference? CreateAppliedReference(
        AssetDetailsDto details,
        AssetDocumentEditDraftReference? existingReference = null)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (!TryGetEffectiveDraftIdentity(details, out var draftId, out var draftMode))
        {
            return null;
        }

        var matchingExistingReference = existingReference is not null &&
            PointsToDraft(existingReference, draftId, draftMode)
                ? existingReference
                : null;

        return new AssetDocumentEditDraftReference(
            draftId,
            draftMode,
            matchingExistingReference?.SourceFingerprint,
            matchingExistingReference?.SourceDescription,
            IsAppliedBaseline: true,
            DraftRevision: details.TextEditDraftRevision);
    }

    public static bool DetailsPointToDraft(
        AssetDetailsDto details,
        AssetDocumentEditDraftReference draftReference)
    {
        ArgumentNullException.ThrowIfNull(draftReference);

        return DetailsPointToDraft(details, draftReference.DraftId, draftReference.DraftMode);
    }

    public static bool DetailsPointToDraft(
        AssetDetailsDto details,
        string draftId,
        string draftMode)
    {
        ArgumentNullException.ThrowIfNull(details);

        return TryGetEffectiveDraftIdentity(details, out var effectiveDraftId, out var effectiveDraftMode) &&
            string.Equals(effectiveDraftId, draftId, StringComparison.Ordinal) &&
            string.Equals(effectiveDraftMode, NormalizeDraftMode(draftMode), StringComparison.Ordinal);
    }

    public static bool DetailsPointToDraftRevision(
        AssetDetailsDto details,
        string draftId,
        string draftMode,
        int? draftRevision) =>
        DetailsPointToDraft(details, draftId, draftMode) &&
        details.TextEditDraftRevision == draftRevision;

    public static string NormalizeDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();

    private static bool TryGetEffectiveDraftIdentity(
        AssetDetailsDto details,
        out string draftId,
        out string draftMode)
    {
        if (string.IsNullOrWhiteSpace(details.TextEditDraftId))
        {
            draftId = string.Empty;
            draftMode = string.Empty;
            return false;
        }

        draftId = details.TextEditDraftId.Trim();
        draftMode = NormalizeDraftMode(details.TextEditDraftMode);
        return true;
    }

    private static bool PointsToDraft(
        AssetDocumentEditDraftReference draftReference,
        string draftId,
        string draftMode) =>
        string.Equals(draftReference.DraftId, draftId, StringComparison.Ordinal) &&
        string.Equals(
            NormalizeDraftMode(draftReference.DraftMode),
            draftMode,
            StringComparison.Ordinal);
}
