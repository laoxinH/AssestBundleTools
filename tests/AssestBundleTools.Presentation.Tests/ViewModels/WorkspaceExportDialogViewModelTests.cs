using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceExportDialogViewModelTests
{
    [Fact]
    public void ResourceProcessorOptions_ShouldDisplayManifestProcessors_WithoutGenericPlaceholderName()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: null,
            resourceProcessorCatalogEntries:
            [
                CreateProcessor(
                    id: "export.texture.png-processor",
                    displayName: "Texture PNG Processor",
                    description: "Processes Texture2D PNG payloads before bundle export.",
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: false),
                CreateProcessor(
                    id: "resource.processor.texture2d.cli",
                    displayName: "resource.processor.texture2d.cli",
                    description: string.Empty,
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: true)
            ]);

        var textureSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "texture2d");
        var processorNames = textureSlot.Processors.Select(static processor => processor.DisplayName).ToArray();

        Assert.Contains("Texture PNG Processor", processorNames);
        Assert.Contains(ResourceKeys.WorkspaceExportDialogProcessorNone, processorNames);
        Assert.DoesNotContain("WorkspaceExportDialog.ProcessorCliPlaceholder", processorNames);
        Assert.Contains(
            ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat + ":" + ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D,
            processorNames);

        var manifestProcessor = textureSlot.Processors.Single(static processor => processor.Id == "export.texture.png-processor");
        Assert.Equal("Processes Texture2D PNG payloads before bundle export.", manifestProcessor.Description);
    }

    private static ResourceProcessorCatalogEntryDto CreateProcessor(
        string id,
        string displayName,
        string description,
        IReadOnlyList<string> resourceKinds,
        bool isBuiltIn)
    {
        return new ResourceProcessorCatalogEntryDto(
            id,
            displayName,
            Version: "1.0.0",
            description,
            ResourceProcessorKind.ExternalProcess,
            resourceKinds,
            ResourceProcessorStatus.Available,
            IsBuiltIn: isBuiltIn,
            IsReadOnly: isBuiltIn,
            ManifestFilePath: isBuiltIn ? null : $@"C:\Plugins\{id}.json",
            ExecutablePath: isBuiltIn ? null : $@"C:\Tools\{id}.exe",
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments: ["--input", "{inputPath}"]);
    }
}
