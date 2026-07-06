using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewCoordinatorTextTests
{
    [Fact]
    public async Task LoadMorePreviewTextLinesAsync_ShouldAppendLinesThroughInspectionGateway()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-text");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewLoadMoreText");
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
        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            workspaceRequest.SourcePath,
            workspaceRequest.WorkspaceDataRootPath);
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain",
            32,
            workspaceKey,
            PreviewCacheKey: "text-cache");
        var lineQueries = new List<LoadPreviewTextLinesQuery>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
                Task.FromResult(Result<AssetPreviewDto>.Success(new AssetPreviewDto(
                    WorkspaceId: query.WorkspaceId,
                    BundleId: query.BundleId,
                    AssetId: query.AssetId,
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain",
                    TextContent: "line-1\nline-2",
                    BinaryContent: null,
                    PreviewPayloadFilePath: "hero.txt",
                    PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
                    PreviewMessage: null,
                    PreferredDisplaySizeInBytes: null,
                    IsFromCache: false,
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    Diagnostic: new AssetPreviewDiagnosticDto(
                        AssetPreviewCacheStatusKind.Missing,
                        AssetPreviewFallbackReasonKind.None))
                {
                    FullContentReference = reference,
                    TextDisplay = new AssetPreviewTextDisplayDto(
                        DisplayCharacterCount: 13,
                        FullCharacterCount: 32,
                        DisplayLineCount: 2,
                        EstimatedFullLineCount: 4,
                        IsTruncated: true,
                        MaxDisplayCharacters: AssetPreviewTextDisplayPolicy.MaxDisplayCharacters,
                        MaxDisplayLineCount: AssetPreviewTextDisplayPolicy.MaxDisplayLineCount)
                })),
            LoadPreviewTextLinesHandler = query =>
            {
                lineQueries.Add(query);
                return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(
                    new AssetPreviewTextLineRangeDto(
                        reference,
                        ["line-3", "line-4"],
                        query.StartLineIndex,
                        2,
                        4,
                        HasMore: false)));
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
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(assetTab, details, static _ => "failed");
        await coordinator.LoadMorePreviewTextLinesAsync(assetTab);

        Assert.Single(lineQueries);
        Assert.Equal(2, lineQueries[0].StartLineIndex);
        Assert.Equal(
            ["line-1", "line-2", "line-3", "line-4"],
            assetTab.Inspector.Preview.PreviewTextLines.Select(static line => line.Text));
        Assert.False(assetTab.Inspector.Preview.CanLoadMorePreviewTextLines);
    }
}
