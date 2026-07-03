using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Bundles.Queries;

internal static class GetResourceProcessorCatalogEntriesHandlerTests
{
    public static async Task HandleAsync_ShouldReturnDefaultExportProcessorSlots()
    {
        var handler = new GetResourceProcessorCatalogEntriesHandler(new TestResourceProcessorCatalogStore(CreateDefaultEntries()));

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
        var handler = new GetResourceProcessorCatalogEntriesHandler(new TestResourceProcessorCatalogStore(CreateDefaultEntries()));
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

    private static IReadOnlyList<ResourceProcessorCatalogEntryDto> CreateDefaultEntries()
    {
        return
        [
            CreatePlaceholder("resource.processor.texture2d.cli", "Texture2D"),
            CreatePlaceholder("resource.processor.audio.cli", "Audio"),
            CreatePlaceholder("resource.processor.atlas.cli", "Atlas"),
            CreatePlaceholder("resource.processor.skel.cli", "Skel")
        ];
    }

    private static ResourceProcessorCatalogEntryDto CreatePlaceholder(
        string id,
        string resourceKind)
    {
        return new ResourceProcessorCatalogEntryDto(
            id,
            id,
            Version: "1.0.0",
            Description: string.Empty,
            ResourceProcessorKind.ExternalProcess,
            [resourceKind],
            ResourceProcessorStatus.Available,
            IsBuiltIn: true,
            IsReadOnly: true,
            ManifestFilePath: null,
            ExecutablePath: null,
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments:
            [
                "--input",
                "{inputPath}",
                "--output",
                "{outputPath}",
                "--result",
                "{resultPath}",
                "--resource-kind",
                "{resourceKind}"
            ]);
    }

    internal sealed class TestResourceProcessorCatalogStore : IResourceProcessorCatalogStore
    {
        private readonly List<ResourceProcessorCatalogEntryDto> _entries;

        public TestResourceProcessorCatalogStore(IReadOnlyList<ResourceProcessorCatalogEntryDto>? entries = null)
        {
            _entries = entries?.ToList() ?? [];
        }

        public IReadOnlyList<ResourceProcessorCatalogEntryDto> Load()
        {
            return _entries.ToArray();
        }

        public void SaveUserManagedProcessor(
            ResourceProcessorDefinitionDto definition,
            string? previousProcessorId = null)
        {
            if (!string.IsNullOrWhiteSpace(previousProcessorId))
            {
                _entries.RemoveAll(entry => string.Equals(entry.Id, previousProcessorId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            _entries.RemoveAll(entry => string.Equals(entry.Id, definition.Id.Trim(), StringComparison.OrdinalIgnoreCase));
            _entries.Add(new ResourceProcessorCatalogEntryDto(
                definition.Id.Trim(),
                definition.DisplayName.Trim(),
                definition.Version,
                definition.Description ?? string.Empty,
                ResourceProcessorKind.ExternalProcess,
                definition.ResourceKinds.ToArray(),
                ResourceProcessorStatus.Available,
                IsBuiltIn: false,
                IsReadOnly: false,
                ManifestFilePath: $@"C:\Plugins\{definition.Id}.json",
                ExecutablePath: definition.ExecutablePath,
                WorkingDirectoryPath: definition.WorkingDirectoryPath,
                TimeoutMs: definition.TimeoutMs,
                ProcessArguments: definition.ProcessArguments.ToArray()));
        }

        public void DeleteUserManagedProcessor(string processorId)
        {
            _entries.RemoveAll(entry => string.Equals(entry.Id, processorId.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
