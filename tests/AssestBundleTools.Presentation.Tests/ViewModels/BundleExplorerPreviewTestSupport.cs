using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

internal static class BundleExplorerPreviewTestSupport
{
    public static WorkspaceSummaryDto CreateWorkspaceSummary(
        WorkspaceId workspaceId,
        WorkspaceNavigationRequest workspaceRequest)
    {
        return new WorkspaceSummaryDto(
            workspaceId,
            workspaceRequest.WorkspaceDisplayName,
            workspaceRequest.SourcePath,
            WorkspaceMode.SinglePane,
            PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow);
    }

    public static AssetDocumentTabViewModel CreateAssetTab(
        BundleId bundleId,
        AssetId assetId,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            "Button",
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    public static Result<AssetPreviewDto> CreateSummaryPreviewResult(GetAssetPreviewQuery query)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: "text/plain",
            TextContent: "summary",
            BinaryContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None)));
    }

    public static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException();
            }

            await Task.Delay(10);
        }
    }

    public static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: "Button",
            AssetPath: "Assets/UI/Button.png",
            ContainerPath: "Assets/UI",
            AssetType: AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: null,
            PathId: 1001,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: "Texture2D");
    }
}
