using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class TextAssetEditDraftViewModelTests
{
    [Fact]
    public async Task LoadMoreLinesAsync_ShouldAppendNextPage_ThroughApplicationGateway()
    {
        var queries = new List<LoadPreviewTextLinesQuery>();
        var reference = CreateReference();
        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadPreviewTextLinesHandler = query =>
            {
                queries.Add(query);
                return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(CreateLineRange(
                    reference,
                    query.StartLineIndex,
                    query.StartLineIndex == 0 ? 200 : 150,
                    totalLineCount: 350,
                    hasMore: query.StartLineIndex == 0)));
            }
        };
        var viewModel = CreateViewModel(reference, gateway);

        await viewModel.LoadInitialLinesAsync();

        Assert.Single(queries);
        Assert.Equal(0, queries[0].StartLineIndex);
        Assert.Equal(200, queries[0].LineCount);
        Assert.Equal(200, viewModel.Lines.Count);
        Assert.True(viewModel.CanLoadMoreLines);
        Assert.Equal(Visibility.Visible, viewModel.LoadMoreLinesButtonVisibility);
        Assert.Equal("loaded 200 / 350", viewModel.StatusText);

        await viewModel.LoadMoreLinesAsync();

        Assert.Equal(2, queries.Count);
        Assert.Equal(200, queries[1].StartLineIndex);
        Assert.Equal(200, queries[1].LineCount);
        Assert.Equal(350, viewModel.Lines.Count);
        Assert.Equal("350", viewModel.Lines[^1].LineNumberText);
        Assert.False(viewModel.CanLoadMoreLines);
        Assert.Equal(Visibility.Collapsed, viewModel.LoadMoreLinesButtonVisibility);
        Assert.Equal("loaded 350 / 350", viewModel.StatusText);
    }

    private static AssetPreviewFullContentReferenceDto CreateReference() =>
        new(
            AssetPreviewFullContentReferenceKind.EditDraft,
            "text/plain; charset=utf-8",
            4096,
            "workspace-a",
            DraftId: "draft-text-1",
            DraftMode: "text-content");

    private static AssetPreviewTextLineRangeDto CreateLineRange(
        AssetPreviewFullContentReferenceDto reference,
        int startLineIndex,
        int lineCount,
        int totalLineCount,
        bool hasMore)
    {
        var lines = Enumerable.Range(startLineIndex + 1, lineCount)
            .Select(lineNumber => $"line {lineNumber}")
            .ToArray();
        return new AssetPreviewTextLineRangeDto(
            reference,
            lines,
            startLineIndex,
            lineCount,
            totalLineCount,
            hasMore);
    }

    private static TextAssetEditDraftViewModel CreateViewModel(
        AssetPreviewFullContentReferenceDto reference,
        TestWorkspaceAssetInspectionGateway gateway) =>
        new(
            new AssetTextEditDraftDto(
                WorkspaceId: new WorkspaceId("workspace-session"),
                BundleId: new BundleId("bundle-a"),
                AssetId: new AssetId("asset-1"),
                AssetName: "story.json",
                AssetType: AssetType.TextAsset,
                AssetTypeDisplayName: AssetType.TextAsset.ToString(),
                DraftId: "draft-text-1",
                DraftMode: "text-content",
                Reference: reference,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                CanApply: true,
                Summary: "TextAsset",
                CanEditInline: false,
                SourceFingerprint: "source-v1",
                SourceDescription: "story.json"),
            gateway,
            new TestBackgroundTaskRunner(),
            @"D:\WorkspaceData\A",
            "story.json",
            "read only",
            "inline",
            "loaded {0} / {1}");
}
