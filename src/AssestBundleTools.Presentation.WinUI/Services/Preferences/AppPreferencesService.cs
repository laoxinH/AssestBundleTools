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
    public const long MinimumBundleArtifactCacheMaximumSizeInBytes = 1L * 1024L * 1024L * 1024L;
    public const long DefaultBundleArtifactCacheMaximumSizeInBytes = 4L * 1024L * 1024L * 1024L;
    public const long MaximumBundleArtifactCacheMaximumSizeInBytes = 16L * 1024L * 1024L * 1024L;
    public const int MinimumOperationRecordLimit = 100;
    public const int DefaultOperationRecordLimit = 250;
    public const int MaximumOperationRecordLimit = 1000;
    public const string DefaultAssetEditorCloseBehaviorKey = ReturnToMainWindowCloseBehaviorKey;
    private const string CurrentWindowBehaviorKey = "currentWindow";
    private const string NewWindowBehaviorKey = "newWindow";
    private const string AskEveryTimeBehaviorKey = "askEveryTime";
    private const string ReturnToMainWindowCloseBehaviorKey = "returnToMainWindow";
    private const string DirectCloseBehaviorKey = "directClose";
    private readonly IAppPreferencesPersistenceStore _preferencesStore;
    private readonly IAppDisplayLanguageOverride? _displayLanguageOverride;
    private AppPreferencesPersistenceSnapshot _currentSnapshot;

    internal AppPreferencesService(
        IAppPreferencesPersistenceStore preferencesStore,
        IAppDisplayLanguageOverride? displayLanguageOverride = null)
    {
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _displayLanguageOverride = displayLanguageOverride;
        _currentSnapshot = _preferencesStore.Load();
        _displayLanguageOverride?.Apply(DisplayLanguage);
    }

    public bool RestoreLastWorkspaceOnStartup => _currentSnapshot.RestoreLastWorkspaceOnStartup;

    public RecentWorkspaceOpenBehavior RecentWorkspaceOpenBehavior =>
        DeserializeRecentWorkspaceOpenBehavior(_currentSnapshot.RecentWorkspaceOpenBehavior);

    public AssetEditorCloseBehavior AssetEditorCloseBehavior =>
        DeserializeAssetEditorCloseBehavior(_currentSnapshot.AssetEditorCloseBehavior);

    public AppDisplayLanguage DisplayLanguage =>
        AppDisplayLanguageCodec.Deserialize(_currentSnapshot.DisplayLanguage);

    public AppThemePreference Theme =>
        AppThemePreferenceCodec.Deserialize(_currentSnapshot.Theme);

    public int WorkspaceImportWorkerCount => CoerceWorkspaceImportWorkerCount(_currentSnapshot.WorkspaceImportWorkerCount);

    public int PreviewWorkerCount => CoercePreviewWorkerCount(_currentSnapshot.PreviewWorkerCount);

    public bool BundleArtifactCacheAutoPruneEnabled => _currentSnapshot.BundleArtifactCacheAutoPruneEnabled;

    public long BundleArtifactCacheMaximumSizeInBytes =>
        CoerceBundleArtifactCacheMaximumSizeInBytes(_currentSnapshot.BundleArtifactCacheMaximumSizeInBytes);

    public int OperationRecordLimit => CoerceOperationRecordLimit(_currentSnapshot.OperationRecordLimit);

    public bool ElevatedDragDropCompatibilityEnabled =>
        _currentSnapshot.ElevatedDragDropCompatibilityEnabled;

    public bool UseContentAwareSpineSkeletonFileExtension =>
        _currentSnapshot.UseContentAwareSpineSkeletonFileExtension;

    public bool FirstRunTutorialPromptHandled =>
        _currentSnapshot.FirstRunTutorialPromptHandled;

    public double? MainWindowWidth => CoerceWindowDimension(_currentSnapshot.MainWindowWidth);

    public double? MainWindowHeight => CoerceWindowDimension(_currentSnapshot.MainWindowHeight);

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

        return TryPersistSnapshot(updatedSnapshot);
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

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetAssetEditorCloseBehavior(AssetEditorCloseBehavior value)
    {
        var serializedValue = SerializeAssetEditorCloseBehavior(value);
        if (string.Equals(
                _currentSnapshot.AssetEditorCloseBehavior,
                serializedValue,
                StringComparison.Ordinal))
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            AssetEditorCloseBehavior = serializedValue
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetDisplayLanguage(AppDisplayLanguage value)
    {
        var serializedValue = AppDisplayLanguageCodec.Serialize(value);
        if (string.Equals(
                _currentSnapshot.DisplayLanguage,
                serializedValue,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            DisplayLanguage = serializedValue
        };

        if (!TryPersistSnapshot(updatedSnapshot))
        {
            return false;
        }

        _displayLanguageOverride?.Apply(value);
        return true;
    }

    public bool TrySetTheme(AppThemePreference value)
    {
        var serializedValue = AppThemePreferenceCodec.Serialize(value);
        if (string.Equals(_currentSnapshot.Theme, serializedValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryPersistSnapshot(_currentSnapshot with { Theme = serializedValue });
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

        return TryPersistSnapshot(updatedSnapshot);
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

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetBundleArtifactCacheAutoPruneEnabled(bool value)
    {
        if (_currentSnapshot.BundleArtifactCacheAutoPruneEnabled == value)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            BundleArtifactCacheAutoPruneEnabled = value
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetBundleArtifactCacheMaximumSizeInBytes(long value)
    {
        var coercedValue = CoerceBundleArtifactCacheMaximumSizeInBytes(value);
        if (_currentSnapshot.BundleArtifactCacheMaximumSizeInBytes == coercedValue)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            BundleArtifactCacheMaximumSizeInBytes = coercedValue
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetOperationRecordLimit(int value)
    {
        var coercedValue = CoerceOperationRecordLimit(value);
        if (_currentSnapshot.OperationRecordLimit == coercedValue)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            OperationRecordLimit = coercedValue
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetElevatedDragDropCompatibilityEnabled(bool value)
    {
        if (_currentSnapshot.ElevatedDragDropCompatibilityEnabled == value)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            ElevatedDragDropCompatibilityEnabled = value
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    public bool TrySetUseContentAwareSpineSkeletonFileExtension(bool value)
    {
        if (_currentSnapshot.UseContentAwareSpineSkeletonFileExtension == value)
        {
            return true;
        }

        return TryPersistSnapshot(_currentSnapshot with
        {
            UseContentAwareSpineSkeletonFileExtension = value
        });
    }

    public bool TryMarkFirstRunTutorialPromptHandled()
    {
        if (_currentSnapshot.FirstRunTutorialPromptHandled)
        {
            return true;
        }

        return TryPersistSnapshot(_currentSnapshot with
        {
            FirstRunTutorialPromptHandled = true
        });
    }

    public bool TrySetMainWindowSize(double width, double height)
    {
        var normalizedWidth = CoerceWindowDimension(width);
        var normalizedHeight = CoerceWindowDimension(height);
        if (normalizedWidth is null || normalizedHeight is null)
        {
            return false;
        }

        if (_currentSnapshot.MainWindowWidth == normalizedWidth &&
            _currentSnapshot.MainWindowHeight == normalizedHeight)
        {
            return true;
        }

        var updatedSnapshot = _currentSnapshot with
        {
            MainWindowWidth = normalizedWidth,
            MainWindowHeight = normalizedHeight
        };

        return TryPersistSnapshot(updatedSnapshot);
    }

    private bool TryPersistSnapshot(AppPreferencesPersistenceSnapshot snapshot)
    {
        try
        {
            _preferencesStore.Save(snapshot);
            _currentSnapshot = snapshot;
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

    private static AssetEditorCloseBehavior DeserializeAssetEditorCloseBehavior(string? behaviorKey)
    {
        return behaviorKey switch
        {
            DirectCloseBehaviorKey => AssetEditorCloseBehavior.DirectClose,
            _ => AssetEditorCloseBehavior.ReturnToMainWindow
        };
    }

    private static string SerializeAssetEditorCloseBehavior(AssetEditorCloseBehavior behavior)
    {
        return behavior switch
        {
            AssetEditorCloseBehavior.DirectClose => DirectCloseBehaviorKey,
            _ => ReturnToMainWindowCloseBehaviorKey
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

    public static long CoerceBundleArtifactCacheMaximumSizeInBytes(long value)
    {
        return Math.Clamp(
            value <= 0 ? DefaultBundleArtifactCacheMaximumSizeInBytes : value,
            MinimumBundleArtifactCacheMaximumSizeInBytes,
            MaximumBundleArtifactCacheMaximumSizeInBytes);
    }

    public static int CoerceOperationRecordLimit(int value)
    {
        return Math.Clamp(
            value <= 0 ? DefaultOperationRecordLimit : value,
            MinimumOperationRecordLimit,
            MaximumOperationRecordLimit);
    }

    private static double? CoerceWindowDimension(double? value) =>
        value is > 0d && double.IsFinite(value.Value)
            ? value
            : null;
}
