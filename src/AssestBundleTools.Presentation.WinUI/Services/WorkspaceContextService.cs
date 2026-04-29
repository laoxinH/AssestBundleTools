using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

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
    private readonly IWorkspaceContextCatalogService _workspaceContextCatalogService;
    private WorkspaceSummaryDto? _currentWorkspaceSummary;
    private readonly List<WorkspaceContextEntryDto> _workspaceEntries = [];
    private string? _lastActiveWorkspaceKey;

    public WorkspaceContextService(IWorkspaceContextCatalogService workspaceContextCatalogService)
    {
        _workspaceContextCatalogService = workspaceContextCatalogService
            ?? throw new ArgumentNullException(nameof(workspaceContextCatalogService));
        LoadWorkspaceCatalogSnapshot();
    }

    public event EventHandler? CurrentWorkspaceChanged;

    public event EventHandler? WorkspaceEntriesChanged;

    public WorkspaceSummaryDto? CurrentWorkspaceSummary => _currentWorkspaceSummary;

    public WorkspaceSummaryDto? ActiveWorkspaceSummary => _currentWorkspaceSummary;

    public bool HasCurrentWorkspace => _currentWorkspaceSummary is not null;

    public IReadOnlyList<WorkspaceContextEntryDto> WorkspaceEntries => _workspaceEntries;

    public int WorkspaceEntryCount => _workspaceEntries.Count;

    public bool TryGetCurrentWorkspaceEntry(out WorkspaceContextEntryDto? workspaceEntry)
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
        if (!TryGetCurrentWorkspaceContext(out _, out var workspaceEntry))
        {
            return null;
        }

        return _workspaceContextCatalogService.LoadWorkspaceBrowserState(
            workspaceEntry.WorkspaceKey,
            workspaceEntry.WorkspaceDataRootPath);
    }

    /// <summary>
    /// 尝试根据最近目录索引恢复“上次活动工作区”的打开请求。
    /// 这里只返回 Presentation 层需要的导航参数，
    /// 不会提前创建运行时会话，避免把“启动恢复”逻辑混入 Application 会话仓储。
    /// </summary>
    public bool TryGetLastActiveWorkspaceNavigationRequest(out WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        var restoreRequest = _workspaceContextCatalogService.ResolveLastActiveWorkspaceRestoreRequest(
            _lastActiveWorkspaceKey,
            _workspaceEntries);
        workspaceNavigationRequest = restoreRequest is null
            ? null
            : new WorkspaceNavigationRequest(
                SourcePath: restoreRequest.SourcePath,
                WorkspaceDisplayName: restoreRequest.WorkspaceDisplayName,
                WorkspaceDataRootPath: restoreRequest.WorkspaceDataRootPath,
                OpenTarget: WorkspaceOpenTarget.CurrentWindow,
                AssetBundleDecryptorId: restoreRequest.AssetBundleDecryptorId);
        return workspaceNavigationRequest is not null;
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
        var registrationResult = _workspaceContextCatalogService.CreateRegisteredEntry(
            workspaceKey,
            workspaceSummary,
            workspaceDataRootPath,
            assetBundleDecryptorId,
            existingEntry,
            now);
        var entry = registrationResult.WorkspaceEntry;

        UpsertWorkspaceEntry(entry);
        _currentWorkspaceSummary = workspaceSummary;

        PersistWorkspaceCatalogSnapshot();
        _workspaceContextCatalogService.SaveWorkspaceActivationMetadata(
            entry,
            openedAtUtc: now,
            registrationResult.ExistingMetadata);

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

        if (!TryGetCurrentWorkspaceContext(out var workspaceSummary, out var workspaceEntry))
        {
            return;
        }

        _workspaceContextCatalogService.SaveWorkspaceBrowserState(
            workspaceEntry,
            workspaceSummary,
            browserStateSnapshot);
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
        var updatedEntry = _workspaceContextCatalogService.RefreshActivatedEntry(
            existingEntry,
            DateTimeOffset.UtcNow);

        _workspaceEntries.RemoveAt(existingIndex);
        _workspaceEntries.Insert(0, updatedEntry);
        SortWorkspaceEntries();
        _currentWorkspaceSummary = updatedEntry.WorkspaceSummary;

        PersistWorkspaceCatalogSnapshot();
        if (updatedEntry.WorkspaceSummary is not null)
        {
            _workspaceContextCatalogService.SaveWorkspaceActivationMetadata(
                updatedEntry,
                updatedEntry.LastActivatedAtUtc);
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
        var catalogState = _workspaceContextCatalogService.LoadCatalogState(BuildLiveSessions());
        _lastActiveWorkspaceKey = catalogState.LastActiveWorkspaceKey;
        _workspaceEntries.Clear();
        _workspaceEntries.AddRange(catalogState.WorkspaceEntries);
    }

    private void PersistWorkspaceCatalogSnapshot()
    {
        var snapshot = _workspaceContextCatalogService.SaveCatalogState(
            _workspaceEntries,
            _currentWorkspaceSummary);
        _lastActiveWorkspaceKey = snapshot.LastActiveWorkspaceKey;
    }

    private bool TryGetCurrentWorkspaceContext(
        out WorkspaceSummaryDto workspaceSummary,
        out WorkspaceContextEntryDto workspaceEntry)
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

    private void UpsertWorkspaceEntry(WorkspaceContextEntryDto entry)
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

    private WorkspaceContextEntryDto? FindExistingEntry(string workspaceKey)
    {
        return _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal));
    }

    private IReadOnlyList<WorkspaceContextLiveSessionDto> BuildLiveSessions()
    {
        return _workspaceEntries
            .Where(static entry => entry.WorkspaceSummary is not null)
            .Select(static entry => new WorkspaceContextLiveSessionDto(
                WorkspaceKey: entry.WorkspaceKey,
                WorkspaceDataRootPath: entry.WorkspaceDataRootPath,
                AssetBundleDecryptorId: entry.AssetBundleDecryptorId,
                WorkspaceSummary: entry.WorkspaceSummary!))
            .ToArray();
    }

    private void SortWorkspaceEntries()
    {
        if (_workspaceEntries.Count <= 1)
        {
            return;
        }

        var orderedEntries = _workspaceContextCatalogService.SortEntries(_workspaceEntries);
        _workspaceEntries.Clear();
        _workspaceEntries.AddRange(orderedEntries);
    }
}
