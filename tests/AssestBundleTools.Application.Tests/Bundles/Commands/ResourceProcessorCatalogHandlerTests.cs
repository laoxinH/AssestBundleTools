using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Tests.Bundles.Queries;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Bundles.Commands;

internal static class ResourceProcessorCatalogHandlerTests
{
    public static async Task Upsert_ShouldSaveUserManagedProcessor_WhenDefinitionIsValid()
    {
        var store = new GetResourceProcessorCatalogEntriesHandlerTests.TestResourceProcessorCatalogStore();
        var handler = new UpsertResourceProcessorHandler(store);

        var result = await handler.HandleAsync(new UpsertResourceProcessorCommand(CreateDefinition()));

        TestAssert.True(result.IsSuccess, "Valid resource processor definitions should be saved.");
        var entry = result.Value ?? throw new InvalidOperationException("Expected saved resource processor entry.");
        TestAssert.Equal("export.texture.png-processor", entry.Id, "Saved entry should keep the definition id.");
        TestAssert.Equal("Texture2D", entry.ResourceKinds[0], "Saved entry should expose the selected resource kind.");
        TestAssert.False(entry.IsReadOnly, "Saved user resource processors should be editable.");
    }

    public static async Task Upsert_ShouldReturnValidationFailure_WhenResourceKindsAreMissing()
    {
        var store = new GetResourceProcessorCatalogEntriesHandlerTests.TestResourceProcessorCatalogStore();
        var handler = new UpsertResourceProcessorHandler(store);

        var result = await handler.HandleAsync(new UpsertResourceProcessorCommand(CreateDefinition(resourceKinds: [])));

        TestAssert.True(result.IsFailure, "Missing resource kinds should fail validation.");
        TestAssert.Equal(
            "Bundle.ResourceProcessor.Catalog.ResourceKindsRequired",
            TestAssert.Single(result.Errors).Code,
            "Failure should use the resource kind validation error.");
    }

    public static async Task Delete_ShouldRejectReadOnlyProcessor()
    {
        var store = new GetResourceProcessorCatalogEntriesHandlerTests.TestResourceProcessorCatalogStore(
        [
            new ResourceProcessorCatalogEntryDto(
                "resource.processor.texture2d.cli",
                "Texture2D",
                "1.0.0",
                string.Empty,
                ResourceProcessorKind.ExternalProcess,
                ["Texture2D"],
                ResourceProcessorStatus.Available,
                IsBuiltIn: true,
                IsReadOnly: true,
                ManifestFilePath: null,
                ExecutablePath: null,
                WorkingDirectoryPath: null,
                TimeoutMs: 15000,
                ProcessArguments: ["--input", "{inputPath}"])
        ]);
        var handler = new DeleteResourceProcessorHandler(store);

        var result = await handler.HandleAsync(new DeleteResourceProcessorCommand("resource.processor.texture2d.cli"));

        TestAssert.True(result.IsFailure, "Read-only resource processor definitions should not be deleted.");
        TestAssert.Equal(
            "Bundle.ResourceProcessor.Catalog.ReadOnly",
            TestAssert.Single(result.Errors).Code,
            "Failure should use the read-only catalog error.");
    }

    private static ResourceProcessorDefinitionDto CreateDefinition(IReadOnlyList<string>? resourceKinds = null)
    {
        return new ResourceProcessorDefinitionDto(
            Id: "export.texture.png-processor",
            DisplayName: "Texture PNG Processor",
            Version: "1.0.0",
            Description: "Processes Texture2D PNG payloads before bundle export.",
            ResourceKinds: resourceKinds ?? ["Texture2D"],
            ExecutablePath: "tools/png-processor.exe",
            WorkingDirectoryPath: ".",
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
}
