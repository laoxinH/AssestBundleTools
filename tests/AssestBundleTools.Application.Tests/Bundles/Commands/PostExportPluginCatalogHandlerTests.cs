using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Bundles.Commands;

internal static class PostExportPluginCatalogHandlerTests
{
    public static async Task Upsert_ShouldSavePostExportPlugin_WhenManifestPathArgumentIsPresent()
    {
        var store = new TestPostExportPluginCatalogStore();
        var handler = new UpsertPostExportPluginHandler(store);

        var result = await handler.HandleAsync(new UpsertPostExportPluginCommand(CreateDefinition()));

        TestAssert.True(result.IsSuccess, "A valid post-export plugin definition should be saved.");
        var entry = result.Value ?? throw new InvalidOperationException("Expected saved post-export plugin.");
        TestAssert.Equal("post-export.zip", entry.Id, "Saved plugin id should round-trip through the catalog.");
        TestAssert.Equal("{manifestPath}", entry.ProcessArguments[1], "The manifest argument should remain an independent ordered argument.");
    }

    public static async Task Upsert_ShouldRejectPostExportPlugin_WhenManifestPathArgumentIsMissing()
    {
        var store = new TestPostExportPluginCatalogStore();
        var handler = new UpsertPostExportPluginHandler(store);

        var result = await handler.HandleAsync(new UpsertPostExportPluginCommand(
            CreateDefinition(["--output", "{outputDirectory}"])));

        TestAssert.True(result.IsFailure, "A post-export plugin without a manifest path argument should be rejected.");
        TestAssert.Equal(
            "Bundle.PostExportPlugin.Catalog.Invalid",
            TestAssert.Single(result.Errors).Code,
            "The invalid manifest argument should use the stable catalog validation error.");
        TestAssert.Empty(store.Load(), "An invalid definition must not be persisted.");
    }

    private static PostExportPluginDefinitionDto CreateDefinition(IReadOnlyList<string>? arguments = null) =>
        new(
            Id: "post-export.zip",
            DisplayName: "ZIP Packager",
            Version: "1.0.0",
            Description: "Packages exported bundles.",
            ExecutablePath: "tools/post-export.exe",
            WorkingDirectoryPath: ".",
            ProcessArguments: arguments ?? ["--manifest", "{manifestPath}"]);

    private sealed class TestPostExportPluginCatalogStore : IPostExportPluginCatalogStore
    {
        private readonly List<PostExportPluginCatalogEntryDto> _entries = [];

        public IReadOnlyList<PostExportPluginCatalogEntryDto> Load() => _entries.ToArray();

        public void SaveUserManagedPlugin(PostExportPluginDefinitionDto definition, string? previousPluginId = null)
        {
            if (!string.IsNullOrWhiteSpace(previousPluginId))
            {
                _entries.RemoveAll(entry => string.Equals(entry.Id, previousPluginId, StringComparison.OrdinalIgnoreCase));
            }

            _entries.RemoveAll(entry => string.Equals(entry.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
            _entries.Add(new PostExportPluginCatalogEntryDto(
                definition.Id,
                definition.DisplayName,
                definition.Version,
                definition.Description ?? string.Empty,
                ResourceProcessorStatus.Available,
                IsReadOnly: false,
                ManifestFilePath: @"C:\Plugins\post-export.json",
                definition.ExecutablePath,
                definition.WorkingDirectoryPath,
                definition.ProcessArguments));
        }

        public void DeleteUserManagedPlugin(string pluginId) =>
            _entries.RemoveAll(entry => string.Equals(entry.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }
}
