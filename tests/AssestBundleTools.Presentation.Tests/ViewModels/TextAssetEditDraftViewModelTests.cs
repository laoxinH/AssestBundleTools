using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
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

    [Fact]
    public async Task InitializeAsync_ShouldDeferFullTextLoad_ForLargeDeferredDraft()
    {
        var lineQueries = new List<LoadPreviewTextLinesQuery>();
        var chunkQueries = new List<LoadPreviewTextChunkQuery>();
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain; charset=utf-8",
            12 * 1024 * 1024,
            "workspace-a",
            PreviewCacheKey: "preview-large-text");
        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadPreviewTextLinesHandler = query =>
            {
                lineQueries.Add(query);
                return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(CreateLineRange(
                    reference,
                    query.StartLineIndex,
                    200,
                    totalLineCount: 5000,
                    hasMore: true)));
            },
            LoadPreviewTextChunkHandler = query =>
            {
                chunkQueries.Add(query);
                return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
                    reference,
                    "full text",
                    query.Offset,
                    "full text".Length,
                    "full text".Length,
                    HasMore: false)));
            }
        };
        var viewModel = CreateViewModel(
            reference,
            gateway,
            canEditInline: true,
            isMaterialized: false);

        await viewModel.InitializeAsync();

        Assert.Single(lineQueries);
        Assert.Empty(chunkQueries);
        Assert.True(viewModel.CanLoadFullEdit);
        Assert.Equal(Visibility.Visible, viewModel.LoadFullEditButtonVisibility);
        Assert.Equal(Visibility.Visible, viewModel.ReadOnlyViewVisibility);

        await viewModel.LoadFullEditAsync();

        Assert.Single(chunkQueries);
        Assert.False(viewModel.CanLoadFullEdit);
        Assert.Equal(Visibility.Visible, viewModel.EditableViewVisibility);
    }

    [Fact]
    public void CreateSaveRangeEdits_ShouldUsePendingSurfaceRangeEdits()
    {
        var reference = CreateReference();
        var gateway = new TestWorkspaceAssetInspectionGateway();
        var viewModel = CreateViewModel(reference, gateway, canEditInline: true);
        viewModel.SetEditorTextFromSurface("alpha beta");
        viewModel.MarkSaved();
        viewModel.MarkEditorDirty();

        viewModel.SetEditorRangeEditsFromSurface(
            [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            TextAssetEditDraftPatchBaseline.Create("alpha beta"));

        var edits = viewModel.CreateSaveRangeEdits();
        var baseline = viewModel.CreateSavePatchBaseline();

        Assert.True(viewModel.CanSave);
        Assert.Single(edits);
        Assert.Equal(6, edits[0].Start);
        Assert.Equal(4, edits[0].Length);
        Assert.Equal("BETA", edits[0].Text);
        Assert.Equal("alpha beta".Length, baseline.ContentLength);
        Assert.Equal(
            TextAssetEditDraftPatchBaseline.Create("alpha beta").ContentSha256,
            baseline.ContentSha256);

        viewModel.MarkSaved();

        Assert.False(viewModel.HasUnsavedChanges);
        Assert.Equal("alpha BETA", viewModel.EditableText);
    }

    [Fact]
    public void CreateSaveRangeEdits_ShouldFallbackToFullPatch_WhenPendingRangeBaseIsStale()
    {
        var reference = CreateReference();
        var gateway = new TestWorkspaceAssetInspectionGateway();
        var viewModel = CreateViewModel(reference, gateway, canEditInline: true);
        viewModel.SetEditorTextFromSurface("alpha beta");
        viewModel.MarkSaved();
        viewModel.SetEditorTextFromSurface("alpha gamma");
        viewModel.SetEditorRangeEditsFromSurface(
            [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            TextAssetEditDraftPatchBaseline.Create("x"));

        var edits = viewModel.CreateSaveRangeEdits();

        Assert.Single(edits);
        Assert.Equal(0, edits[0].Start);
        Assert.Equal("alpha beta".Length, edits[0].Length);
        Assert.Equal("alpha gamma", edits[0].Text);
    }

    [Fact]
    public async Task LoadFullEditAsync_ShouldStreamChunks_WhenSurfaceSupportsStreaming()
    {
        var chunkQueries = new List<LoadPreviewTextChunkQuery>();
        var reference = CreateReference() with { ContentLength = 12 };
        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadPreviewTextChunkHandler = query =>
            {
                chunkQueries.Add(query);
                var text = query.Offset == 0 ? "alpha " : "beta";
                return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
                    reference,
                    text,
                    query.Offset,
                    text.Length,
                    "alpha beta".Length,
                    HasMore: query.Offset == 0)));
            }
        };
        var viewModel = CreateViewModel(reference, gateway, canEditInline: true);
        var sink = new TestTextDocumentStreamingSink();

        await viewModel.LoadFullEditAsync(sink);

        Assert.Equal(2, chunkQueries.Count);
        Assert.Equal(["alpha ", "beta"], sink.Chunks);
        Assert.True(sink.Ended);
        Assert.True(viewModel.UsesStreamingDocumentModel);
        Assert.False(viewModel.CanLoadFullEdit);
        Assert.Equal("alpha beta".Length, viewModel.CurrentPatchBaseline?.ContentLength);
        Assert.Equal(
            TextAssetEditDraftPatchBaseline.Create("alpha beta").ContentSha256,
            viewModel.CurrentPatchBaseline?.ContentSha256);
    }

    [Fact]
    public async Task LoadFullEditAsync_ShouldExposeLoadingState_WhileStreamingDocumentLoads()
    {
        var firstChunkRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstChunk = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reference = CreateReference() with { ContentLength = 12 };
        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadPreviewTextChunkHandler = async query =>
            {
                firstChunkRequested.TrySetResult(true);
                await allowFirstChunk.Task;
                return Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
                    reference,
                    "alpha beta",
                    query.Offset,
                    "alpha beta".Length,
                    "alpha beta".Length,
                    HasMore: false));
            }
        };
        var viewModel = CreateViewModel(reference, gateway, canEditInline: true);
        var sink = new TestTextDocumentStreamingSink();

        var loadTask = viewModel.LoadFullEditAsync(sink);
        await AssertCompletesAsync(firstChunkRequested.Task);

        Assert.True(viewModel.IsFullEditLoading);
        Assert.False(viewModel.CanSave);

        allowFirstChunk.SetResult(true);
        await loadTask;

        Assert.False(viewModel.IsFullEditLoading);
        Assert.False(viewModel.CanSave);

        viewModel.MarkEditorDirty();

        Assert.True(viewModel.CanSave);
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
        TestWorkspaceAssetInspectionGateway gateway,
        bool canEditInline = false,
        bool isMaterialized = true) =>
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
                CanEditInline: canEditInline,
                SourceFingerprint: "source-v1",
                SourceDescription: "story.json",
                IsMaterialized: isMaterialized),
            gateway,
            new TestBackgroundTaskRunner(),
            @"D:\WorkspaceData\A",
            "story.json",
            "read only",
            "inline",
            "deferred",
            "loaded {0} / {1}");

    private static async Task AssertCompletesAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed);
        await task;
    }

    private sealed class TestTextDocumentStreamingSink : ITextDocumentStreamingSink
    {
        public bool CanStreamDocument { get; init; } = true;

        public List<string> Chunks { get; } = new();

        public bool Ended { get; private set; }

        public Task BeginStreamingDocumentAsync(
            string language,
            long expectedLength,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AppendDocumentChunkAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            Chunks.Add(text);
            return Task.CompletedTask;
        }

        public Task EndStreamingDocumentAsync(
            TextAssetEditDraftPatchBaselineDto baseline,
            bool readOnly,
            CancellationToken cancellationToken = default)
        {
            Ended = true;
            return Task.CompletedTask;
        }

        public Task CancelStreamingDocumentAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
