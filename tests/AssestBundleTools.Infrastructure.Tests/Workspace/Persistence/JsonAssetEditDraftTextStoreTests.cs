using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
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
