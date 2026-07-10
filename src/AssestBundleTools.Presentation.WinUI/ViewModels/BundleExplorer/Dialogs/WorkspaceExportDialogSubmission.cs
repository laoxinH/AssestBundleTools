using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed record WorkspaceExportDialogSubmission(
    bool ExportToWorkspaceDirectory,
    IReadOnlyList<BundleId> SelectedBundleIds,
    IReadOnlyList<WorkspaceExportProcessorSelectionDto> ProcessorSelections,
    WorkspaceBundleExportCompressionModeDto CompressionMode,
    WorkspaceBundleExportOutputConflictPolicyDto OutputConflictPolicy,
    WorkspaceBundleExportAudioWriteModeDto AudioWriteMode,
    bool UseSourceCodec,
    WorkspaceExportSettingsSnapshotDto ExportSettings);
