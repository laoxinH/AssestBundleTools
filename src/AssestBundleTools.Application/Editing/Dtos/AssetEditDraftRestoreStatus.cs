namespace AssestBundleTools.Application.Editing.Dtos;

public enum AssetEditDraftRestoreStatus
{
    Restorable = 0,
    Missing = 1,
    SourceMismatch = 2,
    Expired = 3,
    InvalidReference = 4
}
