namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetPaneDragReplacementContractTests
{
    [Fact]
    public void AssetPaneDragReplacement_ShouldUnifyNormalAndElevatedExactRowRouting()
    {
        var projectRoot = GetPresentationProjectRoot();
        var targetCoordinator = Read(projectRoot,
            "Views", "BundleExplorer", "Controls", "AssetPaneDragReplacementTargetCoordinator.cs");
        var view = Read(projectRoot,
            "Views", "BundleExplorer", "Controls", "AssetPaneView.DragExport.cs");
        var page = Read(projectRoot,
            "Views", "BundleExplorer", "BundleExplorerPage.AssetDragReplacement.cs");
        var importRoute = Read(projectRoot,
            "Views", "BundleExplorer", "BundleExplorerPage.DragDrop.cs");
        var routerContract = Read(projectRoot,
            "Services", "Platform", "DragDrop", "IElevatedFileDropRouter.cs");
        var router = Read(projectRoot,
            "Services", "Platform", "DragDrop", "ElevatedFileDropRouter.cs");

        Assert.Contains("StandardDataFormats.StorageItems", targetCoordinator);
        Assert.Contains("GetStorageItemsAsync", targetCoordinator);
        Assert.Contains("TryResolveTarget(listView, args.GetPosition(listView)", targetCoordinator);
        Assert.Contains("listView.ContainerFromItem(candidate)", targetCoordinator);
        Assert.Contains("TransformToVisual(listView)", targetCoordinator);
        Assert.Contains("bounds.Contains(position)", targetCoordinator);
        Assert.DoesNotContain("FindElementsInHostCoordinates(position, listView)", targetCoordinator);
        var dragOver = targetCoordinator[
            targetCoordinator.IndexOf("private void OnDragOver", StringComparison.Ordinal)..
            targetCoordinator.IndexOf("private async void OnDrop", StringComparison.Ordinal)];
        Assert.DoesNotContain("StandardDataFormats.StorageItems", dragOver);
        Assert.Contains("ElevatedFileDropTargetPriorities.Item", targetCoordinator);
        Assert.Contains("HitTest: position => TryResolveTarget", targetCoordinator);
        Assert.Contains("_requestReplacementAsync(item, context.Paths)", targetCoordinator);
        Assert.Contains("DragReplacementRequested", view);
        Assert.Contains("RegisterElevatedDragReplacementTargets", view);
        Assert.Contains("RegisterElevatedAssetReplacementDropTargets", page);
        Assert.Contains("UnregisterElevatedAssetReplacementDropTargets", page);
        Assert.Contains("SuppressWorkspaceImportForAssetReplacement", importRoute);
        Assert.Contains("Func<Point, bool>? HitTest", routerContract);
        Assert.Contains("AcceptsPosition(candidate, localPosition)", router);
    }

    [Fact]
    public void AssetPaneDragReplacement_ShouldConfirmBeforeReusingExistingReplacementChains()
    {
        var projectRoot = GetPresentationProjectRoot();
        var coordinator = Read(projectRoot,
            "Views", "BundleExplorer", "Coordinators", "BundleExplorerAssetDragReplacementCoordinator.cs");
        var actionHandler = Read(projectRoot,
            "ViewModels", "BundleExplorer", "ActionHandlers", "BundleExplorerDragReplacementActionHandler.cs");
        var spineHandler = Read(projectRoot,
            "ViewModels", "BundleExplorer", "ActionHandlers", "BundleExplorerSpineEditActionHandler.cs");

        var assetConfirm = coordinator.IndexOf("ConfirmAsync(", StringComparison.Ordinal);
        var assetApply = coordinator.IndexOf("DragReplacementCommands.ApplyAssetAsync", StringComparison.Ordinal);
        var spinePrepare = coordinator.IndexOf("DragReplacementCommands.PrepareSpineGroupAsync", StringComparison.Ordinal);
        var spineConfirm = coordinator.IndexOf("ResourceKeys.AssetPaneDragReplacementSpineConfirmFormat", StringComparison.Ordinal);
        var spineApply = coordinator.IndexOf("DragReplacementCommands.ApplySpineGroupAsync", StringComparison.Ordinal);

        Assert.True(assetConfirm >= 0 && assetConfirm < assetApply);
        Assert.True(spinePrepare >= 0 && spinePrepare < spineConfirm && spineConfirm < spineApply);
        Assert.Contains("GetFileReplacementCandidateAsync", actionHandler);
        Assert.Contains("ApplyFileReplacementAsync", actionHandler);
        Assert.Contains("CreateSourceMatchDraftAsync", actionHandler);
        Assert.Contains("ApplySpineEditAsync", actionHandler);
        Assert.Contains("MatchSpineReplacementSourcesAsync", spineHandler);
        Assert.Contains("draft.ApplyMatches(matches)", spineHandler);
        Assert.Contains("requireSpineSkeletonFormatMatch", spineHandler);
        Assert.Contains("BeginNotificationBatch", spineHandler);
        Assert.Contains("CreateSpineReplacementOperationContext(group, draft)", spineHandler);
    }

    [Fact]
    public void AssetPaneDragReplacement_ShouldNotMaskSpineMatchFailureWithNoMatchesWarning()
    {
        var projectRoot = GetPresentationProjectRoot();
        var coordinator = Read(projectRoot,
            "Views", "BundleExplorer", "Coordinators", "BundleExplorerAssetDragReplacementCoordinator.cs");
        var commandCoordinator = Read(projectRoot,
            "ViewModels", "BundleExplorer", "Coordinators", "BundleExplorerDragReplacementCommandCoordinator.cs");

        Assert.Contains("SpineDragReplacementPreparationResult", commandCoordinator);
        Assert.Contains("failureWasReported = errors.Count > 0", commandCoordinator);
        Assert.Contains("if (!preparation.FailureWasReported)", coordinator);
        Assert.Contains("ResourceKeys.AssetPaneDragReplacementNoSpineMatchesFormat", coordinator);
    }

    [Fact]
    public void AssetPaneDragReplacement_ShouldLocalizeConfirmationAndDiagnostics()
    {
        var projectRoot = GetPresentationProjectRoot();
        foreach (var culture in new[] { "zh-CN", "en-US" })
        {
            var resources = Read(projectRoot, "Strings", culture, "Resources.resw");
            Assert.Contains("AssetPaneView.DragReplacement.Confirm.Title", resources);
            Assert.Contains("AssetPaneView.DragReplacement.AssetConfirm.Format", resources);
            Assert.Contains("AssetPaneView.DragReplacement.SpineConfirm.Format", resources);
            Assert.Contains("AssetPaneView.DragReplacement.InvalidSource", resources);
            Assert.Contains("AssetPaneView.DragReplacement.NoSpineMatches.Format", resources);
        }
    }

    private static string Read(string root, params string[] pathParts) =>
        File.ReadAllText(Path.Combine([root, .. pathParts]));

    private static string GetPresentationProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectRoot = Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI");
            if (Directory.Exists(projectRoot))
            {
                return projectRoot;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Presentation project root was not found.");
    }
}
