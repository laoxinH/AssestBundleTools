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
            false,
            loadedSnapshot.ElevatedDragDropCompatibilityEnabled,
            "Legacy preferences should keep elevated drag-drop compatibility disabled.");
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
}
