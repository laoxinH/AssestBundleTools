using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetDocumentTabViewModelTests
{
    [Fact]
    public void EditDraftReference_ShouldTrackDraftMarkerWithoutContent()
    {
        var assetTab = CreateAssetTab();
        var draftReference = new AssetDocumentEditDraftReference(
            DraftId: "draft-1",
            DraftMode: "text",
            SourceFingerprint: "source-v1",
            SourceDescription: "TextAsset edit");

        assetTab.SetEditDraftReference(draftReference);

        Assert.True(assetTab.HasEditDraft);
        Assert.Same(draftReference, assetTab.EditDraftReference);

        assetTab.ClearEditDraftReference();

        Assert.False(assetTab.HasEditDraft);
        Assert.Null(assetTab.EditDraftReference);
    }

    [Fact]
    public void RestoreEditDraftReference_ShouldApplyPersistedDraftMarker()
    {
        var assetTab = CreateAssetTab();

        assetTab.RestoreEditDraftReference(
            new AssetDocumentEditDraftReference("draft-2", "material", "source-v2"));

        Assert.True(assetTab.HasEditDraft);
        Assert.Equal("draft-2", assetTab.EditDraftReference?.DraftId);
        Assert.Equal("material", assetTab.EditDraftReference?.DraftMode);
        Assert.Equal("source-v2", assetTab.EditDraftReference?.SourceFingerprint);
    }

    private static AssetDocumentTabViewModel CreateAssetTab()
    {
        var localizationService = new TestLocalizationService();
        return new AssetDocumentTabViewModel(
            new BundleId("bundle-a"),
            new AssetId("asset-1"),
            "Asset 1",
            InspectorInformationTextSetFactory.Create(localizationService));
    }
}
