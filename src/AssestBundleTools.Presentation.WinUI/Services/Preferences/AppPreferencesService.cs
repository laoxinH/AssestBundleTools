namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 管理当前应用会话中的偏好设置快照，并负责把变更持久化到本地。
/// </summary>
public sealed class AppPreferencesService : IAppPreferencesService
{
    public const int AutomaticWorkspaceImportWorkerCount = 0;
    public const int MinimumManualWorkspaceImportWorkerCount = 1;
    public const int MaximumManualWorkspaceImportWorkerCount = 4;
    public const int AutomaticPreviewWorkerCount = 0;
    public const int MinimumManualPreviewWorkerCount = 1;
    public const int MaximumManualPreviewWorkerCount = 4;
    private const string CurrentWindowBehaviorKey = "currentWindow";
    private const string NewWindowBehaviorKey = "newWindow";
    private const string AskEveryTimeBehaviorKey = "askEveryTime";
    private readonly IAppPreferencesPersistenceStore _preferencesStore;
    private AppPreferencesPersistenceSnapshot _currentSnapshot;

    internal AppPreferencesService(IAppPreferencesPersistenceStore preferencesStore)
    {
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _currentSnapshot = _preferencesStore.Load();
    }

    public bool RestoreLastWorkspaceOnStartup => _currentSnapshot.RestoreLastWorkspaceOnStartup;

    public RecentWorkspaceOpenBehavior RecentWorkspaceOpenBehavior =>
        DeserializeRecentWorkspaceOpenBehavior(_currentSnapshot.RecentWorkspaceOpenBehavior);

    public int WorkspaceImportWorkerCount => CoerceWorkspaceImportWorkerCount(_currentSnapshot.WorkspaceImportWorkerCount);

    public int PreviewWorkerCount => CoercePreviewWorkerCount(_currentSnapshot.PreviewWorkerCount);

    public bool TrySetRestoreLastWorkspaceOnStartup(bool value)
    {
        if (_currentSnapshot.RestoreLastWorkspaceOnStartup == value)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            RestoreLastWorkspaceOnStartup = value
        };

        try
        {
            _preferencesStore.Save(updatedSnapshot);
            _currentSnapshot = updatedSnapshot;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TrySetRecentWorkspaceOpenBehavior(RecentWorkspaceOpenBehavior value)
    {
        var serializedValue = SerializeRecentWorkspaceOpenBehavior(value);
        if (string.Equals(
                _currentSnapshot.RecentWorkspaceOpenBehavior,
                serializedValue,
                StringComparison.Ordinal))
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            RecentWorkspaceOpenBehavior = serializedValue
        };

        try
        {
            _preferencesStore.Save(updatedSnapshot);
            _currentSnapshot = updatedSnapshot;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TrySetWorkspaceImportWorkerCount(int value)
    {
        var coercedValue = CoerceWorkspaceImportWorkerCount(value);
        if (_currentSnapshot.WorkspaceImportWorkerCount == coercedValue)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            WorkspaceImportWorkerCount = coercedValue
        };

        try
        {
            _preferencesStore.Save(updatedSnapshot);
            _currentSnapshot = updatedSnapshot;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TrySetPreviewWorkerCount(int value)
    {
        var coercedValue = CoercePreviewWorkerCount(value);
        if (_currentSnapshot.PreviewWorkerCount == coercedValue)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            PreviewWorkerCount = coercedValue
        };

        try
        {
            _preferencesStore.Save(updatedSnapshot);
            _currentSnapshot = updatedSnapshot;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static RecentWorkspaceOpenBehavior DeserializeRecentWorkspaceOpenBehavior(string? behaviorKey)
    {
        return behaviorKey switch
        {
            NewWindowBehaviorKey => RecentWorkspaceOpenBehavior.NewWindow,
            AskEveryTimeBehaviorKey => RecentWorkspaceOpenBehavior.AskEveryTime,
            _ => RecentWorkspaceOpenBehavior.CurrentWindow
        };
    }

    private static string SerializeRecentWorkspaceOpenBehavior(RecentWorkspaceOpenBehavior behavior)
    {
        return behavior switch
        {
            RecentWorkspaceOpenBehavior.NewWindow => NewWindowBehaviorKey,
            RecentWorkspaceOpenBehavior.AskEveryTime => AskEveryTimeBehaviorKey,
            _ => CurrentWindowBehaviorKey
        };
    }

    public static int CoerceWorkspaceImportWorkerCount(int value)
    {
        if (value == AutomaticWorkspaceImportWorkerCount)
        {
            return AutomaticWorkspaceImportWorkerCount;
        }

        return Math.Clamp(
            value,
            MinimumManualWorkspaceImportWorkerCount,
            MaximumManualWorkspaceImportWorkerCount);
    }

    public static int CoercePreviewWorkerCount(int value)
    {
        if (value == AutomaticPreviewWorkerCount)
        {
            return AutomaticPreviewWorkerCount;
        }

        return Math.Clamp(
            value,
            MinimumManualPreviewWorkerCount,
            MaximumManualPreviewWorkerCount);
    }
}
