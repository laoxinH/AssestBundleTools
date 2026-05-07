using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载独立资源编辑窗口待恢复提示的页面级状态。
/// </summary>
public sealed class AssetEditorWindowRestorePromptViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private IReadOnlyList<WorkspaceAssetEditorWindowStateDto> _pendingWindows = Array.Empty<WorkspaceAssetEditorWindowStateDto>();
    private IReadOnlySet<string> _potentialConflictAssetKeys = new HashSet<string>(StringComparer.Ordinal);

    public AssetEditorWindowRestorePromptViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public IReadOnlyList<WorkspaceAssetEditorWindowStateDto> PendingWindows => _pendingWindows;

    public bool HasPendingWindows => _pendingWindows.Count > 0;

    public int PendingWindowCount => _pendingWindows.Count;

    public int PendingAssetTabCount => _pendingWindows.Sum(static window => window.OpenAssetTabs.Count);

    public int PendingDraftTabCount => _pendingWindows.Sum(static window =>
        window.OpenAssetTabs.Count(static assetTab => HasDraftReference(assetTab.EditDraftReference)));

    public int PendingConflictAssetTabCount => _pendingWindows.Sum(window =>
        window.OpenAssetTabs.Count(assetTab => IsPotentialConflict(assetTab)));

    public Visibility PendingRestoreVisibility => HasPendingWindows
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string RestoreButtonText => _localizationService.GetFormattedString(
        ResourceKeys.AssetEditorWindowRestorePendingButtonFormat,
        PendingWindowCount);

    public string RestoreToolTip => _localizationService.GetFormattedString(
        ResourceKeys.AssetEditorWindowRestorePendingToolTipFormat,
        PendingWindowCount,
        PendingAssetTabCount);

    public string RestoreDialogDescription => (PendingDraftTabCount, PendingConflictAssetTabCount) switch
    {
        (> 0, > 0) => _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithDraftsAndConflictsFormat,
            PendingWindowCount,
            PendingAssetTabCount,
            PendingDraftTabCount,
            PendingConflictAssetTabCount),
        (> 0, _) => _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithDraftsFormat,
            PendingWindowCount,
            PendingAssetTabCount,
            PendingDraftTabCount),
        (_, > 0) => _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithConflictsFormat,
            PendingWindowCount,
            PendingAssetTabCount,
            PendingConflictAssetTabCount),
        _ => _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionFormat,
            PendingWindowCount,
            PendingAssetTabCount)
    };

    internal void ReplacePotentialConflictAssetKeys(IEnumerable<string>? assetKeys)
    {
        _potentialConflictAssetKeys = assetKeys is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(assetKeys, StringComparer.Ordinal);
        NotifyRestoreStateChanged();
    }

    public void ReplacePendingSnapshots(IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? snapshots)
    {
        _pendingWindows = NormalizeSnapshots(snapshots);
        NotifyRestoreStateChanged();
    }

    public IReadOnlyList<WorkspaceAssetEditorWindowStateDto> TakePendingSnapshots()
    {
        var snapshots = _pendingWindows;
        _pendingWindows = Array.Empty<WorkspaceAssetEditorWindowStateDto>();
        NotifyRestoreStateChanged();
        return snapshots;
    }

    public void Clear()
    {
        if (_pendingWindows.Count == 0)
        {
            return;
        }

        _pendingWindows = Array.Empty<WorkspaceAssetEditorWindowStateDto>();
        NotifyRestoreStateChanged();
    }

    private void NotifyRestoreStateChanged()
    {
        OnPropertyChanged(nameof(PendingWindows));
        OnPropertyChanged(nameof(HasPendingWindows));
        OnPropertyChanged(nameof(PendingWindowCount));
        OnPropertyChanged(nameof(PendingAssetTabCount));
        OnPropertyChanged(nameof(PendingDraftTabCount));
        OnPropertyChanged(nameof(PendingConflictAssetTabCount));
        OnPropertyChanged(nameof(PendingRestoreVisibility));
        OnPropertyChanged(nameof(RestoreButtonText));
        OnPropertyChanged(nameof(RestoreToolTip));
        OnPropertyChanged(nameof(RestoreDialogDescription));
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowStateDto> NormalizeSnapshots(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? snapshots)
    {
        if (snapshots is null || snapshots.Count == 0)
        {
            return Array.Empty<WorkspaceAssetEditorWindowStateDto>();
        }

        var normalizedWindows = new List<WorkspaceAssetEditorWindowStateDto>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            var normalizedTabs = NormalizeAssetTabs(snapshot.OpenAssetTabs);
            if (normalizedTabs.Count == 0)
            {
                continue;
            }

            normalizedWindows.Add(snapshot with { OpenAssetTabs = normalizedTabs });
        }

        return normalizedWindows;
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowAssetTabDto> NormalizeAssetTabs(
        IReadOnlyList<WorkspaceAssetEditorWindowAssetTabDto>? assetTabs)
    {
        if (assetTabs is null || assetTabs.Count == 0)
        {
            return Array.Empty<WorkspaceAssetEditorWindowAssetTabDto>();
        }

        var normalizedTabs = new List<WorkspaceAssetEditorWindowAssetTabDto>(assetTabs.Count);
        var restoredAssetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assetTab in assetTabs)
        {
            if (!AssetDocumentIdentityKey.TryCreate(assetTab.BundleId, assetTab.AssetId, out var assetKey))
            {
                continue;
            }

            if (!restoredAssetKeys.Add(assetKey))
            {
                continue;
            }

            normalizedTabs.Add(assetTab);
        }

        return normalizedTabs;
    }

    private bool IsPotentialConflict(WorkspaceAssetEditorWindowAssetTabDto assetTab) =>
        AssetDocumentIdentityKey.TryCreate(assetTab.BundleId, assetTab.AssetId, out var assetKey) &&
        _potentialConflictAssetKeys.Contains(assetKey);

    private static bool HasDraftReference(WorkspaceAssetEditDraftReferenceDto? draftReference) =>
        draftReference is not null &&
        !string.IsNullOrWhiteSpace(draftReference.DraftId) &&
        !string.IsNullOrWhiteSpace(draftReference.DraftMode);
}
