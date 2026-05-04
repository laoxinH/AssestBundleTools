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

public sealed class BundleExplorerAssetPreviewCoordinatorMaterialTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyMaterialPreview_WhenMaterialDataIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-material");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MaterialPreview");
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
        var previewResult = CreateMaterialPreviewResult(
            new GetAssetPreviewQuery(
                workspaceId,
                "workspace-key",
                workspaceRequest.WorkspaceDataRootPath,
                bundleId,
                assetId));
        Assert.NotNull(previewResult.Value);
        Assert.NotNull(previewResult.Value.Material);
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
            Name = "paopao_01",
            AssetPath = "Materials/paopao_01.mat",
            ContainerPath = "Materials",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString(),
            Dependencies = Array.Empty<AssetDependencyDto>()
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        Assert.Equal(1, previewResult.Value.Material?.TextureProperties.Count);
        Assert.True(assetTab.Inspector.HasSelection);
        Assert.True(assetTab.Inspector.Preview.Material.HasMaterial);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.Visibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewMaterialVisibility);
        Assert.Equal("paopao_01", assetTab.Inspector.Preview.Material.AssetNameText);
        Assert.Equal("Particles/Additive", assetTab.Inspector.Preview.Material.ShaderNameText);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewShaderReferenceFormat + ":materials.assets|100",
            assetTab.Inspector.Preview.Material.ShaderReferenceText);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewShaderInternalUnresolvedText,
            assetTab.Inspector.Preview.Material.ShaderResolutionText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.ShaderResolutionVisibility);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewPropertyCountFormat + ":1|1|2",
            assetTab.Inspector.Preview.Material.PropertyCountText);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewSemanticHintsTitle,
            assetTab.Inspector.Preview.Material.SemanticSectionTitle);
        Assert.Equal(
            [
                ResourceKeys.InspectorMaterialPreviewSemanticMainTextureDetectedText,
                ResourceKeys.InspectorMaterialPreviewSemanticMetallicSmoothnessCandidateText
            ],
            assetTab.Inspector.Preview.Material.SemanticBadges.Select(static badge => badge.Text));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.SemanticHintsVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.TextureRowsVisibility);
        Assert.Single(assetTab.Inspector.Preview.Material.TextureRows);
        var textureRow = assetTab.Inspector.Preview.Material.TextureRows[0];
        Assert.Equal("_MainTex", textureRow.PropertyName);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewTextureExternalFormat + ":archive:/CAB-textures/CAB-textures|200",
            textureRow.TargetText);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewTextureExternalUnresolvedText,
            textureRow.ResolutionText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, textureRow.ResolutionVisibility);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewTextureTransformFormat + ":1|1|0|0",
            textureRow.TransformText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, textureRow.TransformVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.ColorRowsVisibility);
        Assert.Single(assetTab.Inspector.Preview.Material.ColorRows);
        Assert.Equal("_Color", assetTab.Inspector.Preview.Material.ColorRows[0].PropertyName);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewColorValueFormat + ":1|0|0|1",
            assetTab.Inspector.Preview.Material.ColorRows[0].ValueText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Material.NumberRowsVisibility);
        Assert.Equal(2, assetTab.Inspector.Preview.Material.NumberRows.Count);
        Assert.Equal("_Glossiness", assetTab.Inspector.Preview.Material.NumberRows[0].PropertyName);
        Assert.Equal("1", assetTab.Inspector.Preview.Material.NumberRows[0].ValueText);
        Assert.Equal("_Cull", assetTab.Inspector.Preview.Material.NumberRows[1].PropertyName);
        Assert.Equal("2", assetTab.Inspector.Preview.Material.NumberRows[1].ValueText);
        Assert.Equal(@"C:\PreviewCache\material.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
        Assert.Empty(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldResolveMaterialTextureRow_AndRequestThumbnailPreview()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-material");
        var textureAssetId = new AssetId("asset-texture");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MaterialTextureThumbnail");
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
        var queriedAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                queriedAssetIds.Add(query.AssetId);
                return Task.FromResult(query.AssetId.Equals(textureAssetId)
                    ? CreateTexturePreviewResult(query, [1, 2, 3])
                    : CreateMaterialPreviewResult(query));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner());
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    textureAssetId,
                    bundleId,
                    "HeroTexture",
                    AssetType.Texture2D,
                    PathId: 200,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal([assetId, textureAssetId], queriedAssetIds);
        var textureRow = Assert.Single(assetTab.Inspector.Preview.Material.TextureRows);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewTextureResolvedFormat + ":HeroTexture|Texture2D",
            textureRow.ResolutionText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, textureRow.ResolutionVisibility);
        Assert.Empty(appLogService.WarningMessages);
    }

    private static void ApplyInspectorSelection(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details)
    {
        assetTab.Inspector.Apply(
            assetName: details.Name,
            bundleDisplayName: details.BundleId.Value,
            assetTypeText: details.AssetType.ToString(),
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
