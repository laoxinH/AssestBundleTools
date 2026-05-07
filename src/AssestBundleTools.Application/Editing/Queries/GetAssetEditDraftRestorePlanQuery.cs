using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Application.Editing.Queries;

public sealed record GetAssetEditDraftRestorePlanQuery(
    string WorkspaceKey,
    IReadOnlyList<AssetEditDraftReferenceDto> DraftReferences,
    DateTimeOffset? CurrentTimeUtc = null);
