using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Bundles.Queries;

internal static class GetResourceProcessorCatalogEntriesHandlerTests
{
    public static async Task HandleAsync_ShouldReturnDefaultExportProcessorSlots()
    {
        var handler = new GetResourceProcessorCatalogEntriesHandler();

        var result = await handler.HandleAsync(new GetResourceProcessorCatalogEntriesQuery());

        TestAssert.True(result.IsSuccess, "Resource processor catalog query should succeed.");
        var entries = result.Value ?? throw new InvalidOperationException("Expected resource processor catalog entries.");
        TestAssert.Equal(4, entries.Count, "Default catalog should expose the first export processor slots.");
        AssertContainsResourceKind(entries, "Texture2D");
        AssertContainsResourceKind(entries, "Audio");
        AssertContainsResourceKind(entries, "Atlas");
        AssertContainsResourceKind(entries, "Skel");
        TestAssert.True(
            entries.All(static entry => entry.Status == ResourceProcessorStatus.Available),
            "Placeholder processors should be available for first-stage UI selection.");
        TestAssert.True(
            entries.All(static entry => entry.IsReadOnly),
            "First-stage built-in placeholder processors should be read-only.");
    }

    public static async Task HandleAsync_ShouldReturnCancelled_WhenTokenIsCancelled()
    {
        var handler = new GetResourceProcessorCatalogEntriesHandler();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await handler.HandleAsync(
            new GetResourceProcessorCatalogEntriesQuery(),
            cancellationTokenSource.Token);

        TestAssert.True(result.IsCancelled, "Cancelled resource processor query should return a cancelled result.");
    }

    private static void AssertContainsResourceKind(
        IReadOnlyList<ResourceProcessorCatalogEntryDto> entries,
        string resourceKind)
    {
        TestAssert.True(
            entries.Any(entry => entry.ResourceKinds.Any(kind => string.Equals(kind, resourceKind, StringComparison.Ordinal))),
            $"Catalog should include a processor slot for {resourceKind}.");
    }
}
