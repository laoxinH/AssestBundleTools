namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// Identifies an edit draft without carrying draft content.
/// </summary>
public sealed record AssetEditDraftReferenceDto(
    string DraftId,
    string DraftMode,
    string? SourceFingerprint = null,
    string? SourceDescription = null);
