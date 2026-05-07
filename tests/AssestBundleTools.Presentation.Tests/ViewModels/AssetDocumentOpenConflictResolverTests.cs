using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetDocumentOpenConflictResolverTests
{
    [Fact]
    public void TryResolve_ShouldPreferMainWindow_WhenRequested()
    {
        var mainAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var editorAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var mainBundleTabs = new[]
        {
            CreateBundleDocumentTab("bundle-a", mainAssetTab)
        };
        var editorWindowGroups = new[]
        {
            CreateAssetDocumentGroup(editorAssetTab)
        };

        var resolved = AssetDocumentOpenConflictResolver.TryResolve(
            new BundleId("bundle-a"),
            new AssetId("asset-1"),
            mainBundleTabs,
            editorWindowGroups,
            includeMainWindow: true,
            preferMainWindow: true,
            preferredEditorWindowIndex: null,
            excludedEditorWindowIndex: null,
            out var resolution);

        Assert.True(resolved);
        Assert.Equal(AssetDocumentOpenConflictHostKind.MainWindow, resolution.HostKind);
        Assert.Same(mainBundleTabs[0], resolution.BundleTab);
        Assert.Same(mainAssetTab, resolution.AssetTab);
    }

    [Fact]
    public void TryResolve_ShouldSkipExcludedEditorWindow_AndPreferAnotherEditorBeforeMain()
    {
        var sourceEditorAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var otherEditorAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var mainAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var mainBundleTabs = new[]
        {
            CreateBundleDocumentTab("bundle-a", mainAssetTab)
        };
        var editorWindowGroups = new[]
        {
            CreateAssetDocumentGroup(sourceEditorAssetTab),
            CreateAssetDocumentGroup(otherEditorAssetTab)
        };

        var resolved = AssetDocumentOpenConflictResolver.TryResolve(
            new BundleId("bundle-a"),
            new AssetId("asset-1"),
            mainBundleTabs,
            editorWindowGroups,
            includeMainWindow: true,
            preferMainWindow: false,
            preferredEditorWindowIndex: null,
            excludedEditorWindowIndex: 0,
            out var resolution);

        Assert.True(resolved);
        Assert.Equal(AssetDocumentOpenConflictHostKind.EditorWindow, resolution.HostKind);
        Assert.Equal(1, resolution.EditorWindowIndex);
        Assert.Same(otherEditorAssetTab, resolution.AssetTab);
    }

    [Fact]
    public void TryResolve_ShouldIgnoreMainWindow_WhenMainWindowIsExcluded()
    {
        var mainAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var mainBundleTabs = new[]
        {
            CreateBundleDocumentTab("bundle-a", mainAssetTab)
        };

        var resolved = AssetDocumentOpenConflictResolver.TryResolve(
            new BundleId("bundle-a"),
            new AssetId("asset-1"),
            mainBundleTabs,
            Array.Empty<AssetDocumentGroupViewModel>(),
            includeMainWindow: false,
            preferMainWindow: false,
            preferredEditorWindowIndex: null,
            excludedEditorWindowIndex: null,
            out _);

        Assert.False(resolved);
    }

    private static BundleDocumentTabViewModel CreateBundleDocumentTab(
        string bundleId,
        params AssetDocumentTabViewModel[] assetTabs)
    {
        var bundleTab = new BundleDocumentTabViewModel(
            new BundleId(bundleId),
            bundleId,
            bundleId,
            isPreview: false);
        foreach (var assetTab in assetTabs)
        {
            bundleTab.AddAssetTab(assetTab, activate: false);
        }

        return bundleTab;
    }

    private static AssetDocumentGroupViewModel CreateAssetDocumentGroup(
        params AssetDocumentTabViewModel[] assetTabs)
    {
        var group = new AssetDocumentGroupViewModel();
        foreach (var assetTab in assetTabs)
        {
            group.AddAssetTab(assetTab, activate: false);
        }

        return group;
    }

    private static AssetDocumentTabViewModel CreateAssetDocumentTab(string bundleId, string assetId)
    {
        var textSet = InspectorInformationTextSetFactory.Create(new TestLocalizationService());
        return new AssetDocumentTabViewModel(
            new BundleId(bundleId),
            new AssetId(assetId),
            assetId,
            textSet);
    }
}
