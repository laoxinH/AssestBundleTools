using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewCoordinatorAnimationClipTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyAnimationClipPreview_WhenAnimationClipDataIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-animation");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("AnimationClipPreview");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var backgroundTaskRunner = new TestBackgroundTaskRunner();
        var previewResult = CreateAnimationClipPreviewResult(
            new GetAssetPreviewQuery(
                workspaceId,
                "workspace-key",
                workspaceRequest.WorkspaceDataRootPath,
                bundleId,
                assetId));
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = _ => Task.FromResult(previewResult)
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            backgroundTaskRunner);
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId) with
        {
            Name = "idle_loop",
            AssetPath = "Animations/idle_loop.anim",
            ContainerPath = "Animations",
            AssetType = AssetType.Unknown,
            AssetTypeDisplayName = "AnimationClip",
            Dependencies = Array.Empty<AssetDependencyDto>()
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        Assert.True(assetTab.Inspector.HasSelection);
        Assert.True(assetTab.Inspector.Preview.AnimationClip.HasAnimationClip);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.AnimationClip.Visibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewAnimationClipVisibility);
        Assert.Equal("idle_loop", assetTab.Inspector.Preview.AnimationClip.AssetNameText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewTimingFormat + ":1.5|30|45|0|1.5",
            assetTab.Inspector.Preview.AnimationClip.TimingText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewCurveCountFormat + ":8|2|3|1|0",
            assetTab.Inspector.Preview.AnimationClip.CurveCountText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewBindingSummaryFormat + ":3|16",
            assetTab.Inspector.Preview.AnimationClip.BindingSummaryText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewLoopFormat + ":InspectorPanelView.Boolean.True|InspectorPanelView.Boolean.False|InspectorPanelView.Boolean.True|InspectorPanelView.Boolean.False|InspectorPanelView.Boolean.True",
            assetTab.Inspector.Preview.AnimationClip.LoopText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewEventSummaryFormat + ":1",
            assetTab.Inspector.Preview.AnimationClip.EventSummaryText);
        Assert.Single(assetTab.Inspector.Preview.AnimationClip.EventRows);
        var eventRow = assetTab.Inspector.Preview.AnimationClip.EventRows[0];
        Assert.Equal(ResourceKeys.InspectorAnimationClipPreviewEventTimeFormat + ":0.25", eventRow.TimeText);
        Assert.Equal("OnStep", eventRow.FunctionNameText);
        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewEventParametersFormat + ":left|0.5|2",
            eventRow.ParameterText);
        var curveBindingCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "animation-clip-curve-bindings");
        Assert.Equal(ResourceKeys.InspectorAnimationClipPreviewCurveBindingCardTitle, curveBindingCard.Title);
        Assert.Contains(
            curveBindingCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimationClipPreviewCurveBindingCountLabel &&
                          row.Value == "3");
        Assert.Contains(
            curveBindingCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimationClipPreviewCurveBindingLabelFormat + ":" +
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindFloat + "|1" &&
                          row.Value.Contains("Root/Hips", StringComparison.Ordinal));
        Assert.Contains(
            curveBindingCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimationClipPreviewCurveBindingLabelFormat + ":" +
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindGeneric + "|3" &&
                          row.Value.Contains(
                              ResourceKeys.InspectorAnimationClipPreviewCurveBindingCurveKeyFormat + ":1|-",
                              StringComparison.Ordinal));
        Assert.Equal(@"C:\PreviewCache\animation-clip.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
        Assert.Empty(appLogService.WarningMessages);
    }

    private static void ApplyInspectorSelection(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details)
    {
        assetTab.Inspector.Apply(
            assetName: details.Name,
            bundleDisplayName: details.BundleId.Value,
            assetTypeText: details.AssetTypeDisplayName ?? details.AssetType.ToString(),
            containerPath: string.Empty,
            serializedFileName: string.Empty,
            sizeText: string.Empty,
            pathIdText: details.PathId.ToString(),
            extractCapabilityText: string.Empty,
            fileReplacementCapabilityText: string.Empty,
            workspaceReplacementCapabilityText: string.Empty,
            replacementSourceText: string.Empty,
            replacementStatusText: string.Empty,
            changeSummaryText: string.Empty,
            canExtract: true,
            canSelectFileReplacementSource: true,
            canSelectReplacementSource: true,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: Array.Empty<AssetDependencyViewModel>(),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>());
    }
}
