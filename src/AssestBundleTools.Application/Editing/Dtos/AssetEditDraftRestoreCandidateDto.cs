using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Editing.Dtos;

public sealed record AssetEditDraftRestoreCandidateDto(
    AssetEditDraftReferenceDto Reference,
    AssetEditDraftRestoreStatus Status,
    AssetEditDraftDescriptorDto? DraftDescriptor = null,
    AppError? Diagnostic = null)
{
    public bool IsRestorable => Status == AssetEditDraftRestoreStatus.Restorable;
}
