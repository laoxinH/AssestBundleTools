namespace AssestBundleTools.Application.Editing.Dtos;

public enum AssetEditDraftTextPatchStatus
{
    Applied,
    NotFound,
    BaselineMismatch,
    RangeInvalid,
    LengthExceeded
}

/// <summary>
/// 表示编辑草稿文本范围补丁的落盘结果。
/// </summary>
public sealed record AssetEditDraftTextPatchResultDto(
    AssetEditDraftTextPatchStatus Status,
    int TextLength = 0,
    long ContentLength = 0,
    int? Revision = null,
    int? InvalidRangeStart = null,
    int? InvalidRangeLength = null)
{
    public static AssetEditDraftTextPatchResultDto Applied(int textLength, long contentLength, int revision) =>
        new(AssetEditDraftTextPatchStatus.Applied, textLength, contentLength, revision);

    public static AssetEditDraftTextPatchResultDto NotFound() =>
        new(AssetEditDraftTextPatchStatus.NotFound);

    public static AssetEditDraftTextPatchResultDto BaselineMismatch() =>
        new(AssetEditDraftTextPatchStatus.BaselineMismatch);

    public static AssetEditDraftTextPatchResultDto RangeInvalid(int start, int length) =>
        new(AssetEditDraftTextPatchStatus.RangeInvalid, InvalidRangeStart: start, InvalidRangeLength: length);

    public static AssetEditDraftTextPatchResultDto LengthExceeded() =>
        new(AssetEditDraftTextPatchStatus.LengthExceeded);
}
