using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 将指定资源包从工作区浏览上下文中移除，并同步清理对应缓存。
/// </summary>
public sealed class RemoveWorkspaceBundleHandler
    : ICommandHandler<RemoveWorkspaceBundleCommand, Result<WorkspaceBundleRemovalResultDto>>
{
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;

    public RemoveWorkspaceBundleHandler(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator ?? NoOpBundleArtifactSessionInvalidator.Instance;
    }

    public Task<Result<WorkspaceBundleRemovalResultDto>> HandleAsync(
        RemoveWorkspaceBundleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Failure(
                ApplicationErrors.WorkspaceImportWorkspaceKeyRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Failure(
                ApplicationErrors.WorkspaceImportDataRootPathRequired()));
        }

        var normalizedBundleSourcePath = WorkspaceImportPathUtility.NormalizePath(command.BundleSourcePath);
        if (string.IsNullOrWhiteSpace(normalizedBundleSourcePath))
        {
            return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Failure(
                ApplicationErrors.WorkspaceImportBundleSourcePathRequired()));
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var existingMetadata = metadataStore.Load(command.WorkspaceKey);
        if (existingMetadata is null)
        {
            return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Failure(
                ApplicationErrors.WorkspaceImportMetadataNotFound(command.WorkspaceKey)));
        }

        var importedSources = existingMetadata.ImportedSources ?? Array.Empty<WorkspaceImportedSourceDto>();
        var remainingImportedSources = importedSources
            .Where(source => !string.Equals(
                WorkspaceImportPathUtility.NormalizePath(source.Path),
                normalizedBundleSourcePath,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var removedDirectImportSource = remainingImportedSources.Length != importedSources.Count;

        var shouldRemainExcluded = remainingImportedSources.Any(source =>
            WorkspaceImportPathUtility.IsCoveredByImportedSource(source, normalizedBundleSourcePath));

        var excludedBundleSourcePaths = (existingMetadata.ExcludedBundleSourcePaths ?? Array.Empty<string>())
            .Select(WorkspaceImportPathUtility.NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removedExistingExcludedPath = excludedBundleSourcePaths.RemoveAll(path =>
            string.Equals(path, normalizedBundleSourcePath, StringComparison.OrdinalIgnoreCase)) > 0;
        var addedExcludedBundleSourcePath = false;
        if (shouldRemainExcluded)
        {
            excludedBundleSourcePaths.Add(normalizedBundleSourcePath);
            addedExcludedBundleSourcePath = !removedExistingExcludedPath;
        }

        var remainingOpenTabs = existingMetadata.OpenBundleTabs
            .Where(tab => !string.Equals(tab.BundleId, command.BundleId.Value, StringComparison.Ordinal))
            .ToArray();
        var remainingOpenAssetEditorWindows = RemoveBundleAssetTabsFromEditorWindows(
            existingMetadata.OpenAssetEditorWindows,
            command.BundleId.Value);
        var nextActiveBundleTabId = string.IsNullOrWhiteSpace(existingMetadata.ActiveBundleTabId) ||
                                    remainingOpenTabs.Any(tab => string.Equals(tab.BundleId, existingMetadata.ActiveBundleTabId, StringComparison.Ordinal))
            ? existingMetadata.ActiveBundleTabId
            : remainingOpenTabs.FirstOrDefault()?.BundleId;

        var updatedMetadata = new WorkspaceMetadataDto(
            WorkspaceKey: existingMetadata.WorkspaceKey,
            DisplayName: existingMetadata.DisplayName,
            SourcePath: existingMetadata.SourcePath,
            CreatedAtUtc: existingMetadata.CreatedAtUtc,
            LastOpenedAtUtc: existingMetadata.LastOpenedAtUtc,
            LastBundleId: string.Equals(existingMetadata.LastBundleId, command.BundleId.Value, StringComparison.Ordinal)
                ? null
                : existingMetadata.LastBundleId,
            LastAssetId: string.Equals(existingMetadata.LastBundleId, command.BundleId.Value, StringComparison.Ordinal)
                ? null
                : existingMetadata.LastAssetId,
            LastOutputDirectory: existingMetadata.LastOutputDirectory,
            BundleSearchKeyword: existingMetadata.BundleSearchKeyword,
            AssetSearchKeyword: existingMetadata.AssetSearchKeyword,
            AssetTypeFilter: existingMetadata.AssetTypeFilter,
            ShowModifiedOnly: existingMetadata.ShowModifiedOnly,
            ShowPreviewableOnly: existingMetadata.ShowPreviewableOnly,
            OpenBundleTabs: remainingOpenTabs,
            ActiveBundleTabId: nextActiveBundleTabId,
            ImportedSources: remainingImportedSources,
            AssetBundleDecryptorId: existingMetadata.AssetBundleDecryptorId,
            ExcludedBundleSourcePaths: excludedBundleSourcePaths,
            OpenAssetEditorWindows: remainingOpenAssetEditorWindows);
        metadataStore.Save(updatedMetadata);

        var bundleCacheKey = WorkspaceImportPathUtility.CreateBundleCacheKey(normalizedBundleSourcePath);
        var spineGroupCatalogCacheStore = _workspaceDataStoreFactory.CreateSpineGroupCatalogCacheStore(command.WorkspaceDataRootPath);
        spineGroupCatalogCacheStore.DeleteWorkspace(command.WorkspaceKey);
        if (!string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(command.WorkspaceDataRootPath);
            var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(command.WorkspaceDataRootPath);
            var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
            WorkspaceBundleArtifactCleanupHelper.DeleteBundleArtifacts(
                command.WorkspaceKey,
                bundleCacheKey,
                bundleArtifactStore,
                bundleIndexCacheStore,
                previewCacheStore,
                bundleArtifactSessionInvalidator: _bundleArtifactSessionInvalidator);
        }

        return Task.FromResult(Result<WorkspaceBundleRemovalResultDto>.Success(
            new WorkspaceBundleRemovalResultDto(
                WorkspaceKey: updatedMetadata.WorkspaceKey,
                BundleId: command.BundleId,
                BundleSourcePath: normalizedBundleSourcePath,
                RemovedDirectImportSource: removedDirectImportSource,
                AddedExcludedBundleSourcePath: addedExcludedBundleSourcePath,
                RemainingRegisteredSourceCount: remainingImportedSources.Length)));
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? RemoveBundleAssetTabsFromEditorWindows(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? openAssetEditorWindows,
        string removedBundleId)
    {
        if (openAssetEditorWindows is null || openAssetEditorWindows.Count == 0)
        {
            return openAssetEditorWindows;
        }

        var remainingWindows = new List<WorkspaceAssetEditorWindowStateDto>(openAssetEditorWindows.Count);
        foreach (var window in openAssetEditorWindows)
        {
            var remainingDocumentTabs = GetWindowDocumentTabs(window)
                .Where(tab => !string.Equals(GetBundleId(tab), removedBundleId, StringComparison.Ordinal))
                .ToArray();
            if (remainingDocumentTabs.Length == 0)
            {
                continue;
            }

            var activeTab = remainingDocumentTabs.FirstOrDefault(tab => IsActiveWindowDocumentTab(window, tab))
                            ?? remainingDocumentTabs[0];
            remainingWindows.Add(window with
            {
                OpenDocumentTabs = remainingDocumentTabs,
                OpenAssetTabs = remainingDocumentTabs
                    .Select(static tab => tab.AssetTab)
                    .Where(static tab => tab is not null)
                    .Cast<WorkspaceAssetEditorWindowAssetTabDto>()
                    .ToArray(),
                ActiveDocumentKind = activeTab.Kind,
                ActiveBundleId = GetBundleId(activeTab),
                ActiveAssetId = activeTab.AssetTab?.AssetId,
                ActiveSpineGroupId = activeTab.SpineGroupTab?.GroupId
            });
        }

        return remainingWindows;
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowDocumentTabDto> GetWindowDocumentTabs(
        WorkspaceAssetEditorWindowStateDto window)
    {
        return window.OpenDocumentTabs is { Count: > 0 } documentTabs
            ? documentTabs
            : window.OpenAssetTabs
                .Select(static assetTab => new WorkspaceAssetEditorWindowDocumentTabDto(
                    WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                    AssetTab: assetTab))
                .ToArray();
    }

    private static bool IsActiveWindowDocumentTab(
        WorkspaceAssetEditorWindowStateDto window,
        WorkspaceAssetEditorWindowDocumentTabDto documentTab)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            return (string.IsNullOrWhiteSpace(window.ActiveDocumentKind) ||
                    string.Equals(window.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.AssetKind, StringComparison.Ordinal)) &&
                   string.Equals(assetTab.BundleId, window.ActiveBundleId, StringComparison.Ordinal) &&
                   string.Equals(assetTab.AssetId, window.ActiveAssetId, StringComparison.Ordinal);
        }

        return documentTab.SpineGroupTab is { } spineGroupTab &&
               string.Equals(window.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, StringComparison.Ordinal) &&
               string.Equals(spineGroupTab.BundleId, window.ActiveBundleId, StringComparison.Ordinal) &&
               string.Equals(spineGroupTab.GroupId, window.ActiveSpineGroupId, StringComparison.Ordinal);
    }

    private static string? GetBundleId(WorkspaceAssetEditorWindowDocumentTabDto documentTab) =>
        documentTab.AssetTab?.BundleId ?? documentTab.SpineGroupTab?.BundleId;
}
