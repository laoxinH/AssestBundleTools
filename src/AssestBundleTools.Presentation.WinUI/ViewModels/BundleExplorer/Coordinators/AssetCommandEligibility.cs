using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class AssetCommandEligibility
{
    public static bool HasAppliedReplacement(AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
               !string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName);
    }
}
