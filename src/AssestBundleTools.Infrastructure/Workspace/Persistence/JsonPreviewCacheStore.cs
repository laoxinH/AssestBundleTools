using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于“描述文件 + 二进制内容文件”的预览缓存存储。
/// </summary>
public sealed class JsonPreviewCacheStore : IPreviewCacheStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonPreviewCacheStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(previewCacheKey))
        {
            return null;
        }

        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(workspaceKey, previewCacheKey);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null)
        {
            return null;
        }

        var payloadFilePath = TryResolvePayloadFilePath(
            workspaceKey,
            previewCacheKey,
            descriptor.RelativePayloadPath);
        if (string.IsNullOrWhiteSpace(payloadFilePath))
        {
            return null;
        }

        var payload = JsonWorkspaceFileStorageHelper.TryReadBytes(payloadFilePath);
        if (payload is null)
        {
            return null;
        }

        var imageFocusPayload = TryReadOptionalImageFocusPayload(
            workspaceKey,
            previewCacheKey,
            descriptor);

        return new PreviewCachePayloadDto(descriptor, payload, imageFocusPayload);
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
            length);
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
            JsonPreviewCacheTextLineIndex.Load(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath,
                payload.Value.PayloadLength));
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
            JsonPreviewCacheTextLineIndex.Load(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath,
                payload.Value.PayloadLength));
    }

    public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Array.Empty<PreviewCacheDescriptorDto>();
        }

        var workspacePreviewDirectoryPath = _storagePaths.GetPreviewCacheDirectoryPath(workspaceKey);
        if (!Directory.Exists(workspacePreviewDirectoryPath))
        {
            return Array.Empty<PreviewCacheDescriptorDto>();
        }

        var descriptors = new List<PreviewCacheDescriptorDto>();

        foreach (var descriptorFilePath in Directory.EnumerateFiles(workspacePreviewDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
                descriptorFilePath,
                JsonWorkspaceSerialization.SerializerOptions);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(descriptor.PreviewCacheKey) ||
            string.IsNullOrWhiteSpace(descriptor.RelativePayloadPath))
        {
            return null;
        }

        return TryResolvePayloadFilePath(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath);
    }

    public PreviewCacheUsageDto GetUsage(string workspaceKey)
    {
        var entries = EnumerateCacheEntries(workspaceKey)
            .OrderBy(static entry => entry.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.PreviewCacheKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PreviewCacheUsageDto(
            TotalSizeInBytes: entries.Sum(static entry => entry.SizeInBytes),
            EntryCount: entries.Length,
            Entries: entries);
    }

    public PreviewCachePruneResultDto Prune(PreviewCachePruneRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumSizeInBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumSizeInBytes));
        }

        if (request.MaximumEntryCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumEntryCount));
        }

        var usageBefore = GetUsage(request.WorkspaceKey);
        var targetSizeInBytes = request.MaximumSizeInBytes ?? long.MaxValue;
        var targetEntryCount = request.MaximumEntryCount ?? int.MaxValue;
        IEnumerable<string> protectedPreviewCacheKeysSource =
            request.ProtectedPreviewCacheKeys is null
                ? Array.Empty<string>()
                : request.ProtectedPreviewCacheKeys;
        var protectedPreviewCacheKeys = protectedPreviewCacheKeysSource
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sizeAfter = usageBefore.TotalSizeInBytes;
        var entryCountAfter = usageBefore.EntryCount;
        var prunedEntries = new List<PreviewCacheEntryDto>();

        foreach (var entry in usageBefore.Entries
                     .OrderBy(static entry => entry.GeneratedAtUtc)
                     .ThenBy(static entry => entry.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static entry => entry.PreviewCacheKey, StringComparer.OrdinalIgnoreCase))
        {
            if (sizeAfter <= targetSizeInBytes && entryCountAfter <= targetEntryCount)
            {
                break;
            }

            if (protectedPreviewCacheKeys.Contains(entry.PreviewCacheKey))
            {
                continue;
            }

            Delete(entry.WorkspaceKey, entry.PreviewCacheKey);
            prunedEntries.Add(entry);
            sizeAfter -= entry.SizeInBytes;
            entryCountAfter--;
        }

        return new PreviewCachePruneResultDto(
            SizeBeforeInBytes: usageBefore.TotalSizeInBytes,
            SizeAfterInBytes: Math.Max(0, sizeAfter),
            EntryCountBefore: usageBefore.EntryCount,
            EntryCountAfter: Math.Max(0, entryCountAfter),
            PrunedEntries: prunedEntries);
    }

    public void Save(PreviewCachePayloadDto previewCachePayload)
    {
        ArgumentNullException.ThrowIfNull(previewCachePayload);

        var descriptor = previewCachePayload.Descriptor;
        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(descriptor.WorkspaceKey, descriptor.PreviewCacheKey);
        var payloadFilePath = _storagePaths.GetPreviewPayloadFilePath(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            descriptorFilePath,
            descriptor,
            JsonWorkspaceSerialization.SerializerOptions);
        JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, previewCachePayload.Content);
        if (string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            JsonPreviewCacheTextLineIndex.Save(
                descriptor,
                payloadFilePath,
                previewCachePayload.Content);
        }
        else
        {
            JsonPreviewCacheTextLineIndex.Delete(payloadFilePath);
        }

        if (previewCachePayload.ImageFocusContent is { Length: > 0 } &&
            !string.IsNullOrWhiteSpace(descriptor.ImageFocus?.RelativePayloadPath))
        {
            var imageFocusPayloadFilePath = _storagePaths.GetPreviewPayloadFilePath(
                descriptor.WorkspaceKey,
                descriptor.PreviewCacheKey,
                descriptor.ImageFocus.RelativePayloadPath);
            JsonWorkspaceFileStorageHelper.WriteBytesAtomically(
                imageFocusPayloadFilePath,
                previewCachePayload.ImageFocusContent);
        }
    }

    public void Delete(string workspaceKey, string previewCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(previewCacheKey))
        {
            return;
        }

        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(workspaceKey, previewCacheKey);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(descriptorFilePath);

        if (descriptor is null)
        {
            return;
        }

        var payloadFilePath = TryResolvePayloadFilePath(workspaceKey, previewCacheKey, descriptor.RelativePayloadPath);
        if (string.IsNullOrWhiteSpace(payloadFilePath))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(payloadFilePath);
        JsonPreviewCacheTextLineIndex.Delete(payloadFilePath);
        if (!string.IsNullOrWhiteSpace(descriptor.ImageFocus?.RelativePayloadPath))
        {
            var imageFocusPayloadFilePath = TryResolvePayloadFilePath(
                workspaceKey,
                previewCacheKey,
                descriptor.ImageFocus.RelativePayloadPath);
            if (!string.IsNullOrWhiteSpace(imageFocusPayloadFilePath))
            {
                JsonWorkspaceFileStorageHelper.DeleteFileIfExists(imageFocusPayloadFilePath);
            }
        }

        var payloadDirectoryPath = Path.GetDirectoryName(payloadFilePath);
        if (!string.IsNullOrWhiteSpace(payloadDirectoryPath) &&
            Directory.Exists(payloadDirectoryPath) &&
            !Directory.EnumerateFileSystemEntries(payloadDirectoryPath).Any())
        {
            Directory.Delete(payloadDirectoryPath, recursive: false);
        }
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetPreviewCacheDirectoryPath(workspaceKey));
    }

    private IEnumerable<PreviewCacheEntryDto> EnumerateCacheEntries(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var workspacePreviewDirectoryPath = _storagePaths.GetPreviewCacheDirectoryPath(workspaceKey);
        if (!Directory.Exists(workspacePreviewDirectoryPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(workspacePreviewDirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryCreateCacheEntry)
            .Where(static entry => entry is not null)
            .Select(static entry => entry!);
    }

    private PreviewCacheEntryDto? TryCreateCacheEntry(string descriptorFilePath)
    {
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null)
        {
            return null;
        }

        var sizeInBytes = GetFileSize(descriptorFilePath);
        var payloadFilePath = TryResolvePayloadFilePath(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath);
        if (!string.IsNullOrWhiteSpace(payloadFilePath))
        {
            sizeInBytes += GetFileSize(payloadFilePath);
            sizeInBytes += JsonPreviewCacheTextLineIndex.GetSizeInBytes(payloadFilePath);
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ImageFocus?.RelativePayloadPath))
        {
            var imageFocusPayloadFilePath = TryResolvePayloadFilePath(
                descriptor.WorkspaceKey,
                descriptor.PreviewCacheKey,
                descriptor.ImageFocus.RelativePayloadPath);
            if (!string.IsNullOrWhiteSpace(imageFocusPayloadFilePath))
            {
                sizeInBytes += GetFileSize(imageFocusPayloadFilePath);
            }
        }

        return new PreviewCacheEntryDto(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.BundleCacheKey,
            descriptor.AssetId,
            descriptor.PreviewKind,
            sizeInBytes,
            descriptor.GeneratedAtUtc);
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

    private byte[]? TryReadOptionalImageFocusPayload(
        string workspaceKey,
        string previewCacheKey,
        PreviewCacheDescriptorDto descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.ImageFocus?.RelativePayloadPath))
        {
            return null;
        }

        var imageFocusPayloadFilePath = TryResolvePayloadFilePath(
            workspaceKey,
            previewCacheKey,
            descriptor.ImageFocus.RelativePayloadPath);
        return string.IsNullOrWhiteSpace(imageFocusPayloadFilePath)
            ? null
            : JsonWorkspaceFileStorageHelper.TryReadBytes(imageFocusPayloadFilePath);
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
