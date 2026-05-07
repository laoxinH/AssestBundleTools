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

    public string RestoreDialogDescription => PendingDraftTabCount > 0
        ? _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithDraftsFormat,
            PendingWindowCount,
            PendingAssetTabCount,
            PendingDraftTabCount)
        : _localizationService.GetFormattedString(
            ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionFormat,
            PendingWindowCount,
            PendingAssetTabCount);

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
            if (string.IsNullOrWhiteSpace(assetTab.BundleId) ||
                string.IsNullOrWhiteSpace(assetTab.AssetId))
            {
                continue;
            }

            if (!restoredAssetKeys.Add($"{assetTab.BundleId}\n{assetTab.AssetId}"))
            {
                continue;
            }

            normalizedTabs.Add(assetTab);
        }

        return normalizedTabs;
    }

    private static bool HasDraftReference(WorkspaceAssetEditDraftReferenceDto? draftReference) =>
        draftReference is not null &&
        !string.IsNullOrWhiteSpace(draftReference.DraftId) &&
        !string.IsNullOrWhiteSpace(draftReference.DraftMode);
}
