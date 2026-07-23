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

    [Fact]
    public void TrySetTheme_ShouldPersistStablePreferenceKey()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        var succeeded = service.TrySetTheme(AppThemePreference.Dark);

        Assert.True(succeeded);
        Assert.Equal(AppThemePreference.Dark, service.Theme);
        Assert.Equal("dark", store.SavedSnapshot?.Theme);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public void Constructor_ShouldCoerceUnknownThemeToSystemDefault()
    {
        var store = new TestAppPreferencesPersistenceStore(
            CreateSnapshot("system") with { Theme = "unsupported" });

        var service = new AppPreferencesService(store);

        Assert.Equal(AppThemePreference.SystemDefault, service.Theme);
    }

    [Fact]
    public void TrySetElevatedDragDropCompatibilityEnabled_ShouldPersistValue()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        var succeeded = service.TrySetElevatedDragDropCompatibilityEnabled(true);

        Assert.True(succeeded);
        Assert.True(service.ElevatedDragDropCompatibilityEnabled);
        Assert.True(store.SavedSnapshot?.ElevatedDragDropCompatibilityEnabled);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public void TrySetUseContentAwareSpineSkeletonFileExtension_ShouldPersistValue()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        Assert.False(service.UseContentAwareSpineSkeletonFileExtension);
        Assert.True(service.TrySetUseContentAwareSpineSkeletonFileExtension(true));
        Assert.True(service.UseContentAwareSpineSkeletonFileExtension);
        Assert.True(store.SavedSnapshot?.UseContentAwareSpineSkeletonFileExtension);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public void TryMarkFirstRunTutorialPromptHandled_ShouldPersistOnce()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        Assert.False(service.FirstRunTutorialPromptHandled);
        Assert.True(service.TryMarkFirstRunTutorialPromptHandled());
        Assert.True(service.FirstRunTutorialPromptHandled);
        Assert.True(store.SavedSnapshot?.FirstRunTutorialPromptHandled);
        Assert.True(service.TryMarkFirstRunTutorialPromptHandled());
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public void TrySetMainWindowSize_ShouldPersistEffectiveDimensions()
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        var succeeded = service.TrySetMainWindowSize(1180.5d, 720.25d);

        Assert.True(succeeded);
        Assert.Equal(1180.5d, service.MainWindowWidth);
        Assert.Equal(720.25d, service.MainWindowHeight);
        Assert.Equal(1180.5d, store.SavedSnapshot?.MainWindowWidth);
        Assert.Equal(720.25d, store.SavedSnapshot?.MainWindowHeight);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Theory]
    [InlineData(0d, 720d)]
    [InlineData(1180d, -1d)]
    [InlineData(double.NaN, 720d)]
    public void TrySetMainWindowSize_ShouldRejectInvalidDimensions(double width, double height)
    {
        var store = new TestAppPreferencesPersistenceStore(CreateSnapshot("system"));
        var service = new AppPreferencesService(store);

        var succeeded = service.TrySetMainWindowSize(width, height);

        Assert.False(succeeded);
        Assert.Equal(0, store.SaveCallCount);
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
