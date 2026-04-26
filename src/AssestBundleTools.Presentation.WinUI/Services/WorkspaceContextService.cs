using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;
using System.IO;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 保存当前主工作区上下文的轻量服务。
/// </summary>
/// <remarks>
/// 这个服务只负责“当前 UI 会话里谁是活动工作区、最近登记了哪些工作区摘要”，
/// 真正的运行时工作区数据仍然留在 <c>WorkspaceSessionRegistry</c>，
/// 持久化工作区目录与恢复元数据则交给应用层抽象和基础设施实现。
/// </remarks>
public sealed class WorkspaceContextService : IWorkspaceContextService
{
    private readonly IWorkspaceCatalogStore? _workspaceCatalogStore;
    private readonly IWorkspaceMetadataStoreFactory? _workspaceMetadataStoreFactory;
    private WorkspaceSummaryDto? _currentWorkspaceSummary;
    private readonly List<WorkspaceContextEntry> _workspaceEntries = [];
    private string? _lastActiveWorkspaceKey;

    public WorkspaceContextService(
        IWorkspaceCatalogStore? workspaceCatalogStore = null,
        IWorkspaceMetadataStoreFactory? workspaceMetadataStoreFactory = null)
    {
        _workspaceCatalogStore = workspaceCatalogStore;
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory;
        LoadWorkspaceCatalogSnapshot();
    }

    public event EventHandler? CurrentWorkspaceChanged;

    public event EventHandler? WorkspaceEntriesChanged;

    public WorkspaceSummaryDto? CurrentWorkspaceSummary => _currentWorkspaceSummary;

    public WorkspaceSummaryDto? ActiveWorkspaceSummary => _currentWorkspaceSummary;

    public bool HasCurrentWorkspace => _currentWorkspaceSummary is not null;

    public IReadOnlyList<WorkspaceContextEntry> WorkspaceEntries => _workspaceEntries;

    public int WorkspaceEntryCount => _workspaceEntries.Count;

    public bool TryGetCurrentWorkspaceEntry(out WorkspaceContextEntry? workspaceEntry)
    {
        workspaceEntry = null;

        if (_currentWorkspaceSummary is null)
        {
            return false;
        }

        workspaceEntry = _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(
                entry.WorkspaceSummary?.WorkspaceId.Value,
                _currentWorkspaceSummary.WorkspaceId.Value,
                StringComparison.Ordinal));

