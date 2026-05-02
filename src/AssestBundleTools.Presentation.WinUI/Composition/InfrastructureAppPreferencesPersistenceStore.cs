using AssestBundleTools.Infrastructure.Settings.Persistence;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 将 Infrastructure 的 JSON 偏好存储适配为 Presentation 自己的偏好持久化抽象。
/// </summary>
internal sealed class InfrastructureAppPreferencesPersistenceStore : IAppPreferencesPersistenceStore
{
    private readonly JsonAppPreferencesStore _store;

    public InfrastructureAppPreferencesPersistenceStore(JsonAppPreferencesStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AppPreferencesPersistenceSnapshot Load()
    {
        var snapshot = _store.Load();
        return new AppPreferencesPersistenceSnapshot(
            snapshot.RestoreLastWorkspaceOnStartup,
            snapshot.RecentWorkspaceOpenBehavior,
            snapshot.WorkspaceImportWorkerCount);
    }

    public void Save(AppPreferencesPersistenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _store.Save(new AppPreferencesSnapshot(
            snapshot.RestoreLastWorkspaceOnStartup,
            snapshot.RecentWorkspaceOpenBehavior,
            snapshot.WorkspaceImportWorkerCount));
    }
}
