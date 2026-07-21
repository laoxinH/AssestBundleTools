using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPanelSizeProjectionTests
{
    [Fact]
    public void Apply_ShouldShowSerializedRecordSizeOnlyWhenProvided()
    {
        var viewModel = new InspectorPanelViewModel(CreateInformationTextSet());

        viewModel.Apply(
            assetName: "voice",
            bundleDisplayName: "audio.bundle",
            assetTypeText: "AudioClip",
            containerPath: string.Empty,
            serializedFileName: string.Empty,
            sizeText: "17.5 KB",
            pathIdText: "1",
            extractCapabilityText: string.Empty,
            fileReplacementCapabilityText: string.Empty,
            workspaceReplacementCapabilityText: string.Empty,
            replacementSourceText: string.Empty,
            replacementStatusText: string.Empty,
            changeSummaryText: string.Empty,
            canExtract: false,
            canSelectFileReplacementSource: false,
            canSelectReplacementSource: false,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: Array.Empty<AssetDependencyViewModel>(),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>(),
            serializedObjectSizeText: "172 B");

        var rows = viewModel.InformationCards.Single(static card => card.Key == "basic").Rows;
        Assert.Contains(rows, static row => row.Label == "Source Size" && row.Value == "17.5 KB");
        Assert.Contains(rows, static row => row.Label == "Record Size" && row.Value == "172 B");
    }

    private static InspectorInformationTextSet CreateInformationTextSet() =>
        new(
            BasicInfoTitle: "Basic",
            PreviewInfoTitle: "Preview",
            PreviewDiagnosticTitle: "Diagnostic",
            AssetNameLabel: "Name",
            AssetTypeLabel: "Type",
            BundleLabel: "Bundle",
            ContainerPathLabel: "Container",
            SerializedFileLabel: "Serialized File",
            AssetSizeLabel: "Source Size",
            PathIdLabel: "PathId",
            PreviewCacheStatusLabel: "Cache",
            PreviewFallbackReasonLabel: "Fallback",
            PreviewDiagnosticDetailLabel: "Detail",
            CopyToolTipText: "Copy",
            ItemCountFormat: "{0}",
            SerializedObjectSizeLabel: "Record Size");
}
