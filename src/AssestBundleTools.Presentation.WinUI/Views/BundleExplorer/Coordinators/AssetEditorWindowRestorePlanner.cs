using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestorePlanner
{
    public static AssetEditorWindowRestorePlan CreatePlan(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto> pendingWindows,
        Func<WorkspaceAssetEditorWindowAssetTabDto, bool> canRestoreAssetTab,
        IReadOnlySet<string>? openAssetKeys = null,
        Func<WorkspaceAssetEditorWindowSpineGroupTabDto, bool>? canRestoreSpineGroupTab = null,
        IReadOnlySet<string>? openSpineGroupKeys = null)
    {
        ArgumentNullException.ThrowIfNull(pendingWindows);
        ArgumentNullException.ThrowIfNull(canRestoreAssetTab);

        var restorableWindows = new List<WorkspaceAssetEditorWindowStateDto>(pendingWindows.Count);
        var unavailableWindows = new List<WorkspaceAssetEditorWindowStateDto>();
        var conflictedWindows = new List<WorkspaceAssetEditorWindowStateDto>();
        var reservedAssetKeys = new HashSet<string>(
            openAssetKeys is null ? Array.Empty<string>() : openAssetKeys,
            StringComparer.Ordinal);
        var reservedSpineGroupKeys = new HashSet<string>(
            openSpineGroupKeys is null ? Array.Empty<string>() : openSpineGroupKeys,
            StringComparer.Ordinal);

        foreach (var pendingWindow in pendingWindows)
        {
            var sourceTabs = GetSourceDocumentTabs(pendingWindow);
            var restorableTabs = new List<WorkspaceAssetEditorWindowDocumentTabDto>(sourceTabs.Count);
            var unavailableTabs = new List<WorkspaceAssetEditorWindowDocumentTabDto>();
            var conflictedTabs = new List<WorkspaceAssetEditorWindowDocumentTabDto>();

            foreach (var documentTab in sourceTabs)
            {
                if (!CanRestoreDocumentTab(documentTab, canRestoreAssetTab, canRestoreSpineGroupTab))
                {
                    unavailableTabs.Add(documentTab);
                    continue;
                }

                if (!TryReserveDocumentTabKey(documentTab, reservedAssetKeys, reservedSpineGroupKeys))
                {
                    conflictedTabs.Add(documentTab);
                    continue;
                }

                restorableTabs.Add(documentTab);
            }

            if (restorableTabs.Count > 0)
            {
                restorableWindows.Add(CreateWindowSnapshotWithDocumentTabs(pendingWindow, restorableTabs));
            }

            if (unavailableTabs.Count > 0)
            {
                unavailableWindows.Add(CreateWindowSnapshotWithDocumentTabs(pendingWindow, unavailableTabs));
            }

            if (conflictedTabs.Count > 0)
            {
                conflictedWindows.Add(CreateWindowSnapshotWithDocumentTabs(pendingWindow, conflictedTabs));
            }
        }

        return new AssetEditorWindowRestorePlan(restorableWindows, unavailableWindows, conflictedWindows);
    }

    public static AssetEditorWindowLayoutSnapshot? CreateRestoredLayout(
        WorkspaceAssetEditorWindowLayoutDto layout)
    {
        var restoredLayout = new AssetEditorWindowLayoutSnapshot(
            layout.X,
            layout.Y,
            layout.Width,
            layout.Height);
        return AssetEditorWindowLayoutSafety.CanRestoreSize(restoredLayout)
            ? restoredLayout
            : null;
    }

    private static WorkspaceAssetEditorWindowStateDto CreateWindowSnapshotWithDocumentTabs(
        WorkspaceAssetEditorWindowStateDto source,
        IReadOnlyList<WorkspaceAssetEditorWindowDocumentTabDto> documentTabs)
    {
        var activeTab = documentTabs.FirstOrDefault(tab => IsActiveDocumentTab(source, tab))
                        ?? documentTabs[0];

        return source with
        {
            OpenAssetTabs = documentTabs
                .Select(static tab => tab.AssetTab)
                .Where(static tab => tab is not null)
                .Cast<WorkspaceAssetEditorWindowAssetTabDto>()
                .ToArray(),
            OpenDocumentTabs = documentTabs.ToArray(),
            ActiveDocumentKind = activeTab.Kind,
            ActiveBundleId = GetBundleId(activeTab),
            ActiveAssetId = activeTab.AssetTab?.AssetId,
            ActiveSpineGroupId = activeTab.SpineGroupTab?.GroupId
        };
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowDocumentTabDto> GetSourceDocumentTabs(
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

    private static bool CanRestoreDocumentTab(
        WorkspaceAssetEditorWindowDocumentTabDto documentTab,
        Func<WorkspaceAssetEditorWindowAssetTabDto, bool> canRestoreAssetTab,
        Func<WorkspaceAssetEditorWindowSpineGroupTabDto, bool>? canRestoreSpineGroupTab)
    {
        return documentTab.AssetTab is { } assetTab
            ? canRestoreAssetTab(assetTab)
            : documentTab.SpineGroupTab is { } spineGroupTab &&
              canRestoreSpineGroupTab is not null &&
              canRestoreSpineGroupTab(spineGroupTab);
    }

    private static bool TryReserveDocumentTabKey(
        WorkspaceAssetEditorWindowDocumentTabDto documentTab,
        HashSet<string> reservedAssetKeys,
        HashSet<string> reservedSpineGroupKeys)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            return AssetDocumentIdentityKey.TryCreate(assetTab.BundleId, assetTab.AssetId, out var assetKey) &&
                   reservedAssetKeys.Add(assetKey);
        }

        return documentTab.SpineGroupTab is { } spineGroupTab &&
               SpineGroupDocumentIdentityKey.TryCreate(spineGroupTab.BundleId, spineGroupTab.GroupId, out var spineGroupKey) &&
               reservedSpineGroupKeys.Add(spineGroupKey);
    }

    private static bool IsActiveDocumentTab(
        WorkspaceAssetEditorWindowStateDto source,
        WorkspaceAssetEditorWindowDocumentTabDto documentTab)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            return IsActiveAssetDocument(source, assetTab);
        }

        return documentTab.SpineGroupTab is { } spineGroupTab &&
               string.Equals(source.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, StringComparison.Ordinal) &&
               string.Equals(spineGroupTab.BundleId, source.ActiveBundleId, StringComparison.Ordinal) &&
               string.Equals(spineGroupTab.GroupId, source.ActiveSpineGroupId, StringComparison.Ordinal);
    }

    private static bool IsActiveAssetDocument(
        WorkspaceAssetEditorWindowStateDto source,
        WorkspaceAssetEditorWindowAssetTabDto assetTab)
    {
        return (string.IsNullOrWhiteSpace(source.ActiveDocumentKind) ||
                string.Equals(source.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.AssetKind, StringComparison.Ordinal)) &&
               string.Equals(assetTab.BundleId, source.ActiveBundleId, StringComparison.Ordinal) &&
               string.Equals(assetTab.AssetId, source.ActiveAssetId, StringComparison.Ordinal);
    }

    private static string? GetBundleId(WorkspaceAssetEditorWindowDocumentTabDto documentTab) =>
        documentTab.AssetTab?.BundleId ?? documentTab.SpineGroupTab?.BundleId;
}

internal sealed record AssetEditorWindowRestorePlan(
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> RestorableWindows,
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> PendingUnavailableWindows,
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> PendingConflictedWindows);
