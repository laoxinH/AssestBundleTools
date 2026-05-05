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

public sealed class BundleExplorerAssetPreviewCoordinatorMonoBehaviourTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyMonoBehaviourPreviewAndInformationCards_WhenMonoBehaviourDataIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-mono-behaviour");
        var scriptAssetId = new AssetId("asset-script");
        var materialAssetId = new AssetId("asset-material");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MonoBehaviourPreview");
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
        var previewResult = CreateMonoBehaviourPreviewResult(
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
            Name = "fallback_hero_logic",
            AssetPath = "Scenes/HeroRoot.prefab/HeroLogic",
            ContainerPath = "Scenes",
            AssetType = AssetType.Unknown,
            AssetTypeDisplayName = "MonoBehaviour",
            Dependencies =
            [
                new AssetDependencyDto(
                    scriptAssetId,
                    bundleId,
                    "HeroController",
                    AssetType.Unknown,
                    PathId: 400,
                    AssetTypeDisplayName: "MonoScript"),
                new AssetDependencyDto(
                    materialAssetId,
                    bundleId,
                    "HeroMaterial",
                    AssetType.Material,
                    PathId: 200,
                    AssetTypeDisplayName: "Material")
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
        Assert.True(assetTab.Inspector.Preview.MonoBehaviour.HasMonoBehaviour);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.MonoBehaviour.Visibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewMonoBehaviourVisibility);
        Assert.Equal("HeroLogic", assetTab.Inspector.Preview.MonoBehaviour.AssetNameText);
        Assert.Equal(
            ResourceKeys.InspectorMonoBehaviourPreviewScriptSummaryFormat + ":HeroController|Assembly-CSharp.dll",
            assetTab.Inspector.Preview.MonoBehaviour.ScriptText);
        Assert.Equal(
            ResourceKeys.InspectorMonoBehaviourPreviewStateFormat + ":InspectorPanelView.Boolean.True|2|2",
            assetTab.Inspector.Preview.MonoBehaviour.StateText);
        Assert.Equal(
            ResourceKeys.InspectorMonoBehaviourPreviewFieldSummaryFormat + ":2|1",
            assetTab.Inspector.Preview.MonoBehaviour.FieldSummaryText);
        Assert.Equal(
            ResourceKeys.InspectorMonoBehaviourPreviewReferenceSummaryFormat + ":2|2",
            assetTab.Inspector.Preview.MonoBehaviour.ReferenceSummaryText);
        Assert.Contains(
            assetTab.Inspector.Preview.MonoBehaviour.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardScriptTitle &&
                           card.ValueText == "HeroController" &&
                           card.DetailText == "Assembly-CSharp.dll");
        Assert.Contains(
            assetTab.Inspector.Preview.MonoBehaviour.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardFieldsTitle &&
                           card.ValueText == "2" &&
                           card.DetailText == ResourceKeys.InspectorMonoBehaviourPreviewSummaryFieldDetailFormat + ":8|1|2");
        Assert.Contains(
            assetTab.Inspector.Preview.MonoBehaviour.SummaryCards,
            static card => card.TitleText == ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardReferencesTitle &&
                           card.ValueText == ResourceKeys.InspectorMonoBehaviourPreviewResolvedReferenceCountFormat + ":2|2" &&
                           card.DetailText.Contains(
                               ResourceKeys.InspectorMonoBehaviourPreviewReferenceTypeCountFormat + ":Material|1",
                               StringComparison.Ordinal));

        var objectCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-object");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewObjectCardTitle, objectCard.Title);
        Assert.Contains(
            objectCard.Rows,
            static row => row.Label == ResourceKeys.InspectorMonoBehaviourPreviewNameLabel &&
                          row.Value == "HeroLogic");

        var fieldGroupsCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-field-groups");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewFieldGroupsCardTitle, fieldGroupsCard.Title);
        Assert.Contains(
            fieldGroupsCard.Rows,
            static row => row.Label == "m_Waypoints" &&
                          row.Value == ResourceKeys.InspectorMonoBehaviourPreviewFieldGroupSummaryFormat + ":3|0|1|0");

        var containersCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-containers");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewContainersCardTitle, containersCard.Title);
        Assert.Contains(
            containersCard.Rows,
            static row => row.Label == "m_Waypoints" &&
                          row.Value == ResourceKeys.InspectorMonoBehaviourPreviewContainerSummaryFormat + ":Array|2|1");

        var scriptCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-script");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewScriptCardTitle, scriptCard.Title);
        var scriptPathRow = Assert.Single(
            scriptCard.Rows,
            static row => row.Label == ResourceKeys.InspectorMonoBehaviourPreviewScriptPathIdLabel);
        Assert.NotNull(scriptPathRow.Target);
        Assert.Equal(scriptAssetId, scriptPathRow.Target.AssetId);

        var fieldsCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-fields");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewFieldsCardTitle, fieldsCard.Title);
        Assert.Contains(
            fieldsCard.Rows,
            static row => row.Label == "m_Health" &&
                          row.Value == ResourceKeys.InspectorMonoBehaviourPreviewFieldValueFormat + ":int|100");
        Assert.Contains(
            fieldsCard.Rows,
            static row => row.Label == "m_Waypoints" &&
                          row.Value == ResourceKeys.InspectorMonoBehaviourPreviewFieldArrayCountFormat + ":Array|2");

        var referencesCard = Assert.Single(
            assetTab.Inspector.InformationCards,
            static card => card.Key == "mono-behaviour-references");
        Assert.Equal(ResourceKeys.InspectorMonoBehaviourPreviewReferencesCardTitle, referencesCard.Title);
        var materialReferenceRow = Assert.Single(
            referencesCard.Rows,
            static row => row.Label == "m_Material");
        Assert.NotNull(materialReferenceRow.Target);
        Assert.Equal(materialAssetId, materialReferenceRow.Target.AssetId);
        Assert.Contains(
            ResourceKeys.InspectorMonoBehaviourPreviewReferenceResolvedFormat + ":",
            materialReferenceRow.Value,
            StringComparison.Ordinal);
        Assert.Equal(@"C:\PreviewCache\mono-behaviour.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
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
