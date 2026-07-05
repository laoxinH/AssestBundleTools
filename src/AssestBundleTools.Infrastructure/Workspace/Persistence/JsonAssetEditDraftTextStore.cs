using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, object> DraftWriteLocks = new(StringComparer.OrdinalIgnoreCase);

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

        lock (GetDraftWriteLock(draftDirectoryPath))
        {
            JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
                descriptorFilePath,
                descriptor,
                JsonWorkspaceSerialization.SerializerOptions);
            JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, content);
            JsonPreviewCacheTextLineIndex.Save(descriptor, payloadFilePath, content);
        }
    }

    public AssetEditDraftTextPatchResultDto ApplyTextPatch(AssetEditDraftTextPatchDto patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.DraftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.DraftMode);

        var draftDirectoryPath = GetDraftDirectoryPath(
            patch.WorkspaceKey,
            patch.DraftId,
            patch.DraftMode);
        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);

        lock (GetDraftWriteLock(draftDirectoryPath))
        {
            var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
                descriptorFilePath,
                JsonWorkspaceSerialization.SerializerOptions);
            var payloadBytes = JsonWorkspaceFileStorageHelper.TryReadBytes(payloadFilePath);
            if (descriptor is null ||
                payloadBytes is null ||
                !IsMatchingPayloadDescriptor(descriptor, patch))
            {
                return AssetEditDraftTextPatchResultDto.NotFound();
            }

            var currentText = NormalizeTextContent(Encoding.UTF8.GetString(payloadBytes));
            if (!TextAssetEditDraftPatchBaseline.Matches(currentText, patch.Baseline))
            {
                return AssetEditDraftTextPatchResultDto.BaselineMismatch();
            }

            var appliedTextResult = ApplyRangeEdits(
                currentText,
                patch.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>(),
                patch.MaxTextLength);
            if (appliedTextResult.Status != AssetEditDraftTextPatchStatus.Applied ||
                appliedTextResult.TextContent is null)
            {
                return appliedTextResult.ToPatchResult();
            }

            var patchedText = appliedTextResult.TextContent;
            var patchedContent = Encoding.UTF8.GetBytes(patchedText);
            var patchedDescriptor = CreateDescriptor(
                patch.WorkspaceKey,
                patch.DraftId,
                patch.DraftMode,
                patch.ContentType,
                patchedText,
                patch.UpdatedAtUtc,
                patch.SourceFingerprint,
                patch.SourceDescription,
                patch.Summary);
            JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
                descriptorFilePath,
                patchedDescriptor,
                JsonWorkspaceSerialization.SerializerOptions);
            JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, patchedContent);
            JsonPreviewCacheTextLineIndex.Save(patchedDescriptor, payloadFilePath, patchedContent);
            return AssetEditDraftTextPatchResultDto.Applied(patchedText.Length, patchedContent.Length);
        }
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

    private PreviewCacheDescriptorDto CreateDescriptor(AssetEditDraftTextPayloadDto payload) =>
        CreateDescriptor(
            payload.WorkspaceKey,
            payload.DraftId,
            payload.DraftMode,
            payload.ContentType,
            NormalizeTextContent(payload.TextContent ?? string.Empty),
            payload.UpdatedAtUtc,
            payload.SourceFingerprint,
            payload.SourceDescription,
            payload.Summary);

    private PreviewCacheDescriptorDto CreateDescriptor(
        string workspaceKey,
        string draftId,
        string draftMode,
        string contentType,
        string normalizedText,
        DateTimeOffset updatedAtUtc,
        string? sourceFingerprint,
        string? sourceDescription,
        string? summary)
    {
        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "text/plain; charset=utf-8"
            : contentType.Trim();
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);

        return new PreviewCacheDescriptorDto(
            WorkspaceKey: workspaceKey.Trim(),
            PreviewCacheKey: CreatePreviewCacheKey(draftId, draftMode),
            BundleCacheKey: $"draft:{draftId.Trim()}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: normalizedContentType,
            RelativePayloadPath: PayloadFileName,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: $"draft:{draftId.Trim()}:{draftMode.Trim()}",
                FileSizeInBytes: Encoding.UTF8.GetByteCount(normalizedText),
                LastWriteTimeUtc: updatedAtUtc,
                ContentHash: sourceFingerprint),
            GeneratedAtUtc: updatedAtUtc,
            GeneratorVersion: GeneratorVersion,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata),
            PreferredDisplaySizeInBytes: Encoding.UTF8.GetByteCount(display.TextContent));
    }

    private static bool IsMatchingPayloadDescriptor(
        PreviewCacheDescriptorDto descriptor,
        AssetEditDraftTextPatchDto patch) =>
        string.Equals(descriptor.WorkspaceKey, patch.WorkspaceKey.Trim(), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewCacheKey, CreatePreviewCacheKey(patch.DraftId, patch.DraftMode), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase);

    private static TextPatchApplyResult ApplyRangeEdits(
        string currentText,
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        int maxTextLength)
    {
        if (currentText.Length > maxTextLength)
        {
            return TextPatchApplyResult.LengthExceeded();
        }

        var builder = new StringBuilder(currentText);
        var orderedEdits = rangeEdits
            .OrderByDescending(static edit => edit.Start)
            .ToArray();
        var nextStart = currentText.Length;

        foreach (var edit in orderedEdits)
        {
            if (edit.Start > currentText.Length ||
                edit.Length > currentText.Length - edit.Start ||
                edit.Start + edit.Length > nextStart)
            {
                return TextPatchApplyResult.RangeInvalid(edit.Start, edit.Length);
            }

            builder.Remove(edit.Start, edit.Length);
            builder.Insert(edit.Start, NormalizeTextContent(edit.Text));
            nextStart = edit.Start;
            if (builder.Length > maxTextLength)
            {
                return TextPatchApplyResult.LengthExceeded();
            }
        }

        return TextPatchApplyResult.Applied(builder.ToString());
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

    private static object GetDraftWriteLock(string draftDirectoryPath) =>
        DraftWriteLocks.GetOrAdd(Path.GetFullPath(draftDirectoryPath), static _ => new object());

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

    private sealed record TextPatchApplyResult(
        AssetEditDraftTextPatchStatus Status,
        string? TextContent = null,
        int? InvalidRangeStart = null,
        int? InvalidRangeLength = null)
    {
        public static TextPatchApplyResult Applied(string textContent) =>
            new(AssetEditDraftTextPatchStatus.Applied, textContent);

        public static TextPatchApplyResult RangeInvalid(int start, int length) =>
            new(AssetEditDraftTextPatchStatus.RangeInvalid, InvalidRangeStart: start, InvalidRangeLength: length);

        public static TextPatchApplyResult LengthExceeded() =>
            new(AssetEditDraftTextPatchStatus.LengthExceeded);

        public AssetEditDraftTextPatchResultDto ToPatchResult() =>
            Status switch
            {
                AssetEditDraftTextPatchStatus.RangeInvalid => AssetEditDraftTextPatchResultDto.RangeInvalid(
                    InvalidRangeStart ?? -1,
                    InvalidRangeLength ?? -1),
                AssetEditDraftTextPatchStatus.LengthExceeded => AssetEditDraftTextPatchResultDto.LengthExceeded(),
                _ => AssetEditDraftTextPatchResultDto.NotFound()
            };
    }
}
