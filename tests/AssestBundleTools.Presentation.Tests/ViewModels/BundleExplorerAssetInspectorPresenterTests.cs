using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetInspectorPresenterTests
{
    [Fact]
    public void PresentAssetDocumentTab_ShouldProjectDependencyNavigationTarget()
    {
        var localizationService = new TestLocalizationService();
        var targetBundleId = new BundleId("bundle-textures");
        var targetAssetId = new AssetId("asset-texture");
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        bundlePane.RebuildBundleGroups(CreateBundleTree(targetBundleId, "textures.ab"));
        var presenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var assetTab = new AssetDocumentTabViewModel(
            new BundleId("bundle-materials"),
            new AssetId("asset-material"),
            "paopao_01",
            InspectorInformationTextSetFactory.Create(localizationService));
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            assetTab.BundleId,
            assetTab.AssetId) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = "Material",
            Dependencies =
            [
                new AssetDependencyDto(
                    targetAssetId,
                    targetBundleId,
                    "HeroTexture",
                    AssetType.Texture2D,
                    PathId: 200,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };

        presenter.PresentAssetDocumentTab(assetTab, details, canEdit: true, isBusy: false);

        var dependency = Assert.Single(assetTab.Inspector.AssetDependencies);
        Assert.Equal("textures.ab", dependency.BundleName);
        Assert.Equal(ResourceKeys.InspectorResolvedAssetOpenButton, dependency.OpenButtonText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, dependency.OpenButtonVisibility);
        Assert.NotNull(dependency.Target);
        Assert.Equal(targetAssetId, dependency.Target.AssetId);
        Assert.Equal(targetBundleId, dependency.Target.BundleId);
        Assert.Equal(200, dependency.Target.PathId);
        Assert.Equal("HeroTexture", dependency.Target.DisplayName);
        Assert.Equal("Texture2D", dependency.Target.AssetTypeText);
    }

    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(
        BundleId bundleId,
        string displayName)
    {
        return
        [
            new BundleTreeNodeDto(
                BundleId: new BundleId("group-root"),
                DisplayName: "Root",
                LogicalPath: "Root",
                SourcePath: string.Empty,
                IsVirtualFolder: true,
                AssetCount: 0,
                Children:
                [
                    new BundleTreeNodeDto(
                        BundleId: bundleId,
                        DisplayName: displayName,
                        LogicalPath: displayName,
                        SourcePath: $@"C:\Bundles\{displayName}",
                        IsVirtualFolder: false,
                        AssetCount: 1,
                        Children: Array.Empty<BundleTreeNodeDto>())
                ])
        ];
    }
}
