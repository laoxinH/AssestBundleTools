using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责工作区目录项恢复、可用性判断、恢复请求推导与元数据回写编排。
/// </summary>
public sealed class WorkspaceContextCatalogService : IWorkspaceContextCatalogService
{
    private readonly IWorkspaceContextPersistenceService _workspaceContextPersistenceService;

    public WorkspaceContextCatalogService(IWorkspaceContextPersistenceService workspaceContextPersistenceService)
    {
        _workspaceContextPersistenceService = workspaceContextPersistenceService
            ?? throw new ArgumentNullException(nameof(workspaceContextPersistenceService));
    }

    public WorkspaceContextCatalogStateDto LoadCatalogState(IReadOnlyList<WorkspaceContextLiveSessionDto> liveSessions)
    {
        ArgumentNullException.ThrowIfNull(liveSessions);

        var catalogSnapshot = _workspaceContextPersistenceService.LoadWorkspaceCatalogSnapshot();
        var liveSessionsByKey = liveSessions.ToDictionary(
            static session => session.WorkspaceKey,
            StringComparer.Ordinal);
        var restoredEntries = new List<WorkspaceContextEntryDto>(catalogSnapshot.RecentWorkspaces.Count);

        foreach (var workspaceEntry in catalogSnapshot.RecentWorkspaces)
        {
            liveSessionsByKey.TryGetValue(workspaceEntry.WorkspaceKey, out var liveSession);
            var resolvedWorkspaceDataRootPath = ResolveWorkspaceDataRootPath(
                workspaceEntry.WorkspaceDataRootPath,
                liveSession?.WorkspaceDataRootPath);
            var metadata = _workspaceContextPersistenceService.LoadWorkspaceMetadata(
                workspaceEntry.WorkspaceKey,
                resolvedWorkspaceDataRootPath);
            restoredEntries.Add(new WorkspaceContextEntryDto(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                DisplayName: workspaceEntry.DisplayName,
                SourcePath: workspaceEntry.SourcePath,
                WorkspaceDataRootPath: resolvedWorkspaceDataRootPath,
                AssetBundleDecryptorId: ResolveCatalogEntryAssetBundleDecryptorId(
                    liveSession?.AssetBundleDecryptorId,
                    metadata),
                WorkspaceMode: workspaceEntry.WorkspaceMode,
                LastActivatedAtUtc: workspaceEntry.LastActivatedAtUtc,
                IsPinned: workspaceEntry.IsPinned,
                AvailabilityStatus: ResolveAvailabilityStatus(
                    workspaceEntry.SourcePath,
                    resolvedWorkspaceDataRootPath),
                LastKnownState: workspaceEntry.LastKnownState,
                WorkspaceSummary: liveSession?.WorkspaceSummary));
        }

        return new WorkspaceContextCatalogStateDto(
            catalogSnapshot.LastActiveWorkspaceKey,
            SortEntries(restoredEntries));
    }

    public WorkspaceCatalogSnapshotDto SaveCatalogState(
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries,
        WorkspaceSummaryDto? currentWorkspaceSummary)
    {
        ArgumentNullException.ThrowIfNull(workspaceEntries);

        var snapshot = BuildCatalogSnapshot(workspaceEntries, currentWorkspaceSummary);
        _workspaceContextPersistenceService.SaveWorkspaceCatalogSnapshot(snapshot);
        return snapshot;
    }

    public WorkspaceBrowserStateSnapshotDto? LoadWorkspaceBrowserState(string workspaceKey, string? workspaceDataRootPath)
    {
        return _workspaceContextPersistenceService.LoadWorkspaceBrowserState(workspaceKey, workspaceDataRootPath);
    }

    public WorkspaceContextEntryRegistrationResultDto CreateRegisteredEntry(
        string workspaceKey,
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath,
        string? assetBundleDecryptorId,
        WorkspaceContextEntryDto? existingEntry,
        DateTimeOffset activatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(workspaceSummary);

        var resolvedWorkspaceDataRootPath = ResolveWorkspaceDataRootPath(
            workspaceDataRootPath,
            existingEntry?.WorkspaceDataRootPath);
        var existingMetadata = _workspaceContextPersistenceService.LoadWorkspaceMetadata(
            workspaceKey,
            resolvedWorkspaceDataRootPath);
        var resolvedAssetBundleDecryptorId = ResolveAssetBundleDecryptorId(
            assetBundleDecryptorId,
            existingMetadata,
            existingEntry?.AssetBundleDecryptorId);
        var entry = new WorkspaceContextEntryDto(
            WorkspaceKey: workspaceKey,
            DisplayName: workspaceSummary.DisplayName,
            SourcePath: workspaceSummary.SourcePath,
            WorkspaceDataRootPath: resolvedWorkspaceDataRootPath,
            AssetBundleDecryptorId: resolvedAssetBundleDecryptorId,
            WorkspaceMode: workspaceSummary.Mode,
            LastActivatedAtUtc: activatedAtUtc,
            IsPinned: existingEntry?.IsPinned ?? false,
            AvailabilityStatus: ResolveAvailabilityStatus(
                workspaceSummary.SourcePath,
                resolvedWorkspaceDataRootPath),
            LastKnownState: BuildLastKnownState(workspaceSummary),
            WorkspaceSummary: workspaceSummary);

        return new WorkspaceContextEntryRegistrationResultDto(entry, existingMetadata);
    }

