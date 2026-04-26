namespace AssestBundleTools.Infrastructure.Settings.Persistence;

/// <summary>
/// 描述应用级偏好设置文件的落盘位置。
/// </summary>
internal sealed class JsonAppPreferencesStoragePaths
{
    public JsonAppPreferencesStoragePaths(string rootDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectoryPath);

        RootDirectoryPath = rootDirectoryPath.Trim();
        PreferencesFilePath = Path.Combine(RootDirectoryPath, "app-preferences.json");
    }

    public string RootDirectoryPath { get; }

    public string PreferencesFilePath { get; }

    public static JsonAppPreferencesStoragePaths CreateDefault()
    {
        var localApplicationDataPath =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new JsonAppPreferencesStoragePaths(
            Path.Combine(localApplicationDataPath, "AssestBundleTools"));
    }
}
