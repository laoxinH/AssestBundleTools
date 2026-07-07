using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Processing;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Processing;

internal static class FileSystemResourceProcessorCatalogStoreTests
{
    public static void Load_ShouldIncludeBuiltInSlots_AndUserManagedProcessors()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new FileSystemResourceProcessorCatalogStore([temporaryDirectory.DirectoryPath]);

        store.SaveUserManagedProcessor(CreateDefinition());

        var entries = store.Load();

        TestAssert.True(
            entries.Any(static entry => entry.IsBuiltIn && entry.ResourceKinds.Contains("Texture2D", StringComparer.OrdinalIgnoreCase)),
            "Built-in Texture2D resource processor slot should be present.");
        var audioEntry = entries.FirstOrDefault(static entry => string.Equals(entry.Id, "resource.processor.audio.fmod-fsb5", StringComparison.OrdinalIgnoreCase));
        audioEntry = TestAssert.NotNull(audioEntry, "Built-in FMOD audio resource processor should be present.");
        TestAssert.True(audioEntry.IsReadOnly, "Built-in FMOD audio resource processor should be read-only.");
        TestAssert.Equal("Audio", audioEntry.ResourceKinds[0], "Built-in FMOD audio resource processor should target the audio resource slot.");
        var userEntry = entries.FirstOrDefault(static entry => string.Equals(entry.Id, "export.texture.png-processor", StringComparison.OrdinalIgnoreCase));
        userEntry = TestAssert.NotNull(userEntry, "Saved user resource processor should be loaded from the manifest directory.");
        TestAssert.False(userEntry.IsReadOnly, "User manifest entries should be editable.");
        TestAssert.Equal("Texture PNG Processor", userEntry.DisplayName, "Display name should round-trip through manifest JSON.");
        TestAssert.Equal("Texture2D", userEntry.ResourceKinds[0], "Resource kinds should round-trip through manifest JSON.");
        TestAssert.Equal("--input", userEntry.ProcessArguments[0], "Process arguments should preserve their order.");
    }

    public static void DeleteUserManagedProcessor_ShouldRemoveManifest()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new FileSystemResourceProcessorCatalogStore([temporaryDirectory.DirectoryPath]);
        store.SaveUserManagedProcessor(CreateDefinition());

        store.DeleteUserManagedProcessor("export.texture.png-processor");

        var entries = store.Load();
        TestAssert.False(
            entries.Any(static entry => string.Equals(entry.Id, "export.texture.png-processor", StringComparison.OrdinalIgnoreCase)),
            "Deleted user resource processor manifest should no longer be loaded.");
    }

    private static ResourceProcessorDefinitionDto CreateDefinition()
    {
        return new ResourceProcessorDefinitionDto(
            Id: "export.texture.png-processor",
            DisplayName: "Texture PNG Processor",
            Version: "1.0.0",
            Description: "Processes Texture2D PNG payloads before bundle export.",
            ResourceKinds: ["Texture2D"],
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
                "{resourceKind}",
                "--workspace",
                "{workspaceDataRootPath}"
            ]);
    }
}
