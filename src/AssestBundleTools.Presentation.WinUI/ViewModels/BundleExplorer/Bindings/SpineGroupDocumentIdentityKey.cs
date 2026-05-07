namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal static class SpineGroupDocumentIdentityKey
{
    public static bool TryCreate(string? bundleId, string? groupId, out string key)
    {
        if (string.IsNullOrWhiteSpace(bundleId) ||
            string.IsNullOrWhiteSpace(groupId))
        {
            key = string.Empty;
            return false;
        }

        key = Create(bundleId, groupId);
        return true;
    }

    public static string Create(string bundleId, string groupId) =>
        string.Concat(bundleId, "\n", groupId);
}
