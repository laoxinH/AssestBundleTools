namespace AssestBundleTools.Application.Editing.Dtos;

public sealed record AssetEditDraftRestorePlanDto(
    string WorkspaceKey,
    int TotalReferenceCount,
    int RestorableCount,
    int MissingCount,
    int SourceMismatchCount,
    int ExpiredCount,
    int InvalidReferenceCount,
    IReadOnlyList<AssetEditDraftRestoreCandidateDto> Candidates)
{
    public bool HasUnrestorableCandidates =>
        MissingCount > 0 ||
        SourceMismatchCount > 0 ||
        ExpiredCount > 0 ||
        InvalidReferenceCount > 0;
}
