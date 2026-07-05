using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed record JsonAssetEditDraftTextPatchManifestDto(
    int SchemaVersion,
    string WorkspaceKey,
    string DraftId,
    string DraftMode,
    string ContentType,
    bool UsesPayloadBase,
    int BaseTextLength,
    long BaseContentLength,
    int BaseLineCount,
    string BaseContentSha256,
    WorkspaceAssetEditDraftSourceReferenceDto? SourceReference,
    IReadOnlyList<JsonAssetEditDraftTextPatchEntryDto> Patches,
    int TextLength,
    long ContentLength,
    int LineCount,
    string ContentSha256,
    DateTimeOffset UpdatedAtUtc,
    string? SourceFingerprint = null,
    string? SourceDescription = null,
    string? Summary = null);

internal sealed record JsonAssetEditDraftTextPatchEntryDto(
    int Sequence,
    IReadOnlyList<TextAssetEditDraftRangeEditDto> RangeEdits,
    TextAssetEditDraftPatchBaselineDto Baseline,
    DateTimeOffset AppliedAtUtc);
