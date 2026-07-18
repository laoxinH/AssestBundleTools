using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extensions;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerOnlineComponentPreviewTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldInstallAndReloadCurrentForegroundPreview()
    {
        var previewQueryCount = 0;
        var context = CreateContext(query =>
        {
            previewQueryCount++;
            return Task.FromResult(previewQueryCount == 1
                ? CreateMissingComponentPreviewResult(query)
                : CreateSummaryPreviewResult(query));
        });
        var promptCoordinator = new TestOnlineComponentInstallPromptCoordinator(installResult: true);
        var coordinator = context.CreateCoordinator(promptCoordinator);

        await coordinator.LoadInspectorPreviewAsync(
            context.AssetTab,
            context.Details,
            static _ => "failed");

        Assert.Equal(2, previewQueryCount);
        Assert.Equal(1, promptCoordinator.CallCount);
        Assert.Equal(OnlineComponentIds.VgmstreamCli, promptCoordinator.LastComponentId);
        Assert.Equal("summary", context.AssetTab.Inspector.Preview.PreviewText);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldNotPromptDuringBackgroundPrewarm()
    {
        var previewQueryCount = 0;
        var context = CreateContext(query =>
        {
            previewQueryCount++;
            return Task.FromResult(CreateMissingComponentPreviewResult(query));
        });
        var promptCoordinator = new TestOnlineComponentInstallPromptCoordinator(installResult: true);
        var coordinator = context.CreateCoordinator(promptCoordinator);

        await coordinator.LoadInspectorPreviewAsync(
            context.AssetTab,
            context.Details,
            static _ => "failed",
            priority: AssetPreviewLoadPriority.BackgroundPrewarm);

        Assert.Equal(1, previewQueryCount);
        Assert.Equal(0, promptCoordinator.CallCount);
    }

    private static PreviewTestContext CreateContext(
        Func<GetAssetPreviewQuery, Task<Result<AssetPreviewDto>>> getAssetPreviewHandler)
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-online-component");
        var assetId = new AssetId("asset-fsb5");
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("OnlineComponentPreview");
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
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);
        return new PreviewTestContext(
            workspaceContextService,
            workspaceShell,
            localizationService,
            new TestWorkspaceAssetInspectionGateway
            {
                GetAssetPreviewHandler = getAssetPreviewHandler
            },
            assetTab,
            details);
    }

    private static Result<AssetPreviewDto> CreateMissingComponentPreviewResult(GetAssetPreviewQuery query) =>
        Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: "text/plain",
            BinaryContent: null,
            TextContent: "transcoder unavailable",
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: new AssetPreviewMessageDto(
                AssetPreviewMessageKey.AudioTranscodeUnavailable,
                RequiredComponentId: OnlineComponentIds.VgmstreamCli),
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.GenerationFailed)));

    private sealed record PreviewTestContext(
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerWorkspaceShellViewModel WorkspaceShell,
        TestLocalizationService LocalizationService,
        TestWorkspaceAssetInspectionGateway InspectionGateway,
        AssetDocumentTabViewModel AssetTab,
        AssetDetailsDto Details)
    {
        public BundleExplorerAssetPreviewCoordinator CreateCoordinator(
            IOnlineComponentInstallPromptCoordinator promptCoordinator) =>
            new(
                InspectionGateway,
                WorkspaceContextService,
                WorkspaceShell,
                new TestAppLogService(),
                LocalizationService,
                new TestBackgroundTaskRunner(),
                onlineComponentInstallPromptCoordinator: promptCoordinator);
    }

    private sealed class TestOnlineComponentInstallPromptCoordinator(bool installResult)
        : IOnlineComponentInstallPromptCoordinator
    {
        public int CallCount { get; private set; }

        public string? LastComponentId { get; private set; }

        public Task<bool> EnsureInstalledAsync(string componentId)
        {
            CallCount++;
            LastComponentId = componentId;
            return Task.FromResult(installResult);
        }
    }
}
