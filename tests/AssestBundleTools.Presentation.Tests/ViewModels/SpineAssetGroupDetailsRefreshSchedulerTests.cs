using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAssetGroupDetailsRefreshSchedulerTests
{
    [Fact]
    public async Task RequestAsync_ShouldCoalesceQueuedRequests_ToLatestInputSignature()
    {
        var tab = CreateTab();
        var observedSignatures = new List<string>();
        using var scheduler = new SpineAssetGroupDetailsRefreshScheduler(
            request =>
            {
                observedSignatures.Add(request.InputSignature);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(50));

        var firstRequest = scheduler.RequestAsync(tab);
        await Task.Delay(10);
        tab.StartSpineEditMode(new SpineAssetGroupEditDraftViewModel(tab.Item, new TestLocalizationService()));
        Assert.True(tab.SpineEditDraft!.ApplyPartSource(tab.SpineEditDraft.Parts[0], @"C:\temp\hero-final.skel"));
        tab.NotifySpineEditStateChanged();
        var secondRequest = scheduler.RequestAsync(tab);

        await Task.WhenAll(firstRequest, secondRequest);

        var signature = Assert.Single(observedSignatures);
        Assert.Contains(@"C:\temp\hero-final.skel", signature);
    }

    [Fact]
    public async Task RequestAsync_ShouldNotRestartDebounce_WhenInputSignatureIsUnchanged()
    {
        var tab = CreateTab();
        var observedSignatures = new List<string>();
        using var scheduler = new SpineAssetGroupDetailsRefreshScheduler(
            request =>
            {
                observedSignatures.Add(request.InputSignature);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(50));

        var firstRequest = scheduler.RequestAsync(tab);
        await Task.Delay(10);
        var secondRequest = scheduler.RequestAsync(tab);

        await Task.WhenAll(firstRequest, secondRequest);

        Assert.Single(observedSignatures);
    }

    [Fact]
    public async Task RequestAsync_ShouldIgnoreStaleRunningRequest_WhenNewerRequestArrives()
    {
        var tab = CreateTab();
        var firstLoadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLoadRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var appliedSignatures = new List<string>();
        var callCount = 0;
        using var scheduler = new SpineAssetGroupDetailsRefreshScheduler(
            async request =>
            {
                callCount++;
                if (callCount == 1)
                {
                    firstLoadStarted.SetResult();
                    await firstLoadRelease.Task;
                }

                if (request.IsCurrent)
                {
                    appliedSignatures.Add(request.InputSignature);
                }
            },
            TimeSpan.Zero);

        var firstRequest = scheduler.RequestAsync(tab);
        await firstLoadStarted.Task;

        tab.StartSpineEditMode(new SpineAssetGroupEditDraftViewModel(tab.Item, new TestLocalizationService()));
        Assert.True(tab.SpineEditDraft!.ApplyPartSource(tab.SpineEditDraft.Parts[0], @"C:\temp\hero-new.skel"));
        tab.NotifySpineEditStateChanged();
        var secondRequest = scheduler.RequestAsync(tab);
        firstLoadRelease.SetResult();

        await Task.WhenAll(firstRequest, secondRequest);

        Assert.Equal(2, callCount);
        var signature = Assert.Single(appliedSignatures);
        Assert.Contains(@"C:\temp\hero-new.skel", signature);
    }

    private static SpineAssetGroupDocumentTabViewModel CreateTab()
    {
        var bundleId = new BundleId("bundle-ui");
        var group = new SpineAssetGroupDto(
            "hero",
            bundleId,
            "Hero",
            "characters/hero",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    new AssetId("hero-skeleton"),
                    "ui.ab",
                    "hero.skel",
                    "Assets/hero.skel",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            AtlasAssets: [],
            TexturePageAssets: [],
            Diagnostics: []);
        return new SpineAssetGroupDocumentTabViewModel(
            group,
            InspectorInformationTextSetFactory.Create(new TestLocalizationService()));
    }
}
