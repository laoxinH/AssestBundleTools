namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示中间资产窗格当前展示的列表形态。
/// </summary>
public enum AssetPaneViewMode
{
    Assets = 0,
    SpineGroups = 1
}

internal static class AssetPaneViewModePersistence
{
    public const string AssetsKey = "Assets";
    public const string SpineGroupsKey = "SpineGroups";

    public static string ToPersistenceKey(AssetPaneViewMode viewMode) =>
        viewMode == AssetPaneViewMode.SpineGroups
            ? SpineGroupsKey
            : AssetsKey;

    public static AssetPaneViewMode FromPersistenceKey(string? value) =>
        string.Equals(value, SpineGroupsKey, StringComparison.OrdinalIgnoreCase)
            ? AssetPaneViewMode.SpineGroups
            : AssetPaneViewMode.Assets;
}
