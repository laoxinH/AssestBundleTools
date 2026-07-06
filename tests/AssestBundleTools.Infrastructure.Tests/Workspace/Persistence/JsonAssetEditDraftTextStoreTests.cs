using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonAssetEditDraftTextStoreTests
{
    public static void SaveTextPayload_ShouldReadChunkLinesAndSearch_FromDraftPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        const string text = "line-0\r\nline-1 target\rline-2 TARGET\nline-3";

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-text-1",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: text,
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero),
            SourceFingerprint: "draft-source-v1"));

        var chunk = TestAssert.NotNull(
            store.LoadTextChunk(
                "workspace-a",
                "draft-text-1",
                "text",
                offset: 7,
                length: 6),
            "Draft chunk should be readable.");
        TestAssert.Equal("line-1", chunk.Text, "Draft chunk should use normalized character offsets.");
        TestAssert.Equal(7, chunk.Offset, "Draft chunk should report requested normalized offset.");
        TestAssert.Equal(6, chunk.Length, "Draft chunk should report returned character count.");

        var lines = TestAssert.NotNull(
            store.LoadTextLines(
                "workspace-a",
                "draft-text-1",
                "text",
                startLineIndex: 1,
                lineCount: 2),
            "Draft line range should be readable.");
        TestAssert.Equal(2, lines.Lines.Count, "Draft line range should return requested lines.");
        TestAssert.Equal("line-1 target", lines.Lines[0], "Draft line range should normalize CRLF lines.");
        TestAssert.Equal("line-2 TARGET", lines.Lines[1], "Draft line range should normalize CR lines.");

        var search = TestAssert.NotNull(
            store.SearchText(
                "workspace-a",
                "draft-text-1",
                "text",
                searchText: "target",
                startLineIndex: 1,
                maxMatchCount: 1,
                matchCase: false),
            "Draft search should be readable.");
        var match = TestAssert.Single(search.Matches);
        TestAssert.Equal(1, match.LineIndex, "Draft search should preserve normalized line indices.");
        TestAssert.True(search.HasMore, "Draft search should report additional matches beyond the budget.");
    }

    public static void Delete_ShouldRemoveDraftPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-delete",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "line-0\nline-1",
            UpdatedAtUtc: DateTimeOffset.UtcNow));

        store.Delete("workspace-a", "draft-delete", "text");

        TestAssert.Null(
            store.LoadTextChunk(
                "workspace-a",
                "draft-delete",
                "text",
                offset: 0,
                length: 6),
            "Deleted draft payload should no longer be readable.");
    }

    public static void ApplyTextPatch_ShouldUpdatePayloadDescriptorAndLineIndex()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        const string originalText = "alpha\r\nbeta\ngamma";

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-patch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: originalText,
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero),
            SourceFingerprint: "draft-source-v1"));

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-patch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits:
            [
                new TextAssetEditDraftRangeEditDto(6, 4, "BETA"),
                new TextAssetEditDraftRangeEditDto(16, 0, "\nomega")
            ],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            SourceFingerprint: "draft-source-v2"));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, result.Status, "Valid range patch should be applied.");
        TestAssert.Equal("alpha\nBETA\ngamma\nomega".Length, result.TextLength, "Patch result should report normalized text length.");
        TestAssert.Equal(
            System.Text.Encoding.UTF8.GetByteCount("alpha\nBETA\ngamma\nomega"),
            result.ContentLength,
            "Patch result should report UTF-8 payload length.");

        var chunk = TestAssert.NotNull(
            store.LoadTextChunk(
                "workspace-a",
                "draft-patch",
                "text",
                offset: 0,
                length: 64),
            "Patched draft chunk should be readable.");
        TestAssert.Equal("alpha\nBETA\ngamma\nomega", chunk.Text, "Patched payload should be written back.");
        TestAssert.Equal("draft-source-v2", chunk.Descriptor.SourceFingerprint.ContentHash, "Patched descriptor should carry new source fingerprint.");

        var lines = TestAssert.NotNull(
            store.LoadTextLines(
                "workspace-a",
                "draft-patch",
                "text",
                startLineIndex: 2,
                lineCount: 2),
            "Patched line index should be readable.");
        TestAssert.Equal("gamma", lines.Lines[0], "Patched line index should expose updated third line.");
        TestAssert.Equal("omega", lines.Lines[1], "Patched line index should expose appended line.");

        var search = TestAssert.NotNull(
            store.SearchText(
                "workspace-a",
                "draft-patch",
                "text",
                searchText: "omega",
                startLineIndex: 0,
                maxMatchCount: 1,
                matchCase: true),
            "Patched search index should be readable.");
        TestAssert.Equal(3, TestAssert.Single(search.Matches).LineIndex, "Search should see appended patched content.");
    }

    public static void ApplyTextPatch_ShouldAppendPatchLog_WithoutRewritingPayloadFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-log",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha\nbeta\ngamma",
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero)));
        var payloadPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "drafts",
            "workspace-a",
            "draft-log",
            "text",
            "text.txt");
        var payloadBefore = File.ReadAllText(payloadPath);

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-log",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, result.Status, "Patch should be accepted.");
        TestAssert.Equal(payloadBefore, File.ReadAllText(payloadPath), "Patch-backed save should not rewrite the base payload file.");
        TestAssert.True(
            File.Exists(Path.Combine(Path.GetDirectoryName(payloadPath)!, "text-patches.json")),
            "Patch-backed save should persist a patch manifest.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-log", "text", 0, 64),
            "Patched draft should be readable through the composite view.");
        TestAssert.Equal("alpha\nBETA\ngamma", chunk.Text, "Composite view should apply the patch over the base payload.");
    }

    public static void ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredSourceReference()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var previewStore = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        const string sourceText = "alpha\nbeta\ngamma";
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(sourceText);
        var descriptor = new PreviewCacheDescriptorDto(
            WorkspaceKey: "workspace-a",
            PreviewCacheKey: "preview-source",
            BundleCacheKey: "bundle-a",
            AssetId: "asset-a",
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: "text/plain; charset=utf-8",
            RelativePayloadPath: "payloads/source.txt",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: "source",
                FileSizeInBytes: System.Text.Encoding.UTF8.GetByteCount(sourceText),
                LastWriteTimeUtc: new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero),
                ContentHash: "source-v1"),
            GeneratedAtUtc: new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero),
            GeneratorVersion: "test",
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(null, display.Metadata));
        previewStore.Save(new PreviewCachePayloadDto(
            descriptor,
            System.Text.Encoding.UTF8.GetBytes(sourceText)));

        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-deferred",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create(sourceText),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            SourceReference: new WorkspaceAssetEditDraftSourceReferenceDto(
                nameof(AssetPreviewFullContentReferenceKind.PreviewCache),
                "text/plain; charset=utf-8",
                System.Text.Encoding.UTF8.GetByteCount(sourceText),
                "workspace-a",
                PreviewCacheKey: "preview-source")));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, result.Status, "Deferred source patch should be accepted.");
        var draftDirectory = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "drafts",
            "workspace-a",
            "draft-deferred",
            "text");
        TestAssert.False(File.Exists(Path.Combine(draftDirectory, "text.txt")), "Deferred patch should not materialize a full draft payload.");
        TestAssert.True(File.Exists(Path.Combine(draftDirectory, "text-base.txt")), "Deferred patch should persist a stable base snapshot.");
        TestAssert.True(File.Exists(Path.Combine(draftDirectory, "text-patches.json")), "Deferred patch should persist patch manifest.");
        previewStore.Delete("workspace-a", "preview-source");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-deferred", "text", 0, 64),
            "Deferred patched draft should be readable through the composite view after source cache is removed.");
        TestAssert.Equal("alpha\nBETA\ngamma", chunk.Text, "Composite view should apply deferred patch over source reference.");
    }

    public static void ApplyTextPatch_ShouldMaterializeFullReplacement_WhenDeferredSourceCacheWasPruned()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        const string sourceText = "alpha\nbeta\ngamma";
        const string editedText = "alpha\nBETA\ngamma";

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-pruned-source",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(0, sourceText.Length, editedText)],
            Baseline: TextAssetEditDraftPatchBaseline.Create(sourceText),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            SourceReference: new WorkspaceAssetEditDraftSourceReferenceDto(
                nameof(AssetPreviewFullContentReferenceKind.PreviewCache),
                "text/plain; charset=utf-8",
                System.Text.Encoding.UTF8.GetByteCount(sourceText),
                "workspace-a",
                PreviewCacheKey: "preview-source-pruned")));

        TestAssert.Equal(
            AssetEditDraftTextPatchStatus.Applied,
            result.Status,
            "Full replacement patch should still save when the deferred preview cache source was pruned.");
        TestAssert.Equal(0, result.Revision, "Materialized fallback should expose the payload base revision.");
        var draftDirectory = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "drafts",
            "workspace-a",
            "draft-pruned-source",
            "text");
        TestAssert.True(File.Exists(Path.Combine(draftDirectory, "text.txt")), "Fallback should materialize a self-contained draft payload.");
        TestAssert.False(File.Exists(Path.Combine(draftDirectory, "text-base.txt")), "Fallback should not create a deferred base snapshot without source text.");
        TestAssert.False(File.Exists(Path.Combine(draftDirectory, "text-patches.json")), "Fallback payload should not depend on a patch manifest.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-pruned-source", "text", 0, 64),
            "Materialized fallback draft should be readable.");
        TestAssert.Equal(editedText, chunk.Text, "Materialized fallback should preserve the editor text.");
    }

    public static void ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredFileReplacementSourceReference()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var sourceFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "large-atlas.json");
        const string sourceText = "alpha\nbeta\ngamma";
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        File.WriteAllText(sourceFilePath, sourceText);

        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-file-replacement",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create(sourceText),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            SourceReference: new WorkspaceAssetEditDraftSourceReferenceDto(
                nameof(AssetPreviewFullContentReferenceKind.FileReplacement),
                "text/plain; charset=utf-8",
                System.Text.Encoding.UTF8.GetByteCount(sourceText),
                "workspace-a",
                SourcePath: sourceFilePath)));

        TestAssert.Equal(
            AssetEditDraftTextPatchStatus.Applied,
            result.Status,
            "Deferred file replacement source patch should be accepted.");
        var draftDirectory = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "drafts",
            "workspace-a",
            "draft-file-replacement",
            "text");
        TestAssert.False(File.Exists(Path.Combine(draftDirectory, "text.txt")), "Deferred file replacement patch should not materialize a full draft payload.");
        TestAssert.True(File.Exists(Path.Combine(draftDirectory, "text-base.txt")), "Deferred file replacement patch should persist a stable base snapshot.");
        TestAssert.True(File.Exists(Path.Combine(draftDirectory, "text-patches.json")), "Deferred file replacement patch should persist patch manifest.");
        File.Delete(sourceFilePath);
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-file-replacement", "text", 0, 64),
            "Deferred file replacement patched draft should be readable after the source file is removed.");
        TestAssert.Equal("alpha\nBETA\ngamma", chunk.Text, "Composite view should apply file replacement patch over stable base snapshot.");
    }

    public static void ApplyTextPatch_ShouldApplySubsequentPatchLogEntries()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-multi-patch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha\nbeta\ngamma",
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero)));

        var first = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-multi-patch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));
        var second = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-multi-patch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto("alpha\nBETA\ngamma".Length, 0, "\nomega")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nBETA\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 11, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, first.Status, "First patch should be accepted.");
        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, second.Status, "Second patch should be accepted against patched baseline.");
        TestAssert.Equal(1, first.Revision, "First patch should expose revision 1.");
        TestAssert.Equal(2, second.Revision, "Second patch should expose revision 2.");
        var firstRevisionChunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-multi-patch", "text", 0, 64, revision: first.Revision),
            "First patch revision should be readable.");
        TestAssert.Equal("alpha\nBETA\ngamma", firstRevisionChunk.Text, "Revision 1 should replay only the first patch.");
        var baseRevisionChunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-multi-patch", "text", 0, 64, revision: 0),
            "Base revision should be readable.");
        TestAssert.Equal("alpha\nbeta\ngamma", baseRevisionChunk.Text, "Revision 0 should expose the stable base text.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-multi-patch", "text", 0, 64),
            "Multi-patch draft should be readable.");
        TestAssert.Equal("alpha\nBETA\ngamma\nomega", chunk.Text, "Composite view should replay all patch log entries in order.");
    }

    public static void ApplyTextPatch_ShouldReadOffsetChunk_FromPatchBackedDraft()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        var sourceText = string.Concat(
            Enumerable.Range(0, 9000).Select(index => $"line-{index:D4}: abcdefghijklmnopqrstuvwxyz\n"));

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-offset-chunk",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: sourceText,
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero)));

        var patchOffset = sourceText.IndexOf("line-4500", StringComparison.Ordinal);
        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-offset-chunk",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(patchOffset, 9, "LINE-4500")],
            Baseline: TextAssetEditDraftPatchBaseline.Create(sourceText),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: sourceText.Length + 16));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, result.Status, "Large patch-backed draft should be accepted.");
        var requestedOffset = patchOffset - 20;
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-offset-chunk", "text", requestedOffset, 80),
            "Patch-backed draft should support direct offset chunk reads.");

        var expected = sourceText[..patchOffset] + "LINE-4500" + sourceText[(patchOffset + 9)..];
        TestAssert.Equal(requestedOffset, chunk.Offset, "Chunk offset should remain the requested composite text offset.");
        TestAssert.Equal(expected.Substring(requestedOffset, 80), chunk.Text, "Offset chunk should be read from the patched composite text.");

        var sidecarPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "drafts",
            "workspace-a",
            "draft-offset-chunk",
            "text",
            "text.lines.json");
        TestAssert.True(File.Exists(sidecarPath), "Patch-backed payload reads should create or reuse a character anchor sidecar.");
    }

    public static void ApplyTextPatch_ShouldBranchFromRequestedRevision_WhenHeadIsNewer()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);

        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-branch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha\nbeta\ngamma",
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero)));

        var first = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-branch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));
        var second = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-branch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto("alpha\nBETA\ngamma".Length, 0, "\nomega")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nBETA\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 11, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));

        var branch = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-branch",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto("alpha\nBETA\ngamma".Length, 0, "\ndelta")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nBETA\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            BaseRevision: first.Revision));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, first.Status, "First patch should be accepted.");
        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, second.Status, "Second patch should be accepted before branching.");
        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, branch.Status, "Branch patch should be accepted against requested revision.");
        TestAssert.Equal(2, branch.Revision, "Branch patch should replace the abandoned tail and become the new head revision.");

        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-branch", "text", 0, 64),
            "Branched draft should be readable.");
        TestAssert.Equal(
            "alpha\nBETA\ngamma\ndelta",
            chunk.Text,
            "Branch save should continue from revision 1 instead of the stale head revision.");
    }

    public static void ApplyTextPatch_ShouldUseNormalizedOffsets_ForDeferredCrlfSource()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var previewStore = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        const string sourceText = "alpha\r\nbeta\r\ngamma";
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(sourceText);
        var descriptor = new PreviewCacheDescriptorDto(
            WorkspaceKey: "workspace-a",
            PreviewCacheKey: "preview-crlf",
            BundleCacheKey: "bundle-a",
            AssetId: "asset-a",
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: "text/plain; charset=utf-8",
            RelativePayloadPath: "payloads/source-crlf.txt",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: "source-crlf",
                FileSizeInBytes: System.Text.Encoding.UTF8.GetByteCount(sourceText),
                LastWriteTimeUtc: new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero),
                ContentHash: "source-crlf-v1"),
            GeneratedAtUtc: new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero),
            GeneratorVersion: "test",
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(null, display.Metadata));
        previewStore.Save(new PreviewCachePayloadDto(
            descriptor,
            System.Text.Encoding.UTF8.GetBytes(sourceText)));

        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        var first = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-crlf-deferred",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 4, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024,
            SourceReference: new WorkspaceAssetEditDraftSourceReferenceDto(
                nameof(AssetPreviewFullContentReferenceKind.PreviewCache),
                "text/plain; charset=utf-8",
                System.Text.Encoding.UTF8.GetByteCount(sourceText),
                "workspace-a",
                PreviewCacheKey: "preview-crlf")));
        previewStore.Delete("workspace-a", "preview-crlf");
        var second = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-crlf-deferred",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto("alpha\nBETA\n".Length, 5, "GAMMA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nBETA\ngamma"),
            UpdatedAtUtc: new DateTimeOffset(2026, 7, 4, 11, 0, 0, TimeSpan.Zero),
            MaxTextLength: 1024));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, first.Status, "Deferred CRLF source patch should be accepted.");
        TestAssert.Equal(AssetEditDraftTextPatchStatus.Applied, second.Status, "Deferred manifest should remain editable without passing source reference again.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk("workspace-a", "draft-crlf-deferred", "text", 0, 64),
            "Deferred CRLF patched draft should be readable.");
        TestAssert.Equal("alpha\nBETA\nGAMMA", chunk.Text, "Patch offsets should use normalized text coordinates, not raw CRLF file coordinates.");
    }

    public static void ApplyTextPatch_ShouldRejectStaleBaseline_WithoutChangingPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-stale",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha\nchanged",
            UpdatedAtUtc: DateTimeOffset.UtcNow));

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-stale",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(6, 7, "BETA")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha\nbeta"),
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MaxTextLength: 1024));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.BaselineMismatch, result.Status, "Stale baseline should be rejected by the store.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk(
                "workspace-a",
                "draft-stale",
                "text",
                offset: 0,
                length: 64),
            "Original draft should remain readable after stale patch.");
        TestAssert.Equal("alpha\nchanged", chunk.Text, "Stale patch should not alter payload.");
    }

    public static void ApplyTextPatch_ShouldRejectInvalidRange_WithoutChangingPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-invalid-range",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha",
            UpdatedAtUtc: DateTimeOffset.UtcNow));

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-invalid-range",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(9, 1, "x")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha"),
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MaxTextLength: 1024));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.RangeInvalid, result.Status, "Invalid range should be rejected by the store.");
        TestAssert.Equal(9, result.InvalidRangeStart, "Rejected range start should be reported.");
        TestAssert.Equal(1, result.InvalidRangeLength, "Rejected range length should be reported.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk(
                "workspace-a",
                "draft-invalid-range",
                "text",
                offset: 0,
                length: 64),
            "Original draft should remain readable after invalid range.");
        TestAssert.Equal("alpha", chunk.Text, "Invalid range should not alter payload.");
    }

    public static void ApplyTextPatch_ShouldRejectLengthExceeded_WithoutChangingPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);
        store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-length",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            TextContent: "alpha",
            UpdatedAtUtc: DateTimeOffset.UtcNow));

        var result = store.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-length",
            DraftMode: "text",
            ContentType: "text/plain; charset=utf-8",
            RangeEdits: [new TextAssetEditDraftRangeEditDto(5, 0, " beta")],
            Baseline: TextAssetEditDraftPatchBaseline.Create("alpha"),
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MaxTextLength: 5));

        TestAssert.Equal(AssetEditDraftTextPatchStatus.LengthExceeded, result.Status, "Oversized patch result should be rejected by the store.");
        var chunk = TestAssert.NotNull(
            store.LoadTextChunk(
                "workspace-a",
                "draft-length",
                "text",
                offset: 0,
                length: 64),
            "Original draft should remain readable after oversized patch.");
        TestAssert.Equal("alpha", chunk.Text, "Oversized patch should not alter payload.");
    }

    public static void SaveTextPayload_ShouldRejectInvalidDraftPathSegments()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAssetEditDraftTextStore(temporaryDirectory.DirectoryPath);

        try
        {
            store.SaveTextPayload(new AssetEditDraftTextPayloadDto(
                WorkspaceKey: "workspace-a",
                DraftId: "..\\escape",
                DraftMode: "text",
                ContentType: "text/plain; charset=utf-8",
                TextContent: "payload",
                UpdatedAtUtc: DateTimeOffset.UtcNow));
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("Invalid draft path segments should be rejected before writing.");
    }
}
