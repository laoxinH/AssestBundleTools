using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed record WorkspaceBatchExtractionDialogSubmission(
    bool UseWorkspaceExtractionDirectory,
    IReadOnlyList<BundleId> SelectedBundleIds,
    IReadOnlyList<WorkspaceBatchExtractionCategoryDto> SelectedCategories);
