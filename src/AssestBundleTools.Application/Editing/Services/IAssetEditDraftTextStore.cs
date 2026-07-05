using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 负责编辑草稿文本载荷的文件背书存取，不承载 UI 编辑状态。
/// </summary>
public interface IAssetEditDraftTextStore
{
    PreviewCacheTextChunkDto? LoadTextChunk(
        string workspaceKey,
        string draftId,
        string draftMode,
        int offset,
        int length);

    PreviewCacheTextLineRangeDto? LoadTextLines(
        string workspaceKey,
        string draftId,
        string draftMode,
        int startLineIndex,
        int lineCount);

    PreviewCacheTextSearchResultDto? SearchText(
        string workspaceKey,
        string draftId,
        string draftMode,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase);

    void SaveTextPayload(AssetEditDraftTextPayloadDto payload);

    AssetEditDraftTextPatchResultDto ApplyTextPatch(AssetEditDraftTextPatchDto patch);

    void Delete(string workspaceKey, string draftId, string draftMode);
}
