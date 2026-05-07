namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// Describes stored edit draft metadata without exposing draft content.
/// </summary>
public sealed record AssetEditDraftDescriptorDto(
    string WorkspaceKey,
    string DraftId,
    string DraftMode,
    string? SourceFingerprint,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null,
    string? SourceDescription = null,
    string? Summary = null);
