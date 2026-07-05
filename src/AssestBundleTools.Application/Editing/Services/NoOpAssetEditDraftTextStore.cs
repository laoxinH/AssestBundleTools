using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 用于未配置草稿文本载荷存储时的空实现。
/// </summary>
public sealed class NoOpAssetEditDraftTextStore : IAssetEditDraftTextStore
{
    public static NoOpAssetEditDraftTextStore Instance { get; } = new();

    private NoOpAssetEditDraftTextStore()
    {
    }

    public PreviewCacheTextChunkDto? LoadTextChunk(
        string workspaceKey,
        string draftId,
        string draftMode,
        int offset,
        int length,
        int? revision = null) => null;

    public PreviewCacheTextLineRangeDto? LoadTextLines(
        string workspaceKey,
        string draftId,
        string draftMode,
        int startLineIndex,
        int lineCount,
        int? revision = null) => null;

    public PreviewCacheTextSearchResultDto? SearchText(
        string workspaceKey,
        string draftId,
        string draftMode,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase,
        int? revision = null) => null;

    public void SaveTextPayload(AssetEditDraftTextPayloadDto payload)
    {
    }

    public AssetEditDraftTextPatchResultDto ApplyTextPatch(AssetEditDraftTextPatchDto patch) =>
        AssetEditDraftTextPatchResultDto.NotFound();

    public void Delete(string workspaceKey, string draftId, string draftMode)
    {
    }
}
