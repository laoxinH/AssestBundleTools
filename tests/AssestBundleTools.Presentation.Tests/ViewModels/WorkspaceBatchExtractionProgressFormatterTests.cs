using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceBatchExtractionProgressFormatterTests
{
    [Fact]
    public void Format_ShouldDescribePreparingProgress()
    {
        var formatter = new WorkspaceBatchExtractionProgressFormatter(new TestLocalizationService());

        var text = formatter.Format(new WorkspaceBatchExtractionProgressDto(
            WorkspaceBatchExtractionProgressKind.Preparing,
            CompletedItemCount: 0,
            TotalItemCount: 12,
            CandidateBundleCount: 3,
            SucceededItemCount: 0,
            FailedItemCount: 0));

        Assert.Equal(
            $"{ResourceKeys.WorkspaceBatchExtractionDialogProgressPreparingFormat}:12|3",
            text);
    }

    [Fact]
    public void Format_ShouldDescribeCurrentAssetAndMemoryThrottle()
    {
        var formatter = new WorkspaceBatchExtractionProgressFormatter(new TestLocalizationService());

        var text = formatter.Format(new WorkspaceBatchExtractionProgressDto(
            WorkspaceBatchExtractionProgressKind.Asset,
            CompletedItemCount: 2,
            TotalItemCount: 5,
            CandidateBundleCount: 2,
            SucceededItemCount: 2,
            FailedItemCount: 1,
            BundleDisplayName: "ui.ab",
            ItemDisplayName: "HeroTexture",
            AssetType: AssetType.Texture2D,
            AssetTypeDisplayName: "Texture2D",
            IsMemoryThrottled: true));

        Assert.Equal(
            $"{ResourceKeys.WorkspaceBatchExtractionDialogProgressAssetFormat}:ui.ab|HeroTexture|{ResourceKeys.WorkspaceBatchExtractionDialogCategoryTexture2D}|2|1|{ResourceKeys.WorkspaceBatchExtractionDialogProgressMemoryThrottledSuffix}",
            text);
    }

    [Fact]
    public void Format_ShouldPreserveUnknownAssetTypeDisplayName()
    {
        var formatter = new WorkspaceBatchExtractionProgressFormatter(new TestLocalizationService());

        var text = formatter.Format(new WorkspaceBatchExtractionProgressDto(
            WorkspaceBatchExtractionProgressKind.Asset,
            CompletedItemCount: 0,
            TotalItemCount: 1,
            CandidateBundleCount: 1,
            SucceededItemCount: 0,
            FailedItemCount: 0,
            BundleDisplayName: "dynamic.ab",
            ItemDisplayName: "HeroShader",
            AssetType: AssetType.Unknown,
            AssetTypeDisplayName: "Shader"));

        Assert.Equal(
            $"{ResourceKeys.WorkspaceBatchExtractionDialogProgressAssetFormat}:dynamic.ab|HeroShader|Shader|0|0|",
            text);
    }
}
