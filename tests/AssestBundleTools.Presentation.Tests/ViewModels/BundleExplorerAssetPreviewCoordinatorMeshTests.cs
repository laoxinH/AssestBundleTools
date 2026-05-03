using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewCoordinatorMeshTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldApplyMeshPreview_WhenMeshGeometryIsAvailable()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-mesh");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MeshPreview");
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
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query => Task.FromResult(CreateMeshPreviewResult(query))
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
            AssetType = AssetType.Mesh,
            AssetTypeDisplayName = AssetType.Mesh.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    new AssetId("mesh-material"),
                    bundleId,
                    "MeshMaterial",
                    AssetType.Material,
                    AssetTypeDisplayName: "Material")
            ]
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.PreviewMeshVisibility);
        Assert.NotNull(assetTab.Inspector.Preview.Mesh.Geometry);
        Assert.Equal(3, assetTab.Inspector.Preview.Mesh.Geometry!.Vertices.Count);
        Assert.Equal(3, assetTab.Inspector.Preview.Mesh.Geometry.Uv0?.Count ?? 0);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Mesh.DisplayModeSwitchVisibility);
        Assert.Equal(
            ResourceKeys.InspectorMeshPreviewBadgeFormat + ":3|3",
            assetTab.Inspector.Preview.Mesh.BadgeText);
        Assert.Equal(
            ResourceKeys.InspectorMeshPreviewTextureAssociationFormat + ":" +
            ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat + ":MeshMaterial|Material",
            assetTab.Inspector.Preview.Mesh.TextureAssociationText);
        Assert.Equal(@"C:\PreviewCache\mesh.json", assetTab.Inspector.Preview.PreviewPayloadFilePath);
        Assert.Empty(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldLoadTextureOverlayPreview_ForMeshTextureDependency()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var meshAssetId = new AssetId("asset-mesh");
        var textureAssetId = new AssetId("asset-texture");
        var overlayContent = new byte[] { 9, 8, 7, 6 };
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MeshTextureOverlay");
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
                    ? CreateTexturePreviewResult(query, overlayContent)
                    : CreateMeshPreviewResult(query));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner());
        var assetTab = CreateAssetTab(bundleId, meshAssetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, meshAssetId) with
        {
            AssetType = AssetType.Mesh,
            AssetTypeDisplayName = AssetType.Mesh.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    textureAssetId,
                    bundleId,
                    "HeroTexture",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal([meshAssetId, textureAssetId], queriedAssetIds);
        Assert.Equal(overlayContent, assetTab.Inspector.Preview.Mesh.TextureOverlayContent);
        Assert.True(assetTab.Inspector.Preview.Mesh.HasTextureOverlay);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetTab.Inspector.Preview.Mesh.TextureOverlaySwitchVisibility);
        Assert.Empty(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldPreferTextureAssociations_ForMeshTextureOverlay()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var meshAssetId = new AssetId("asset-mesh");
        var directTextureAssetId = new AssetId("asset-direct-texture");
        var associatedTextureAssetId = new AssetId("asset-associated-texture");
        var overlayContent = new byte[] { 1, 3, 5, 7 };
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("MeshTextureAssociationOverlay");
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
                return Task.FromResult(query.AssetId.Equals(associatedTextureAssetId)
                    ? CreateTexturePreviewResult(query, overlayContent)
                    : CreateMeshPreviewResult(query));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner());
        var assetTab = CreateAssetTab(bundleId, meshAssetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, meshAssetId) with
        {
            AssetType = AssetType.Mesh,
            AssetTypeDisplayName = AssetType.Mesh.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    directTextureAssetId,
                    bundleId,
                    "DirectTexture",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ],
            TextureAssociations =
            [
                new AssetDependencyDto(
                    associatedTextureAssetId,
                    bundleId,
                    "AssociatedTexture",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };
        assetTab.ApplyDetails(details);
        ApplyInspectorSelection(assetTab, details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal([meshAssetId, associatedTextureAssetId], queriedAssetIds);
        Assert.Equal(overlayContent, assetTab.Inspector.Preview.Mesh.TextureOverlayContent);
        Assert.DoesNotContain(directTextureAssetId, queriedAssetIds);
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
