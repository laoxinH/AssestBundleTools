using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 负责预览缓存 Text payload 的按需读取和索引 sidecar 解析。
/// </summary>
internal sealed class JsonPreviewCacheTextAccessor
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonPreviewCacheTextAccessor(JsonWorkspaceStoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public PreviewCacheTextChunkDto? LoadTextChunk(
        string workspaceKey,
        string previewCacheKey,
        int offset,
        int length)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(previewCacheKey) ||
            offset < 0 ||
            length <= 0)
        {
            return null;
        }

        var payload = TryResolveTextPayload(workspaceKey, previewCacheKey);
        if (payload is null)
        {
            return null;
        }

        return JsonPreviewCacheTextPayloadReader.TryReadChunk(
            payload.Value.Descriptor,
            payload.Value.PayloadFilePath,
            payload.Value.PayloadLength,
            offset,
            length,
            LoadIndex(payload.Value));
    }

    public PreviewCacheTextLineRangeDto? LoadTextLines(
        string workspaceKey,
        string previewCacheKey,
        int startLineIndex,
        int lineCount)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(previewCacheKey) ||
            startLineIndex < 0 ||
            lineCount <= 0)
        {
            return null;
        }

        var payload = TryResolveTextPayload(workspaceKey, previewCacheKey);
        if (payload is null)
        {
            return null;
        }

        return JsonPreviewCacheTextPayloadReader.TryReadLines(
            payload.Value.Descriptor,
            payload.Value.PayloadFilePath,
            payload.Value.PayloadLength,
            startLineIndex,
            lineCount,
            LoadIndex(payload.Value));
    }

    public PreviewCacheTextSearchResultDto? SearchText(
        string workspaceKey,
        string previewCacheKey,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase)
    {
        if (string.IsNullOrEmpty(searchText) ||
            startLineIndex < 0 ||
            maxMatchCount <= 0)
        {
            return null;
        }

        var payload = TryResolveTextPayload(workspaceKey, previewCacheKey);
        if (payload is null)
        {
            return null;
        }

        return JsonPreviewCacheTextPayloadReader.TrySearch(
            payload.Value.Descriptor,
            payload.Value.PayloadFilePath,
            payload.Value.PayloadLength,
            searchText,
            startLineIndex,
            maxMatchCount,
            matchCase,
            LoadIndex(payload.Value));
    }

    private JsonPreviewCacheTextLineIndexDto? LoadIndex(PreviewCacheTextPayloadReference payload) =>
        JsonPreviewCacheTextLineIndex.Load(
            payload.Descriptor,
            payload.PayloadFilePath,
            payload.PayloadLength);

    private PreviewCacheTextPayloadReference? TryResolveTextPayload(
        string workspaceKey,
        string previewCacheKey)
    {
        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(workspaceKey, previewCacheKey);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null ||
            !string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payloadFilePath = TryResolvePayloadFilePath(
            workspaceKey,
            previewCacheKey,
            descriptor.RelativePayloadPath);
        return string.IsNullOrWhiteSpace(payloadFilePath) || !File.Exists(payloadFilePath)
            ? null
            : new PreviewCacheTextPayloadReference(descriptor, payloadFilePath, GetFileSize(payloadFilePath));
    }

    private string? TryResolvePayloadFilePath(
        string workspaceKey,
        string previewCacheKey,
        string relativePayloadPath)
    {
        try
        {
            return _storagePaths.GetPreviewPayloadFilePath(workspaceKey, previewCacheKey, relativePayloadPath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static long GetFileSize(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private readonly record struct PreviewCacheTextPayloadReference(
        PreviewCacheDescriptorDto Descriptor,
        string PayloadFilePath,
        long PayloadLength);
}
