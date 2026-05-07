namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal static class AssetDocumentIdentityKey
{
    public static bool TryCreate(string? bundleId, string? assetId, out string key)
    {
        if (string.IsNullOrWhiteSpace(bundleId) ||
            string.IsNullOrWhiteSpace(assetId))
        {
            key = string.Empty;
            return false;
        }

        key = Create(bundleId, assetId);
        return true;
    }

    public static string Create(string bundleId, string assetId) =>
        string.Concat(bundleId, "\n", assetId);
}
