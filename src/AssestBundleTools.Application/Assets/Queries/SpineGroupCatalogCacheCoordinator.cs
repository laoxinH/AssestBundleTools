using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed class SpineGroupCatalogCacheCoordinator
{
    public const int SchemaVersion = 1;
    public const string DetectorVersion = "spine-group-catalog-v1";

    private static readonly JsonSerializerOptions FingerprintSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Lazy<SpineGroupCatalogCacheEntryDto>> _runtimeEntries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _buildLocks = new(StringComparer.Ordinal);
    private readonly ISpineGroupCatalogCacheStore? _persistentStore;

    public SpineGroupCatalogCacheCoordinator(ISpineGroupCatalogCacheStore? persistentStore)
    {
        _persistentStore = persistentStore;
    }

    public SpineAssetGroupListResultDto GetOrBuildResult(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        string targetBundleDisplayName,
        string? searchKeyword,
        IReadOnlyList<AssetListResultDto> assetLists,
        IReadOnlyDictionary<string, AssetDetailsDto> assetDetailsByAssetKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assetLists);
        ArgumentNullException.ThrowIfNull(assetDetailsByAssetKey);

        var bundleIndexFingerprint = CreateBundleIndexFingerprint(assetLists);
        var catalogFingerprint = CreateCatalogFingerprint(workspaceId, bundleIndexFingerprint);
        var runtimeKey = string.Concat(workspaceId.Value, "\n", catalogFingerprint);

        var runtimeEntry = _runtimeEntries.GetOrAdd(runtimeKey, _ => new Lazy<SpineGroupCatalogCacheEntryDto>(
            () => LoadOrBuildCatalog(
                workspaceId,
                catalogFingerprint,
                bundleIndexFingerprint,
                assetLists,
                assetDetailsByAssetKey,
                cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication));
        SpineGroupCatalogCacheEntryDto catalogEntry;
        try
        {
            catalogEntry = runtimeEntry.Value;
        }
        catch (OperationCanceledException)
        {
            _runtimeEntries.TryRemove(runtimeKey, out _);
            throw;
        }

        return ProjectResult(
            catalogEntry,
            workspaceId,
            targetBundleId,
            targetBundleDisplayName,
            searchKeyword);
    }

    private SpineGroupCatalogCacheEntryDto LoadOrBuildCatalog(
        WorkspaceId workspaceId,
        string catalogFingerprint,
        string bundleIndexFingerprint,
        IReadOnlyList<AssetListResultDto> assetLists,
        IReadOnlyDictionary<string, AssetDetailsDto> assetDetailsByAssetKey,
        CancellationToken cancellationToken)
    {
        var workspaceKey = workspaceId.Value;
        var buildLock = _buildLocks.GetOrAdd(workspaceKey, static _ => new object());
        lock (buildLock)
        {
            var persistentEntry = _persistentStore?.Load(workspaceKey);
            if (IsValid(persistentEntry, workspaceKey, catalogFingerprint, bundleIndexFingerprint))
            {
                var accessedEntry = persistentEntry! with { LastAccessedAtUtc = DateTimeOffset.UtcNow };
                _persistentStore?.Save(accessedEntry);
                return accessedEntry;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var builtEntry = BuildCatalog(
                workspaceId,
                catalogFingerprint,
                bundleIndexFingerprint,
                assetLists,
                assetDetailsByAssetKey,
                cancellationToken);
            _persistentStore?.Save(builtEntry);
            return builtEntry;
        }
    }

    private static SpineGroupCatalogCacheEntryDto BuildCatalog(
        WorkspaceId workspaceId,
        string catalogFingerprint,
        string bundleIndexFingerprint,
        IReadOnlyList<AssetListResultDto> assetLists,
        IReadOnlyDictionary<string, AssetDetailsDto> assetDetailsByAssetKey,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var bundleResults = new List<SpineAssetGroupListResultDto>(assetLists.Count);
        foreach (var assetList in assetLists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bundleResults.Add(SpineAssetGroupDetector.CreateResult(
                workspaceId,
                assetList.BundleId,
                assetList.BundleDisplayName,
                searchKeyword: null,
                assetLists,
                assetDetailsByAssetKey,
                includeAssetDetailState: false));
        }

        return new SpineGroupCatalogCacheEntryDto(
            SchemaVersion,
            workspaceId.Value,
            catalogFingerprint,
            DetectorVersion,
            bundleIndexFingerprint,
            CreatedAtUtc: now,
            LastAccessedAtUtc: now,
            BundleResults: bundleResults);
    }

    private static SpineAssetGroupListResultDto ProjectResult(
        SpineGroupCatalogCacheEntryDto catalogEntry,
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        string targetBundleDisplayName,
        string? searchKeyword)
    {
        var normalizedSearchKeyword = SpineAssetGroupDetector.NormalizeSearchKeyword(searchKeyword);
        var cachedResult = catalogEntry.BundleResults.FirstOrDefault(result => result.BundleId.Equals(targetBundleId));
        if (cachedResult is null)
        {
            return new SpineAssetGroupListResultDto(
                workspaceId,
                targetBundleId,
                targetBundleDisplayName,
                normalizedSearchKeyword,
                TotalCount: 0,
                FilteredCount: 0,
                Items: Array.Empty<SpineAssetGroupDto>());
        }

        var filteredItems = cachedResult.Items
            .Where(group => SpineAssetGroupDetector.MatchesSearch(group, normalizedSearchKeyword))
            .ToArray();

        return cachedResult with
        {
            BundleDisplayName = targetBundleDisplayName,
            SearchKeyword = normalizedSearchKeyword,
            FilteredCount = filteredItems.Length,
            Items = filteredItems
        };
    }

    private static bool IsValid(
        SpineGroupCatalogCacheEntryDto? entry,
        string workspaceKey,
        string catalogFingerprint,
        string bundleIndexFingerprint)
    {
        return entry is not null &&
               entry.SchemaVersion == SchemaVersion &&
               string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
               string.Equals(entry.CatalogFingerprint, catalogFingerprint, StringComparison.Ordinal) &&
               string.Equals(entry.DetectorVersion, DetectorVersion, StringComparison.Ordinal) &&
               string.Equals(entry.BundleIndexFingerprint, bundleIndexFingerprint, StringComparison.Ordinal);
    }

    private static string CreateCatalogFingerprint(WorkspaceId workspaceId, string bundleIndexFingerprint)
    {
        return CreateSha256Hex(string.Concat(
            SchemaVersion,
            "\n",
            DetectorVersion,
            "\n",
            workspaceId.Value,
            "\n",
            bundleIndexFingerprint));
    }

    private static string CreateBundleIndexFingerprint(IReadOnlyList<AssetListResultDto> assetLists)
    {
        var fingerprintEntries = assetLists
            .OrderBy(static list => list.BundleId.Value, StringComparer.Ordinal)
            .Select(static list => new BundleFingerprintEntry(
                list.BundleId.Value,
                list.BundleDisplayName,
                list.TotalCount,
                list.Items
                    .OrderBy(static item => item.AssetId.Value, StringComparer.Ordinal)
                    .Select(static item => new AssetFingerprintEntry(
                        item.AssetId.Value,
                        item.Name,
                        item.AssetPath,
                        item.AssetType.ToString(),
                        item.AssetTypeDisplayName,
                        item.SizeInBytes,
                        item.HasPreview))
                    .ToArray()))
            .ToArray();
        return CreateSha256Hex(JsonSerializer.Serialize(fingerprintEntries, FingerprintSerializerOptions));
    }

    private static string CreateSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record BundleFingerprintEntry(
        string BundleId,
        string BundleDisplayName,
        int TotalCount,
        IReadOnlyList<AssetFingerprintEntry> Assets);

    private sealed record AssetFingerprintEntry(
        string AssetId,
        string Name,
        string AssetPath,
        string AssetType,
        string? AssetTypeDisplayName,
        long SizeInBytes,
        bool HasPreview);
}
