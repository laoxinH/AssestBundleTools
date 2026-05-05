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

public sealed class BundleExplorerAssetPreviewCoordinatorAnimatorControllerTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyAnimatorControllerPreviewAndInformationCards_WhenAnimatorControllerDataIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-animator-controller");
        var idleClipAssetId = new AssetId("asset-idle-clip");
        var attackClipAssetId = new AssetId("asset-attack-clip");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("AnimatorControllerPreview");
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
        var previewResult = CreateAnimatorControllerPreviewResult(
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
            Name = "fallback_animator",
            AssetPath = "Animations/HeroAnimator.controller",
            ContainerPath = "Animations",
            AssetType = AssetType.Unknown,
            AssetTypeDisplayName = "AnimatorController",
            Dependencies =
            [
                new AssetDependencyDto(
                    idleClipAssetId,
                    bundleId,
                    "HeroIdle",
                    AssetType.Unknown,
                    PathId: 700,
                    AssetTypeDisplayName: "AnimationClip"),
                new AssetDependencyDto(
                    attackClipAssetId,
                    bundleId,
                    "HeroAttack",
                    AssetType.Unknown,
                    PathId: 701,
                    AssetTypeDisplayName: "AnimationClip")
            ]
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        Assert.True(assetTab.Inspector.HasSelection);
        Assert.True(assetTab.Inspector.Preview.AnimatorController.HasAnimatorController);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.AnimatorController.Visibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewAnimatorControllerVisibility);
        Assert.Equal("HeroAnimator", assetTab.Inspector.Preview.AnimatorController.AssetNameText);
        Assert.Equal(
            ResourceKeys.InspectorAnimatorControllerPreviewStructureFormat + ":0|2",
            assetTab.Inspector.Preview.AnimatorController.StructureText);
        Assert.Equal(
            ResourceKeys.InspectorAnimatorControllerPreviewStateSummaryFormat + ":1|5|8",
            assetTab.Inspector.Preview.AnimatorController.StateSummaryText);
        Assert.Contains(
            ResourceKeys.InspectorAnimatorControllerPreviewReferenceSummaryFormat + ":2|3|",
            assetTab.Inspector.Preview.AnimatorController.ReferenceSummaryText,
            StringComparison.Ordinal);
        Assert.Contains(
            assetTab.Inspector.Preview.AnimatorController.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardStatesTitle &&
                           card.ValueText == "5" &&
                           card.DetailText == ResourceKeys.InspectorAnimatorControllerPreviewSummaryStateDetailFormat + ":8|2");
        Assert.Contains(
            assetTab.Inspector.Preview.AnimatorController.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardReferencesTitle &&
                           card.ValueText == ResourceKeys.InspectorAnimatorControllerPreviewResolvedReferenceCountFormat + ":2|3" &&
                           card.DetailText.Contains(
                               ResourceKeys.InspectorAnimatorControllerPreviewReferenceTypeCountFormat + ":AnimationClip|2",
                               StringComparison.Ordinal));

        var objectCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "animator-controller-object");
        Assert.Equal(ResourceKeys.InspectorAnimatorControllerPreviewObjectCardTitle, objectCard.Title);
        Assert.Contains(
            objectCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimatorControllerPreviewStateCountLabel &&
                          row.Value == "5");

        var parametersCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "animator-controller-parameters");
        Assert.Equal(ResourceKeys.InspectorAnimatorControllerPreviewParametersCardTitle, parametersCard.Title);
        Assert.Contains(
            parametersCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimatorControllerPreviewParameterLabelFormat + ":1" &&
                          row.Value == ResourceKeys.InspectorAnimatorControllerPreviewParameterValueFormat + ":Speed|" +
                              ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeFloat + "|1");

        var containersCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "animator-controller-containers");
        Assert.Equal(ResourceKeys.InspectorAnimatorControllerPreviewContainersCardTitle, containersCard.Title);
        Assert.Contains(
            containersCard.Rows,
            static row => row.Label.EndsWith("m_StateConstantArray", StringComparison.Ordinal) &&
                          row.Value == ResourceKeys.InspectorAnimatorControllerPreviewContainerSummaryFormat + ":" +
                              ResourceKeys.InspectorAnimatorControllerPreviewContainerKindState + "|3|6");

        var referencesCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "animator-controller-references");
        Assert.Equal(ResourceKeys.InspectorAnimatorControllerPreviewReferencesCardTitle, referencesCard.Title);
        var idleClipRow = Assert.Single(
            referencesCard.Rows,
            static row => row.Label == "m_AnimationClips.Array.data[0]");
        Assert.NotNull(idleClipRow.Target);
        Assert.Equal(idleClipAssetId, idleClipRow.Target.AssetId);
        Assert.Contains(
            ResourceKeys.InspectorAnimatorControllerPreviewReferenceResolvedFormat + ":",
            idleClipRow.Value,
            StringComparison.Ordinal);
        Assert.Equal(@"C:\PreviewCache\animator-controller.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
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
