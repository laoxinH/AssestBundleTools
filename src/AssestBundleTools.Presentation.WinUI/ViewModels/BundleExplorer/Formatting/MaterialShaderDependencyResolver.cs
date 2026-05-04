using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MaterialShaderDependencyResolver
{
    private const string ShaderAssetTypeDisplayName = "Shader";

    public static AssetDependencyDto? ResolveShaderDependency(
        AssetPreviewMaterialShaderDto? shader,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (shader?.PathId is not { } pathId || pathId == 0)
        {
            return null;
        }

        return details.Dependencies
            .Where(dependency => dependency.PathId == pathId)
            .OrderBy(static dependency => IsShaderDependency(dependency) ? 0 : 1)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsShaderDependency);
    }

    private static bool IsShaderDependency(AssetDependencyDto dependency)
    {
        return string.Equals(
            dependency.AssetTypeDisplayName,
            ShaderAssetTypeDisplayName,
            StringComparison.OrdinalIgnoreCase);
    }
}
