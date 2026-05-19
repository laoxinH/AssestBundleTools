using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单工作区下的预览缓存。
/// </summary>
public interface IPreviewCacheStore
{
    PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey);

    IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey);

    string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor);

    PreviewCacheUsageDto GetUsage(string workspaceKey)
    {
        var entries = LoadDescriptors(workspaceKey)
            .Where(static descriptor =>
                !string.IsNullOrWhiteSpace(descriptor.WorkspaceKey) &&
                !string.IsNullOrWhiteSpace(descriptor.PreviewCacheKey))
            .Select(descriptor =>
            {
                var payload = Load(descriptor.WorkspaceKey, descriptor.PreviewCacheKey);
                var sizeInBytes =
                    (payload?.Content.LongLength ?? 0) +
                    (payload?.ImageFocusContent?.LongLength ?? 0);
                return new PreviewCacheEntryDto(
                    descriptor.WorkspaceKey,
                    descriptor.PreviewCacheKey,
                    descriptor.BundleCacheKey,
                    descriptor.AssetId,
                    descriptor.PreviewKind,
                    sizeInBytes,
                    descriptor.GeneratedAtUtc);
            })
            .OrderBy(static entry => entry.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.PreviewCacheKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PreviewCacheUsageDto(
            TotalSizeInBytes: entries.Sum(static entry => entry.SizeInBytes),
            EntryCount: entries.Length,
            Entries: entries);
    }

    PreviewCachePruneResultDto Prune(PreviewCachePruneRequestDto request)
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

    void Save(PreviewCachePayloadDto previewCachePayload);

    void Delete(string workspaceKey, string previewCacheKey);

    void DeleteWorkspace(string workspaceKey);
}
