using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class AppPreferencesServiceTests
{
    [Fact]
    public void Constructor_ShouldLoadAndApplyPersistedDisplayLanguage()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("en-US"));
        var languageOverride = new TestAppDisplayLanguageOverride();

        var service = new AppPreferencesService(store, languageOverride);

        Assert.Equal(AppDisplayLanguage.English, service.DisplayLanguage);
        Assert.Equal([AppDisplayLanguage.English], languageOverride.AppliedLanguages);
    }

    [Fact]
    public void TrySetDisplayLanguage_ShouldPersistAndApplyLanguageOverride()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var languageOverride = new TestAppDisplayLanguageOverride();
        var service = new AppPreferencesService(store, languageOverride);
        languageOverride.AppliedLanguages.Clear();

        var succeeded = service.TrySetDisplayLanguage(AppDisplayLanguage.SimplifiedChinese);

        Assert.True(succeeded);
        Assert.Equal(AppDisplayLanguage.SimplifiedChinese, service.DisplayLanguage);
        Assert.Equal("zh-CN", store.SavedSnapshot?.DisplayLanguage);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal([AppDisplayLanguage.SimplifiedChinese], languageOverride.AppliedLanguages);
    }

    [Fact]
    public void Constructor_ShouldCoerceUnknownDisplayLanguageToSystemDefault()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("fr-FR"));
        var languageOverride = new TestAppDisplayLanguageOverride();

        var service = new AppPreferencesService(store, languageOverride);

        Assert.Equal(AppDisplayLanguage.SystemDefault, service.DisplayLanguage);
        Assert.Equal([AppDisplayLanguage.SystemDefault], languageOverride.AppliedLanguages);
    }

    private static AppPreferencesPersistenceSnapshot CreateSnapshot(string displayLanguage)
    {
        return new AppPreferencesPersistenceSnapshot(
            RestoreLastWorkspaceOnStartup: true,
            RecentWorkspaceOpenBehavior: "currentWindow",
            AssetEditorCloseBehavior: "returnToMainWindow",
            WorkspaceImportWorkerCount: 0,
            PreviewWorkerCount: 0,
            DisplayLanguage: displayLanguage);
    }

    private sealed class TestAppPreferencesPersistenceStore(
        AppPreferencesPersistenceSnapshot initialSnapshot) : IAppPreferencesPersistenceStore
    {
        public int SaveCallCount { get; private set; }

        public AppPreferencesPersistenceSnapshot? SavedSnapshot { get; private set; }

        public AppPreferencesPersistenceSnapshot Load() => initialSnapshot;

        public void Save(AppPreferencesPersistenceSnapshot snapshot)
        {
            SaveCallCount++;
            SavedSnapshot = snapshot;
        }
    }

    private sealed class TestAppDisplayLanguageOverride : IAppDisplayLanguageOverride
    {
        public List<AppDisplayLanguage> AppliedLanguages { get; } = [];

        public void Apply(AppDisplayLanguage language)
        {
            AppliedLanguages.Add(language);
        }
    }
}
