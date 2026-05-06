using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 基于工作区目录仓储与元数据仓储，为 Presentation 提供统一的持久化入口。
/// </summary>
public sealed class WorkspaceContextPersistenceService : IWorkspaceContextPersistenceService
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceImportFileSystemService _workspaceImportFileSystemService;

    public WorkspaceContextPersistenceService(
        IWorkspaceCatalogStore workspaceCatalogStore,
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceImportFileSystemService workspaceImportFileSystemService)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceImportFileSystemService = workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService));
    }

    public WorkspaceCatalogSnapshotDto LoadWorkspaceCatalogSnapshot() => _workspaceCatalogStore.Load();

    public void SaveWorkspaceCatalogSnapshot(WorkspaceCatalogSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _workspaceCatalogStore.Save(snapshot);
    }

    public WorkspaceMetadataDto? LoadWorkspaceMetadata(string workspaceKey, string? workspaceDataRootPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        return _workspaceMetadataStoreFactory.Create(workspaceDataRootPath).Load(workspaceKey);
    }

    public WorkspaceBrowserStateSnapshotDto? LoadWorkspaceBrowserState(string workspaceKey, string? workspaceDataRootPath)
    {
        var metadata = LoadWorkspaceMetadata(workspaceKey, workspaceDataRootPath);
        return metadata is null ? null : MapToBrowserStateSnapshot(metadata);
    }

    /// <summary>
    /// 工作区被创建、打开或再次激活时，先统一补齐最小元数据骨架。
    /// 这样后续页面级浏览状态只需要做增量回写，而不必在 Presentation 中重复拼装默认字段。
    /// </summary>
    public void SaveWorkspaceActivationMetadata(WorkspaceActivationMetadataSaveRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var metadataStore = _workspaceMetadataStoreFactory.Create(request.WorkspaceDataRootPath);
        var existingMetadata = metadataStore.Load(request.WorkspaceKey);
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: request.WorkspaceKey,
            DisplayName: request.DisplayName,
            SourcePath: request.SourcePath,
            CreatedAtUtc: existingMetadata?.CreatedAtUtc ?? request.OpenedAtUtc,
            LastOpenedAtUtc: request.OpenedAtUtc,
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
            AssetBundleDecryptorId: string.IsNullOrWhiteSpace(request.AssetBundleDecryptorId)
                ? existingMetadata?.AssetBundleDecryptorId
                : request.AssetBundleDecryptorId.Trim(),
            ExcludedBundleSourcePaths: existingMetadata?.ExcludedBundleSourcePaths,
            OpenAssetEditorWindows: existingMetadata?.OpenAssetEditorWindows);

        metadataStore.Save(metadata);
    }

    /// <summary>
    /// 页面级浏览状态落盘时，应用层统一负责与既有元数据做字段级合并，
    /// 避免 Presentation 了解导入来源、输出目录和解密器等非页面状态字段如何保留。
    /// </summary>
    public void SaveWorkspaceBrowserState(WorkspaceBrowserStateSaveRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var metadataStore = _workspaceMetadataStoreFactory.Create(request.WorkspaceDataRootPath);
        var existingMetadata = metadataStore.Load(request.WorkspaceKey);
        var now = DateTimeOffset.UtcNow;
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: request.WorkspaceKey,
            DisplayName: request.DisplayName,
            SourcePath: request.SourcePath,
            CreatedAtUtc: existingMetadata?.CreatedAtUtc ?? now,
            LastOpenedAtUtc: existingMetadata?.LastOpenedAtUtc ?? now,
            LastBundleId: request.BrowserState.LastBundleId,
            LastAssetId: request.BrowserState.LastAssetId,
            LastOutputDirectory: existingMetadata?.LastOutputDirectory,
            BundleSearchKeyword: request.BrowserState.BundleSearchKeyword,
            AssetSearchKeyword: request.BrowserState.AssetSearchKeyword,
            AssetTypeFilter: request.BrowserState.AssetTypeFilter,
            ShowModifiedOnly: request.BrowserState.ShowModifiedOnly,
            ShowPreviewableOnly: request.BrowserState.ShowPreviewableOnly,
            OpenBundleTabs: request.BrowserState.OpenBundleTabs,
            ActiveBundleTabId: request.BrowserState.ActiveBundleTabId,
            ImportedSources: existingMetadata?.ImportedSources,
            AssetBundleDecryptorId: string.IsNullOrWhiteSpace(request.AssetBundleDecryptorId)
                ? existingMetadata?.AssetBundleDecryptorId
                : request.AssetBundleDecryptorId.Trim(),
            ExcludedBundleSourcePaths: existingMetadata?.ExcludedBundleSourcePaths,
            OpenAssetEditorWindows: request.BrowserState.OpenAssetEditorWindows);

        metadataStore.Save(metadata);
    }

    public string GetDefaultWorkspaceDataRootPath() => _workspaceMetadataStoreFactory.GetDefaultRootDirectoryPath();

    public WorkspaceImportPathKind ResolveOptionalPathKind(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspaceImportPathKind.Unknown;
        }

        return _workspaceImportFileSystemService.ResolvePathKind(path.Trim());
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
            LastAssetId: metadata.LastAssetId,
            OpenAssetEditorWindows: metadata.OpenAssetEditorWindows);
    }
}
