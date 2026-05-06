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

    public RemoveWorkspaceBundleHandler(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
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
        if (!string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(command.WorkspaceDataRootPath);
            var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
            WorkspaceBundleArtifactCleanupHelper.DeleteBundleArtifacts(
                command.WorkspaceKey,
                bundleCacheKey,
                bundleIndexCacheStore,
                previewCacheStore);
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
            var remainingTabs = window.OpenAssetTabs
                .Where(tab => !string.Equals(tab.BundleId, removedBundleId, StringComparison.Ordinal))
                .ToArray();
            if (remainingTabs.Length == 0)
            {
                continue;
            }

            var hasActiveTab = remainingTabs.Any(tab =>
                string.Equals(tab.BundleId, window.ActiveBundleId, StringComparison.Ordinal) &&
                string.Equals(tab.AssetId, window.ActiveAssetId, StringComparison.Ordinal));
            var activeTab = hasActiveTab ? null : remainingTabs[0];
            remainingWindows.Add(window with
            {
                OpenAssetTabs = remainingTabs,
                ActiveBundleId = hasActiveTab ? window.ActiveBundleId : activeTab?.BundleId,
                ActiveAssetId = hasActiveTab ? window.ActiveAssetId : activeTab?.AssetId
            });
        }

        return remainingWindows;
    }
}
