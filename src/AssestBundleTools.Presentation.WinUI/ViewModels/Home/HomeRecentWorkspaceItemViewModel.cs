using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Home;

/// <summary>
/// 工作台“最近工作区”列表项。
/// </summary>
public sealed class HomeRecentWorkspaceItemViewModel
{
    public HomeRecentWorkspaceItemViewModel(
        string workspaceKey,
        string displayName,
        string sourcePath,
        string workspaceDataRootPath,
        string sourceSummary,
        string workspaceDataRootSummary,
        string lastActivatedSummary,
        string primaryBadgeText,
        string? secondaryBadgeText,
        string? availabilitySummary,
        bool isCurrentWorkspace,
        bool isPinned,
        bool canOpen,
        string pinButtonToolTipText)
    {
        WorkspaceKey = workspaceKey;
        DisplayName = displayName;
        SourcePath = sourcePath;
        WorkspaceDataRootPath = workspaceDataRootPath;
        SourceSummary = sourceSummary;
        WorkspaceDataRootSummary = workspaceDataRootSummary;
        LastActivatedSummary = lastActivatedSummary;
        PrimaryBadgeText = primaryBadgeText;
        SecondaryBadgeText = secondaryBadgeText;
        AvailabilitySummary = availabilitySummary ?? string.Empty;
        IsCurrentWorkspace = isCurrentWorkspace;
        IsPinned = isPinned;
        CanOpen = canOpen;
        PinButtonToolTipText = pinButtonToolTipText;
    }

    public string WorkspaceKey { get; }

    public string DisplayName { get; }

    public string SourcePath { get; }

    public string WorkspaceDataRootPath { get; }

    public string SourceSummary { get; }

    public string WorkspaceDataRootSummary { get; }

    public string LastActivatedSummary { get; }

    public string PrimaryBadgeText { get; }

    public string? SecondaryBadgeText { get; }

    public string AvailabilitySummary { get; }

    public bool IsCurrentWorkspace { get; }

    public bool IsPinned { get; }

    public bool CanOpen { get; }

    public string PinButtonToolTipText { get; }

    public string PinButtonGlyph => IsPinned ? "\uEE79" : "\uEE75";

    public Visibility SecondaryBadgeVisibility =>
        string.IsNullOrWhiteSpace(SecondaryBadgeText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AvailabilitySummaryVisibility =>
        string.IsNullOrWhiteSpace(AvailabilitySummary) ? Visibility.Collapsed : Visibility.Visible;
}
