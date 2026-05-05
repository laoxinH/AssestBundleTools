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

public sealed class BundleExplorerAssetPreviewCoordinatorGameObjectTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyGameObjectPreviewAndInformationCards_WhenGameObjectDataIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-game-object");
        var transformAssetId = new AssetId("component-transform");
        var meshFilterAssetId = new AssetId("component-mesh-filter");
        var rendererAssetId = new AssetId("component-renderer");
        var behaviourAssetId = new AssetId("component-behaviour");
        var materialAssetId = new AssetId("asset-material");
        var meshAssetId = new AssetId("asset-mesh");
        var scriptAssetId = new AssetId("asset-script");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("GameObjectPreview");
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
        var previewResult = CreateGameObjectPreviewResult(
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
            Name = "fallback_hero_root",
            AssetPath = "Scenes/HeroRoot.prefab",
            ContainerPath = "Scenes",
            AssetType = AssetType.Unknown,
            AssetTypeDisplayName = "GameObject",
            Dependencies =
            [
                new AssetDependencyDto(
                    transformAssetId,
                    bundleId,
                    "HeroTransform",
                    AssetType.Unknown,
                    PathId: 100,
                    AssetTypeDisplayName: "Transform"),
                new AssetDependencyDto(
                    rendererAssetId,
                    bundleId,
                    "HeroRenderer",
                    AssetType.Unknown,
                    PathId: 102,
                    AssetTypeDisplayName: "MeshRenderer"),
                new AssetDependencyDto(
                    meshFilterAssetId,
                    bundleId,
                    "HeroMeshFilter",
                    AssetType.Unknown,
                    PathId: 101,
                    AssetTypeDisplayName: "MeshFilter"),
                new AssetDependencyDto(
                    behaviourAssetId,
                    bundleId,
                    "HeroLogic",
                    AssetType.Unknown,
                    PathId: 103,
                    AssetTypeDisplayName: "MonoBehaviour"),
                new AssetDependencyDto(
                    materialAssetId,
                    bundleId,
                    "HeroMaterial",
                    AssetType.Material,
                    PathId: 200,
                    AssetTypeDisplayName: "Material"),
                new AssetDependencyDto(
                    meshAssetId,
                    bundleId,
                    "HeroMesh",
                    AssetType.Mesh,
                    PathId: 300,
                    AssetTypeDisplayName: "Mesh"),
                new AssetDependencyDto(
                    scriptAssetId,
                    bundleId,
                    "HeroController",
                    AssetType.Unknown,
                    PathId: 400,
                    AssetTypeDisplayName: "MonoScript")
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
        Assert.True(assetTab.Inspector.Preview.GameObject.HasGameObject);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.GameObject.Visibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewGameObjectVisibility);
        Assert.Equal("HeroRoot", assetTab.Inspector.Preview.GameObject.AssetNameText);
        Assert.Equal(
            ResourceKeys.InspectorGameObjectPreviewStateFormat + ":InspectorPanelView.Boolean.True|5|Player",
            assetTab.Inspector.Preview.GameObject.StateText);
        Assert.Equal(
            ResourceKeys.InspectorGameObjectPreviewComponentSummaryFormat + ":4|Transform, MeshFilter, MeshRenderer, HeroController",
            assetTab.Inspector.Preview.GameObject.ComponentSummaryText);
        Assert.Equal(
            ResourceKeys.InspectorGameObjectPreviewTransformSummaryFormat + ":" +
            ResourceKeys.InspectorGameObjectPreviewVector3Format + ":1|2|3|" +
            ResourceKeys.InspectorGameObjectPreviewVector3Format + ":1|1|1",
            assetTab.Inspector.Preview.GameObject.TransformSummaryText);
        Assert.Equal(
            ResourceKeys.InspectorGameObjectPreviewRootHierarchyFormat + ":2",
            assetTab.Inspector.Preview.GameObject.HierarchySummaryText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.GameObject.SummaryCardsVisibility);
        Assert.Contains(
            assetTab.Inspector.Preview.GameObject.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorGameObjectPreviewSummaryCardComponentsTitle &&
                           card.ValueText == "4" &&
                           card.DetailText == "Transform, MeshFilter, MeshRenderer, HeroController");
        Assert.Contains(
            assetTab.Inspector.Preview.GameObject.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorGameObjectPreviewSummaryCardReferencesTitle &&
                           card.ValueText == ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewSummaryResolvedReferenceLabel + "|3" &&
                           card.DetailText.Contains(
                               ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh + "|1",
                               StringComparison.Ordinal));
        Assert.Contains(
            assetTab.Inspector.Preview.GameObject.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorGameObjectPreviewSummaryCardRendererTitle &&
                           card.ValueText == ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewReferenceRoleMaterial + "|1" &&
                           card.DetailText.Contains(
                               ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewComponentSummaryReceiveShadowsLabel + "|" +
                               ResourceKeys.InspectorBooleanTrue,
                               StringComparison.Ordinal));
        Assert.Contains(
            assetTab.Inspector.Preview.GameObject.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorGameObjectPreviewSummaryCardMeshTitle &&
                           card.ValueText == ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh + "|HeroMesh");
        Assert.Contains(
            assetTab.Inspector.Preview.GameObject.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorGameObjectPreviewSummaryCardScriptTitle &&
                           card.ValueText == ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat + ":" +
                               ResourceKeys.InspectorGameObjectPreviewReferenceRoleScript + "|HeroController");

        var objectCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "game-object-object");
        Assert.Equal(ResourceKeys.InspectorGameObjectPreviewObjectCardTitle, objectCard.Title);
        Assert.Contains(
            objectCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewNameLabel &&
                          row.Value == "HeroRoot");
        Assert.Contains(
            objectCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentCountLabel &&
                          row.Value == "4");

        var transformCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "game-object-transform");
        Assert.Equal(ResourceKeys.InspectorGameObjectPreviewTransformCardTitle, transformCard.Title);
        Assert.Contains(
            transformCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewChildCountLabel &&
                          row.Value == "2");
        var transformPathRow = Assert.Single(
            transformCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewTransformPathIdLabel);
        Assert.NotNull(transformPathRow.Target);
        Assert.Equal(transformAssetId, transformPathRow.Target.AssetId);
        Assert.Contains(
            transformCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewLocalPositionLabel &&
                          row.Value == ResourceKeys.InspectorGameObjectPreviewVector3Format + ":1|2|3");

        var componentsCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "game-object-components");
        Assert.Equal(ResourceKeys.InspectorGameObjectPreviewComponentsCardTitle, componentsCard.Title);
        Assert.Equal(4, componentsCard.Rows.Count);
        Assert.Contains(
            componentsCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentLabelFormat + ":4" &&
                          row.Value.Contains("HeroController", StringComparison.Ordinal) &&
                          row.Value.Contains(
                              ResourceKeys.InspectorGameObjectPreviewComponentScriptFormat + ":HeroController",
                              StringComparison.Ordinal));
        var behaviourComponentRow = Assert.Single(
            componentsCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentLabelFormat + ":4");
        Assert.NotNull(behaviourComponentRow.Target);
        Assert.Equal(behaviourAssetId, behaviourComponentRow.Target.AssetId);

        var referencesCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "game-object-component-references");
        Assert.Equal(ResourceKeys.InspectorGameObjectPreviewComponentReferencesCardTitle, referencesCard.Title);
        Assert.Equal(3, referencesCard.Rows.Count);
        var meshReferenceRow = Assert.Single(
            referencesCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentReferenceLabelFormat + ":" +
                "2|" + ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh);
        Assert.NotNull(meshReferenceRow.Target);
        Assert.Equal(meshAssetId, meshReferenceRow.Target.AssetId);
        var materialReferenceRow = Assert.Single(
            referencesCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentReferenceLabelFormat + ":" +
                "3|" + ResourceKeys.InspectorGameObjectPreviewReferenceRoleMaterial);
        Assert.NotNull(materialReferenceRow.Target);
        Assert.Equal(materialAssetId, materialReferenceRow.Target.AssetId);
        Assert.Contains(
            ResourceKeys.InspectorGameObjectPreviewComponentReferenceResolvedFormat + ":",
            materialReferenceRow.Value,
            StringComparison.Ordinal);
        var scriptReferenceRow = Assert.Single(
            referencesCard.Rows,
            static row => row.Label == ResourceKeys.InspectorGameObjectPreviewComponentReferenceLabelFormat + ":" +
                "4|" + ResourceKeys.InspectorGameObjectPreviewReferenceRoleScript);
        Assert.NotNull(scriptReferenceRow.Target);
        Assert.Equal(scriptAssetId, scriptReferenceRow.Target.AssetId);
        Assert.Equal(@"C:\PreviewCache\game-object.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
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
