namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal static class TextAssetEditLanguageResolver
{
    public static string DetectLanguage(string titleText, string? assetTypeDisplayName)
    {
        var extension = Path.GetExtension(titleText);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".skel", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (extension.Equals(".atlas", StringComparison.OrdinalIgnoreCase))
        {
            return "atlas";
        }

        if (string.Equals(assetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase))
        {
            return "shader";
        }

        return "plaintext";
    }
}