    public WorkspaceContextEntryDto RefreshActivatedEntry(
        WorkspaceContextEntryDto existingEntry,
        DateTimeOffset activatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(existingEntry);

        return existingEntry with
        {
            LastActivatedAtUtc = activatedAtUtc,
            AvailabilityStatus = ResolveAvailabilityStatus(
                existingEntry.SourcePath,
                existingEntry.WorkspaceDataRootPath),
            LastKnownState = existingEntry.WorkspaceSummary is not null
                ? BuildLastKnownState(existingEntry.WorkspaceSummary)
                : existingEntry.LastKnownState
        };
    }

    public WorkspaceContextRestoreRequestDto? ResolveLastActiveWorkspaceRestoreRequest(
        string? lastActiveWorkspaceKey,
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries)
    {
        ArgumentNullException.ThrowIfNull(workspaceEntries);

        if (string.IsNullOrWhiteSpace(lastActiveWorkspaceKey))
        {
            return null;
        }

        var lastActiveEntry = workspaceEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, lastActiveWorkspaceKey, StringComparison.Ordinal));
        if (lastActiveEntry is null || !lastActiveEntry.IsAvailable)
        {
            return null;
        }

        return new WorkspaceContextRestoreRequestDto(
            SourcePath: lastActiveEntry.SourcePath,
            WorkspaceDisplayName: lastActiveEntry.DisplayName,
            WorkspaceDataRootPath: lastActiveEntry.WorkspaceDataRootPath,
            AssetBundleDecryptorId: lastActiveEntry.AssetBundleDecryptorId);
    }

    public void SaveWorkspaceActivationMetadata(
        WorkspaceContextEntryDto workspaceEntry,
        DateTimeOffset openedAtUtc,
        WorkspaceMetadataDto? existingMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceEntry);

        if (workspaceEntry.WorkspaceSummary is null)
        {
            return;
        }

        existingMetadata ??= _workspaceContextPersistenceService.LoadWorkspaceMetadata(
            workspaceEntry.WorkspaceKey,
            workspaceEntry.WorkspaceDataRootPath);
        _workspaceContextPersistenceService.SaveWorkspaceActivationMetadata(
            new WorkspaceActivationMetadataSaveRequestDto(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                DisplayName: workspaceEntry.WorkspaceSummary.DisplayName,
                SourcePath: workspaceEntry.WorkspaceSummary.SourcePath,
                OpenedAtUtc: openedAtUtc,
                AssetBundleDecryptorId: ResolveAssetBundleDecryptorId(
                    workspaceEntry.AssetBundleDecryptorId,
                    existingMetadata,
                    workspaceEntry.AssetBundleDecryptorId)));
    }

    public void SaveWorkspaceBrowserState(
        WorkspaceContextEntryDto workspaceEntry,
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceBrowserStateSnapshotDto browserStateSnapshot)
    {
        ArgumentNullException.ThrowIfNull(workspaceEntry);
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(browserStateSnapshot);

        var existingMetadata = _workspaceContextPersistenceService.LoadWorkspaceMetadata(
            workspaceEntry.WorkspaceKey,
            workspaceEntry.WorkspaceDataRootPath);
        _workspaceContextPersistenceService.SaveWorkspaceBrowserState(
            new WorkspaceBrowserStateSaveRequestDto(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                DisplayName: workspaceSummary.DisplayName,
                SourcePath: workspaceSummary.SourcePath,
                AssetBundleDecryptorId: ResolveAssetBundleDecryptorId(
                    workspaceEntry.AssetBundleDecryptorId,
                    existingMetadata,
                    workspaceEntry.AssetBundleDecryptorId),
                BrowserState: browserStateSnapshot));
    }

    public IReadOnlyList<WorkspaceContextEntryDto> SortEntries(IEnumerable<WorkspaceContextEntryDto> workspaceEntries)
    {
        ArgumentNullException.ThrowIfNull(workspaceEntries);

        return workspaceEntries
            .OrderByDescending(static entry => entry.IsPinned)
            .ThenByDescending(static entry => entry.LastActivatedAtUtc)
            .ToArray();
    }

    private WorkspaceCatalogSnapshotDto BuildCatalogSnapshot(
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries,
        WorkspaceSummaryDto? currentWorkspaceSummary)
    {
        var lastActiveWorkspaceKey = ResolveLastActiveWorkspaceKey(workspaceEntries, currentWorkspaceSummary);
        return new WorkspaceCatalogSnapshotDto(
            Version: 1,
            LastActiveWorkspaceKey: lastActiveWorkspaceKey,
            RecentWorkspaces: workspaceEntries
                .Select(entry => new WorkspaceCatalogEntryDto(
                    WorkspaceKey: entry.WorkspaceKey,
                    DisplayName: entry.DisplayName,
                    SourcePath: entry.SourcePath,
                    WorkspaceDataRootPath: entry.WorkspaceDataRootPath,
                    WorkspaceMode: entry.WorkspaceMode,
                    LastActivatedAtUtc: entry.LastActivatedAtUtc,
                    IsPinned: entry.IsPinned,
                    LastKnownState: entry.LastKnownState))
                .ToArray());
    }

    private static string? ResolveLastActiveWorkspaceKey(
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries,
        WorkspaceSummaryDto? currentWorkspaceSummary)
    {
        if (currentWorkspaceSummary is not null)
        {
            var currentEntry = workspaceEntries.FirstOrDefault(entry =>
                string.Equals(
                    entry.WorkspaceSummary?.WorkspaceId.Value,
                    currentWorkspaceSummary.WorkspaceId.Value,
                    StringComparison.Ordinal));
            if (currentEntry is not null)
            {
                return currentEntry.WorkspaceKey;
            }
        }

        return workspaceEntries.FirstOrDefault()?.WorkspaceKey;
    }

    private WorkspaceAvailabilityStatus ResolveAvailabilityStatus(
        string? sourcePath,
        string? workspaceDataRootPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath) &&
            !IsDirectoryPath(workspaceDataRootPath))
        {
            return WorkspaceAvailabilityStatus.MissingWorkspaceDataRootDirectory;
        }

        if (!string.IsNullOrWhiteSpace(sourcePath) &&
            !IsDirectoryPath(sourcePath))
        {
            return WorkspaceAvailabilityStatus.MissingSourceDirectory;
        }

        return WorkspaceAvailabilityStatus.Available;
    }

    private bool IsDirectoryPath(string path)
    {
        return _workspaceContextPersistenceService.ResolveOptionalPathKind(path) == WorkspaceImportPathKind.Directory;
    }

    private string ResolveWorkspaceDataRootPath(
        string? workspaceDataRootPath,
        string? existingWorkspaceDataRootPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return workspaceDataRootPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existingWorkspaceDataRootPath))
        {
            return existingWorkspaceDataRootPath;
        }

        return _workspaceContextPersistenceService.GetDefaultWorkspaceDataRootPath();
    }

    private static string? ResolveAssetBundleDecryptorId(
        string? requestedAssetBundleDecryptorId,
        WorkspaceMetadataDto? existingMetadata,
        string? existingEntryDecryptorId)
    {
        var normalizedRequestedDecryptorId = NormalizeOptionalValue(requestedAssetBundleDecryptorId);
        var normalizedMetadataDecryptorId = NormalizeOptionalValue(existingMetadata?.AssetBundleDecryptorId);
        var normalizedExistingEntryDecryptorId = NormalizeOptionalValue(existingEntryDecryptorId);

        if (IsAutoDetectDecryptorId(normalizedRequestedDecryptorId))
        {
            return PreferResolvedDecryptorId(
                normalizedMetadataDecryptorId,
                normalizedExistingEntryDecryptorId,
                normalizedRequestedDecryptorId);
        }

        return normalizedRequestedDecryptorId
               ?? PreferResolvedDecryptorId(
                   normalizedMetadataDecryptorId,
                   normalizedExistingEntryDecryptorId);
    }

    private static string? ResolveCatalogEntryAssetBundleDecryptorId(
        string? liveSessionDecryptorId,
        WorkspaceMetadataDto? metadata)
    {
        return PreferResolvedDecryptorId(
            NormalizeOptionalValue(metadata?.AssetBundleDecryptorId),
            NormalizeOptionalValue(liveSessionDecryptorId));
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? PreferResolvedDecryptorId(
        string? primaryDecryptorId,
        string? secondaryDecryptorId,
        string? autoDetectFallbackDecryptorId = null)
    {
        if (!string.IsNullOrWhiteSpace(primaryDecryptorId) &&
            !IsAutoDetectDecryptorId(primaryDecryptorId))
        {
            return primaryDecryptorId;
        }

        if (!string.IsNullOrWhiteSpace(secondaryDecryptorId) &&
            !IsAutoDetectDecryptorId(secondaryDecryptorId))
        {
            return secondaryDecryptorId;
        }

        return autoDetectFallbackDecryptorId
               ?? primaryDecryptorId
               ?? secondaryDecryptorId;
    }

    private static bool IsAutoDetectDecryptorId(string? decryptorId)
    {
        return string.Equals(
            decryptorId,
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLastKnownState(WorkspaceSummaryDto workspaceSummary) =>
        $"bundleCount={workspaceSummary.BundleCount};assetCount={workspaceSummary.AssetCount}";
}
