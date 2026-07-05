namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class TextDocumentWebViewEnvironmentProvider
{
    private const string WebViewUserDataFolderEnvironmentVariable = "WEBVIEW2_USER_DATA_FOLDER";
    private static readonly object UserDataFolderGate = new();
    private static bool _userDataFolderConfigured;

    public static string ResolveMonacoAssetRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Monaco");
    }

    public static void EnsureUserDataFolderConfigured()
    {
        if (_userDataFolderConfigured)
        {
            return;
        }

        lock (UserDataFolderGate)
        {
            if (_userDataFolderConfigured)
            {
                return;
            }

            var userDataFolder = ResolveUserDataFolder();
            Directory.CreateDirectory(userDataFolder);
            Environment.SetEnvironmentVariable(
                WebViewUserDataFolderEnvironmentVariable,
                userDataFolder,
                EnvironmentVariableTarget.Process);
            _userDataFolderConfigured = true;
        }
    }

    private static string ResolveUserDataFolder()
    {
        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            localApplicationDataPath = AppContext.BaseDirectory;
        }

        return Path.Combine(localApplicationDataPath, "AssestBundleTools", "WebView2", "TextEditor");
    }
}