        return workspaceEntry is not null;
    }

    public WorkspaceBrowserStateSnapshotDto? GetCurrentWorkspaceBrowserState()
    {
        if (!TryGetCurrentWorkspaceContext(out _, out var workspaceEntry) ||
            _workspaceMetadataStoreFactory is null)
        {
            return null;
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(workspaceEntry.WorkspaceDataRootPath);
        var metadata = metadataStore.Load(workspaceEntry.WorkspaceKey);
        return metadata is null ? null : MapToBrowserStateSnapshot(metadata);
    }

    /// <summary>
    /// 尝试根据最近目录索引恢复“上次活动工作区”的打开请求。
    /// 这里只返回 Presentation 层需要的导航参数，
    /// 不会提前创建运行时会话，避免把“启动恢复”逻辑混入 Application 会话仓储。
    /// </summary>
    public bool TryGetLastActiveWorkspaceNavigationRequest(out WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        workspaceNavigationRequest = null;

        if (string.IsNullOrWhiteSpace(_lastActiveWorkspaceKey))
        {
            return false;
        }

        var lastActiveEntry = _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, _lastActiveWorkspaceKey, StringComparison.Ordinal));
        if (lastActiveEntry is null || !lastActiveEntry.IsAvailable)
        {
            return false;
        }

        workspaceNavigationRequest = new WorkspaceNavigationRequest(
            SourcePath: lastActiveEntry.SourcePath,
            WorkspaceDisplayName: lastActiveEntry.DisplayName,
            WorkspaceDataRootPath: lastActiveEntry.WorkspaceDataRootPath,
            OpenTarget: WorkspaceOpenTarget.CurrentWindow,
            AssetBundleDecryptorId: lastActiveEntry.AssetBundleDecryptorId);
        return true;
    }

    /// <summary>
    /// 在当前应用会话中登记工作区，并将其切换为活动工作区。
    /// 相同工作区标识路径会复用同一条目录项，避免刷新时不断追加重复记录。
    /// </summary>
    public void RegisterOrActivateWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            workspaceSummary.SourcePath,
            workspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var existingEntry = FindExistingEntry(workspaceKey);
        var resolvedWorkspaceDataRootPath = ResolveWorkspaceDataRootPath(workspaceDataRootPath, existingEntry);
        var existingMetadata = LoadWorkspaceMetadata(workspaceKey, resolvedWorkspaceDataRootPath);
        var resolvedAssetBundleDecryptorId = ResolveAssetBundleDecryptorId(
            assetBundleDecryptorId,
            existingMetadata,
            existingEntry);
        var entry = new WorkspaceContextEntry(
            WorkspaceKey: workspaceKey,
            DisplayName: workspaceSummary.DisplayName,
            SourcePath: workspaceSummary.SourcePath,
            WorkspaceDataRootPath: resolvedWorkspaceDataRootPath,
            AssetBundleDecryptorId: resolvedAssetBundleDecryptorId,
            WorkspaceMode: workspaceSummary.Mode,
            LastActivatedAtUtc: now,
            IsPinned: existingEntry?.IsPinned ?? false,
            AvailabilityStatus: ResolveAvailabilityStatus(
                workspaceSummary.SourcePath,
                resolvedWorkspaceDataRootPath),
            LastKnownState: BuildLastKnownState(workspaceSummary),
            WorkspaceSummary: workspaceSummary);

        UpsertWorkspaceEntry(entry);
        _currentWorkspaceSummary = workspaceSummary;

        PersistWorkspaceCatalogSnapshot();
        PersistWorkspaceMetadata(entry, openedAtUtc: now, existingMetadata);

        WorkspaceEntriesChanged?.Invoke(this, EventArgs.Empty);
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadWorkspaceCatalogEntries()
    {
        LoadWorkspaceCatalogSnapshot();
        WorkspaceEntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 由页面级协调器回写当前活动工作区的浏览状态。
    /// 这里统一负责把浏览快照合并进工作区元数据，
    /// 避免 ViewModel 直接感知持久化文件结构。
    /// </summary>
    public void UpdateCurrentWorkspaceBrowserState(WorkspaceBrowserStateSnapshotDto browserStateSnapshot)
    {
        ArgumentNullException.ThrowIfNull(browserStateSnapshot);

        if (!TryGetCurrentWorkspaceContext(out var workspaceSummary, out var workspaceEntry) ||
            _workspaceMetadataStoreFactory is null)
        {
            return;
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(workspaceEntry.WorkspaceDataRootPath);
        var existingMetadata = metadataStore.Load(workspaceEntry.WorkspaceKey);
        var now = DateTimeOffset.UtcNow;
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: workspaceEntry.WorkspaceKey,
            DisplayName: workspaceSummary.DisplayName,
            SourcePath: workspaceSummary.SourcePath,
            CreatedAtUtc: existingMetadata?.CreatedAtUtc ?? now,
            LastOpenedAtUtc: existingMetadata?.LastOpenedAtUtc ?? now,
            LastBundleId: browserStateSnapshot.LastBundleId,
            LastAssetId: browserStateSnapshot.LastAssetId,
            LastOutputDirectory: existingMetadata?.LastOutputDirectory,
            BundleSearchKeyword: browserStateSnapshot.BundleSearchKeyword,
            AssetSearchKeyword: browserStateSnapshot.AssetSearchKeyword,
            AssetTypeFilter: browserStateSnapshot.AssetTypeFilter,
            ShowModifiedOnly: browserStateSnapshot.ShowModifiedOnly,
            ShowPreviewableOnly: browserStateSnapshot.ShowPreviewableOnly,
            OpenBundleTabs: browserStateSnapshot.OpenBundleTabs,
            ActiveBundleTabId: browserStateSnapshot.ActiveBundleTabId,
            ImportedSources: existingMetadata?.ImportedSources,
            AssetBundleDecryptorId: ResolveAssetBundleDecryptorId(
                existingMetadata?.AssetBundleDecryptorId,
                existingMetadata,
                workspaceEntry),
            ExcludedBundleSourcePaths: existingMetadata?.ExcludedBundleSourcePaths);

        metadataStore.Save(metadata);
    }

    public bool TryActivateWorkspace(WorkspaceId workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return false;
        }

        var existingIndex = _workspaceEntries.FindIndex(entry =>
            string.Equals(entry.WorkspaceSummary?.WorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal));

        if (existingIndex < 0)
        {
            return false;
        }

        var existingEntry = _workspaceEntries[existingIndex];
        var updatedEntry = existingEntry with
        {
            LastActivatedAtUtc = DateTimeOffset.UtcNow,
            AvailabilityStatus = ResolveAvailabilityStatus(
                existingEntry.SourcePath,
                existingEntry.WorkspaceDataRootPath),
            LastKnownState = existingEntry.WorkspaceSummary is not null
                ? BuildLastKnownState(existingEntry.WorkspaceSummary)
                : existingEntry.LastKnownState
        };

        _workspaceEntries.RemoveAt(existingIndex);
        _workspaceEntries.Insert(0, updatedEntry);
        SortWorkspaceEntries();
        _currentWorkspaceSummary = updatedEntry.WorkspaceSummary;

        PersistWorkspaceCatalogSnapshot();
        if (updatedEntry.WorkspaceSummary is not null)
        {
            PersistWorkspaceMetadata(updatedEntry, updatedEntry.LastActivatedAtUtc);
        }

        WorkspaceEntriesChanged?.Invoke(this, EventArgs.Empty);
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetCurrentWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null)
    {
        RegisterOrActivateWorkspace(workspaceSummary, workspaceDataRootPath, assetBundleDecryptorId);
    }

    public void ClearCurrentWorkspace()
    {
        if (_currentWorkspaceSummary is null)
        {
            return;
        }

        _currentWorkspaceSummary = null;
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 启动时先恢复“最近工作区目录项”，
    /// 但不直接把它们当成已经真正打开的活动工作区。
    /// 这样可以维持运行时会话和持久化目录状态的边界。
    /// </summary>
    private void LoadWorkspaceCatalogSnapshot()
    {
        if (_workspaceCatalogStore is null)
        {
            return;
        }

        var catalogSnapshot = _workspaceCatalogStore.Load();
        _lastActiveWorkspaceKey = catalogSnapshot.LastActiveWorkspaceKey;
        var existingEntriesByKey = _workspaceEntries.ToDictionary(
            static entry => entry.WorkspaceKey,
            StringComparer.Ordinal);
        _workspaceEntries.Clear();

        foreach (var workspaceEntry in catalogSnapshot.RecentWorkspaces)
        {
            existingEntriesByKey.TryGetValue(workspaceEntry.WorkspaceKey, out var existingEntry);
            var resolvedWorkspaceDataRootPath = ResolveWorkspaceDataRootPath(
                workspaceEntry.WorkspaceDataRootPath,
                existingEntry);
            var metadata = LoadWorkspaceMetadata(workspaceEntry.WorkspaceKey, resolvedWorkspaceDataRootPath);
            _workspaceEntries.Add(new WorkspaceContextEntry(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                DisplayName: workspaceEntry.DisplayName,
                SourcePath: workspaceEntry.SourcePath,
                WorkspaceDataRootPath: resolvedWorkspaceDataRootPath,
                AssetBundleDecryptorId: ResolveCatalogEntryAssetBundleDecryptorId(existingEntry, metadata),
                WorkspaceMode: workspaceEntry.WorkspaceMode,
                LastActivatedAtUtc: workspaceEntry.LastActivatedAtUtc,
                IsPinned: workspaceEntry.IsPinned,
                AvailabilityStatus: ResolveAvailabilityStatus(
                    workspaceEntry.SourcePath,
                    workspaceEntry.WorkspaceDataRootPath),
                LastKnownState: workspaceEntry.LastKnownState,
                WorkspaceSummary: existingEntry?.WorkspaceSummary));
        }

        SortWorkspaceEntries();
    }

    private void PersistWorkspaceCatalogSnapshot()
    {
        if (_workspaceCatalogStore is null)
        {
            return;
        }

        _lastActiveWorkspaceKey = ResolveLastActiveWorkspaceKey();
        var snapshot = new WorkspaceCatalogSnapshotDto(
            Version: 1,
            LastActiveWorkspaceKey: _lastActiveWorkspaceKey,
            RecentWorkspaces: _workspaceEntries
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

        _workspaceCatalogStore.Save(snapshot);
    }

    /// <summary>
    /// 在工作区首次登记或重新激活时同步补齐基础元数据文件。
    /// 页面级浏览状态会在后续交互中通过浏览状态快照持续回写，
    /// 这里则负责保证元数据文件至少具备完整的基础字段。
    /// </summary>
    private void PersistWorkspaceMetadata(
        WorkspaceContextEntry workspaceEntry,
        DateTimeOffset openedAtUtc,
        WorkspaceMetadataDto? existingMetadata = null)
    {
        if (_workspaceMetadataStoreFactory is null || workspaceEntry.WorkspaceSummary is null)
        {
            return;
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(workspaceEntry.WorkspaceDataRootPath);
        existingMetadata ??= metadataStore.Load(workspaceEntry.WorkspaceKey);
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: workspaceEntry.WorkspaceKey,
            DisplayName: workspaceEntry.WorkspaceSummary.DisplayName,
            SourcePath: workspaceEntry.WorkspaceSummary.SourcePath,
            CreatedAtUtc: existingMetadata?.CreatedAtUtc ?? openedAtUtc,
            LastOpenedAtUtc: openedAtUtc,
            LastBundleId: existingMetadata?.LastBundleId,
            LastAssetId: existingMetadata?.LastAssetId,
            LastOutputDirectory: existingMetadata?.LastOutputDirectory,
            BundleSearchKeyword: existingMetadata?.BundleSearchKeyword ?? string.Empty,
            AssetSearchKeyword: existingMetadata?.AssetSearchKeyword ?? string.Empty,
            AssetTypeFilter: existingMetadata?.AssetTypeFilter,
            ShowModifiedOnly: existingMetadata?.ShowModifiedOnly ?? false,
            ShowPreviewableOnly: existingMetadata?.ShowPreviewableOnly ?? false,
            OpenBundleTabs: existingMetadata?.OpenBundleTabs ?? Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: existingMetadata?.ActiveBundleTabId,
            ImportedSources: existingMetadata?.ImportedSources,
            AssetBundleDecryptorId: ResolveAssetBundleDecryptorId(
                workspaceEntry.AssetBundleDecryptorId,
                existingMetadata,
                workspaceEntry),
            ExcludedBundleSourcePaths: existingMetadata?.ExcludedBundleSourcePaths);

        metadataStore.Save(metadata);
    }

    private bool TryGetCurrentWorkspaceContext(
        out WorkspaceSummaryDto workspaceSummary,
        out WorkspaceContextEntry workspaceEntry)
    {
        workspaceSummary = null!;
        workspaceEntry = null!;

        if (_currentWorkspaceSummary is null)
        {
            return false;
        }

        var currentEntry = _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(
                entry.WorkspaceSummary?.WorkspaceId.Value,
                _currentWorkspaceSummary.WorkspaceId.Value,
                StringComparison.Ordinal));
        if (currentEntry is null)
        {
            return false;
        }

        workspaceSummary = _currentWorkspaceSummary;
        workspaceEntry = currentEntry;
        return true;
    }

    private void UpsertWorkspaceEntry(WorkspaceContextEntry entry)
    {
        var existingIndex = _workspaceEntries.FindIndex(existingEntry =>
            string.Equals(existingEntry.WorkspaceKey, entry.WorkspaceKey, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            _workspaceEntries.RemoveAt(existingIndex);
        }

        _workspaceEntries.Insert(0, entry);
        SortWorkspaceEntries();
    }

    private WorkspaceContextEntry? FindExistingEntry(string workspaceKey)
    {
        return _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal));
    }

    private string? ResolveLastActiveWorkspaceKey()
    {
        if (_currentWorkspaceSummary is not null)
        {
            var currentEntry = _workspaceEntries.FirstOrDefault(entry =>
                string.Equals(
                    entry.WorkspaceSummary?.WorkspaceId.Value,
                    _currentWorkspaceSummary.WorkspaceId.Value,
                    StringComparison.Ordinal));
            if (currentEntry is not null)
            {
                return currentEntry.WorkspaceKey;
            }
        }

        return _workspaceEntries.FirstOrDefault()?.WorkspaceKey;
    }

    private void SortWorkspaceEntries()
    {
        if (_workspaceEntries.Count <= 1)
        {
            return;
        }

        var orderedEntries = _workspaceEntries
            .OrderByDescending(static entry => entry.IsPinned)
            .ThenByDescending(static entry => entry.LastActivatedAtUtc)
            .ToArray();

        _workspaceEntries.Clear();
        _workspaceEntries.AddRange(orderedEntries);
    }

    private static WorkspaceAvailabilityStatus ResolveAvailabilityStatus(
        string? sourcePath,
        string? workspaceDataRootPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath) &&
            !Directory.Exists(workspaceDataRootPath))
        {
            return WorkspaceAvailabilityStatus.MissingWorkspaceDataRootDirectory;
        }

        if (!string.IsNullOrWhiteSpace(sourcePath) &&
            !Directory.Exists(sourcePath))
        {
            return WorkspaceAvailabilityStatus.MissingSourceDirectory;
        }

        return WorkspaceAvailabilityStatus.Available;
    }

    private string ResolveWorkspaceDataRootPath(
        string? workspaceDataRootPath,
        WorkspaceContextEntry? existingEntry)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return workspaceDataRootPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existingEntry?.WorkspaceDataRootPath))
        {
            return existingEntry.WorkspaceDataRootPath;
        }

        return _workspaceMetadataStoreFactory?.GetDefaultRootDirectoryPath() ?? string.Empty;
    }

    private WorkspaceMetadataDto? LoadWorkspaceMetadata(string workspaceKey, string? workspaceDataRootPath)
    {
        if (_workspaceMetadataStoreFactory is null ||
            string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(workspaceDataRootPath);
        return metadataStore.Load(workspaceKey);
    }

    private static string? ResolveAssetBundleDecryptorId(
        string? requestedAssetBundleDecryptorId,
        WorkspaceMetadataDto? existingMetadata,
        WorkspaceContextEntry? existingEntry)
    {
        var normalizedRequestedDecryptorId = NormalizeOptionalValue(requestedAssetBundleDecryptorId);
        var normalizedMetadataDecryptorId = NormalizeOptionalValue(existingMetadata?.AssetBundleDecryptorId);
        var normalizedExistingEntryDecryptorId = NormalizeOptionalValue(existingEntry?.AssetBundleDecryptorId);

        // “自动识别”只是工作区创建/打开阶段的策略标记。
        // 一旦导入链路已经把它解析成真实解密器，就优先展示和持久化真实结果，避免 UI 长期停留在策略值上。
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
        WorkspaceContextEntry? existingEntry,
        WorkspaceMetadataDto? metadata)
    {
        return PreferResolvedDecryptorId(
            NormalizeOptionalValue(metadata?.AssetBundleDecryptorId),
            NormalizeOptionalValue(existingEntry?.AssetBundleDecryptorId));
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

    private static WorkspaceBrowserStateSnapshotDto MapToBrowserStateSnapshot(WorkspaceMetadataDto metadata)
    {
        return new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: metadata.BundleSearchKeyword,
            AssetSearchKeyword: metadata.AssetSearchKeyword,
            AssetTypeFilter: metadata.AssetTypeFilter,
            ShowModifiedOnly: metadata.ShowModifiedOnly,
            ShowPreviewableOnly: metadata.ShowPreviewableOnly,
            OpenBundleTabs: metadata.OpenBundleTabs,
            ActiveBundleTabId: metadata.ActiveBundleTabId,
            LastBundleId: metadata.LastBundleId,
            LastAssetId: metadata.LastAssetId);
    }

    private static string BuildLastKnownState(WorkspaceSummaryDto workspaceSummary) =>
        $"bundleCount={workspaceSummary.BundleCount};assetCount={workspaceSummary.AssetCount}";
}
