using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 将文本编辑草稿载荷写入工作区 staging/drafts，并通过 Application 查询按需读取。
/// </summary>
public sealed class JsonAssetEditDraftTextStore : IAssetEditDraftTextStore
{
    private const string DescriptorFileName = "text-draft.json";
    private const string PayloadFileName = "text.txt";
    private const string GeneratorVersion = "edit-draft-text-v1";

    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonAssetEditDraftTextStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public PreviewCacheTextChunkDto? LoadTextChunk(
        string workspaceKey,
        string draftId,
        string draftMode,
        int offset,
        int length)
    {
        if (offset < 0 || length <= 0)
        {
            return null;
        }

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode);
        return payload is null
            ? null
            : JsonPreviewCacheTextPayloadReader.TryReadChunk(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath,
                payload.Value.PayloadLength,
                offset,
                length,
                LoadIndex(payload.Value));
    }

    public PreviewCacheTextLineRangeDto? LoadTextLines(
        string workspaceKey,
        string draftId,
        string draftMode,
        int startLineIndex,
        int lineCount)
    {
        if (startLineIndex < 0 || lineCount <= 0)
        {
            return null;
        }

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode);
        return payload is null
            ? null
            : JsonPreviewCacheTextPayloadReader.TryReadLines(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath,
                payload.Value.PayloadLength,
                startLineIndex,
                lineCount,
                LoadIndex(payload.Value));
    }

    public PreviewCacheTextSearchResultDto? SearchText(
        string workspaceKey,
        string draftId,
        string draftMode,
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

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode);
        return payload is null
            ? null
            : JsonPreviewCacheTextPayloadReader.TrySearch(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath,
                payload.Value.PayloadLength,
                searchText,
                startLineIndex,
                maxMatchCount,
                matchCase,
                LoadIndex(payload.Value));
    }

    public void SaveTextPayload(AssetEditDraftTextPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.DraftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.DraftMode);

        var descriptor = CreateDescriptor(payload);
        var draftDirectoryPath = GetDraftDirectoryPath(
            payload.WorkspaceKey,
            payload.DraftId,
            payload.DraftMode);
        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);
        var content = Encoding.UTF8.GetBytes(NormalizeTextContent(payload.TextContent ?? string.Empty));

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            descriptorFilePath,
            descriptor,
            JsonWorkspaceSerialization.SerializerOptions);
        JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, content);
        JsonPreviewCacheTextLineIndex.Save(descriptor, payloadFilePath, content);
    }

    public void Delete(string workspaceKey, string draftId, string draftMode)
    {
        var draftDirectoryPath = TryGetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        if (string.IsNullOrWhiteSpace(draftDirectoryPath))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(draftDirectoryPath);
    }

    private JsonPreviewCacheTextLineIndexDto? LoadIndex(DraftTextPayloadReference payload) =>
        JsonPreviewCacheTextLineIndex.Load(
            payload.Descriptor,
            payload.PayloadFilePath,
            payload.PayloadLength);

    private DraftTextPayloadReference? TryResolvePayload(
        string workspaceKey,
        string draftId,
        string draftMode)
    {
        var draftDirectoryPath = TryGetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        if (string.IsNullOrWhiteSpace(draftDirectoryPath))
        {
            return null;
        }

        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null ||
            !string.Equals(descriptor.WorkspaceKey, workspaceKey.Trim(), StringComparison.Ordinal) ||
            !string.Equals(descriptor.PreviewCacheKey, CreatePreviewCacheKey(draftId, draftMode), StringComparison.Ordinal) ||
            !string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(payloadFilePath))
        {
            return null;
        }

        return new DraftTextPayloadReference(
            descriptor,
            payloadFilePath,
            GetFileSize(payloadFilePath));
    }

    private PreviewCacheDescriptorDto CreateDescriptor(AssetEditDraftTextPayloadDto payload)
    {
        var normalizedText = NormalizeTextContent(payload.TextContent ?? string.Empty);
        var normalizedContentType = string.IsNullOrWhiteSpace(payload.ContentType)
            ? "text/plain; charset=utf-8"
            : payload.ContentType.Trim();
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);

        return new PreviewCacheDescriptorDto(
            WorkspaceKey: payload.WorkspaceKey.Trim(),
            PreviewCacheKey: CreatePreviewCacheKey(payload.DraftId, payload.DraftMode),
            BundleCacheKey: $"draft:{payload.DraftId.Trim()}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: normalizedContentType,
            RelativePayloadPath: PayloadFileName,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: $"draft:{payload.DraftId.Trim()}:{payload.DraftMode.Trim()}",
                FileSizeInBytes: Encoding.UTF8.GetByteCount(normalizedText),
                LastWriteTimeUtc: payload.UpdatedAtUtc,
                ContentHash: payload.SourceFingerprint),
            GeneratedAtUtc: payload.UpdatedAtUtc,
            GeneratorVersion: GeneratorVersion,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata),
            PreferredDisplaySizeInBytes: Encoding.UTF8.GetByteCount(display.TextContent));
    }

    private string GetDraftDirectoryPath(
        string workspaceKey,
        string draftId,
        string draftMode)
    {
        return _storagePaths.GetAssetEditDraftDirectoryPath(
            workspaceKey.Trim(),
            draftId.Trim(),
            draftMode.Trim());
    }

    private string? TryGetDraftDirectoryPath(
        string workspaceKey,
        string draftId,
        string draftMode)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(draftId) ||
            string.IsNullOrWhiteSpace(draftMode))
        {
            return null;
        }

        try
        {
            return GetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string CreatePreviewCacheKey(string draftId, string draftMode) =>
        string.Concat("draft.", draftMode.Trim(), ".", draftId.Trim());

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

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

    private readonly record struct DraftTextPayloadReference(
        PreviewCacheDescriptorDto Descriptor,
        string PayloadFilePath,
        long PayloadLength);
}
