using AssestBundleTools.Infrastructure.Settings.Persistence;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Settings.Persistence;

internal static class JsonAppPreferencesStoreTests
{
    public static void Save_ShouldRoundTripDisplayLanguage()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);
        var snapshot = AppPreferencesSnapshot.Default with
        {
            DisplayLanguage = "en-US"
        };

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal(
            "en-US",
            loadedSnapshot.DisplayLanguage,
            "Display language should round-trip through the app preferences store.");
    }

    public static void Save_ShouldRoundTripTheme()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);
        var snapshot = AppPreferencesSnapshot.Default with { Theme = "dark" };

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal("dark", loadedSnapshot.Theme, "Theme should round-trip through app preferences.");
    }

    public static void Load_ShouldUseSystemLanguage_WhenLegacySnapshotOmitsDisplayLanguage()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        File.WriteAllText(
            Path.Combine(temporaryDirectory.DirectoryPath, "app-preferences.json"),
            """
            {
              "restoreLastWorkspaceOnStartup": true,
              "recentWorkspaceOpenBehavior": "currentWindow",
              "workspaceImportWorkerCount": 0
            }
            """);
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);

        var loadedSnapshot = store.Load();

        TestAssert.Equal(
            AppPreferencesSnapshot.DefaultDisplayLanguage,
            loadedSnapshot.DisplayLanguage,
            "Legacy preferences should default the display language to the system setting.");
        TestAssert.Equal(
            AppPreferencesSnapshot.DefaultTheme,
            loadedSnapshot.Theme,
            "Legacy preferences should default the theme to the system setting.");
        TestAssert.Equal(
            false,
            loadedSnapshot.ElevatedDragDropCompatibilityEnabled,
            "Legacy preferences should keep elevated drag-drop compatibility disabled.");
        TestAssert.Equal(
            false,
            loadedSnapshot.FirstRunTutorialPromptHandled,
            "Legacy preferences should leave the first-run tutorial prompt pending.");
    }

    public static void Save_ShouldRoundTripElevatedDragDropCompatibility()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);
        var snapshot = AppPreferencesSnapshot.Default with
        {
            ElevatedDragDropCompatibilityEnabled = true
        };

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal(
            true,
            loadedSnapshot.ElevatedDragDropCompatibilityEnabled,
            "Elevated drag-drop compatibility should round-trip through the preferences store.");
    }

    public static void Save_ShouldRoundTripMainWindowSize()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);
        var snapshot = AppPreferencesSnapshot.Default with
        {
            MainWindowWidth = 1180.5d,
            MainWindowHeight = 720.25d
        };

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal(1180.5d, loadedSnapshot.MainWindowWidth, "Main window width should round-trip.");
        TestAssert.Equal(720.25d, loadedSnapshot.MainWindowHeight, "Main window height should round-trip.");
    }

    public static void Save_ShouldRoundTripFirstRunTutorialPromptState()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonAppPreferencesStore(temporaryDirectory.DirectoryPath);
        var snapshot = AppPreferencesSnapshot.Default with
        {
            FirstRunTutorialPromptHandled = true
        };

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal(
            true,
            loadedSnapshot.FirstRunTutorialPromptHandled,
            "First-run tutorial prompt state should round-trip through app preferences.");
    }
}
