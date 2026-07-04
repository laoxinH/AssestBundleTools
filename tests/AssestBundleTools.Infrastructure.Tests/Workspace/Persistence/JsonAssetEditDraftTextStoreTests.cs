using AssestBundleTools.Application.Editing.Dtos;
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
