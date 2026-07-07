namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal static class TextAssetEditLanguageResolver
{
    public static string DetectLanguage(
        string titleText,
        string? assetTypeDisplayName,
        string? textSample = null)
    {
        var extension = Path.GetExtension(titleText);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".skel", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (LooksLikeJson(textSample))
        {
            return "json";
        }

        if (string.Equals(assetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase))
        {
            return "shader";
        }

        if (extension.Equals(".atlas", StringComparison.OrdinalIgnoreCase))
        {
            return "atlas";
        }

        return "plaintext";
    }

    private static bool LooksLikeJson(string? textSample)
    {
        var trimmed = textSample?.TrimStart();
        return !string.IsNullOrEmpty(trimmed) &&
            (trimmed[0] == '{' || trimmed[0] == '[');
    }
}
