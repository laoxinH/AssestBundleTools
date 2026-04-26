using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 统一构建工作区目录项的展示状态，避免多个页面各自维护一套徽标优先级规则。
/// </summary>
public static class WorkspaceEntryPresentationBuilder
{
    public static WorkspaceEntryPresentationDescriptor Build(
        WorkspaceContextEntry entry,
        string? currentWorkspaceId,
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(localizationService);

        var isCurrentWorkspace = !string.IsNullOrWhiteSpace(currentWorkspaceId) &&
                                 string.Equals(
                                     entry.WorkspaceSummary?.WorkspaceId.Value,
                                     currentWorkspaceId,
                                     StringComparison.Ordinal);

        return new WorkspaceEntryPresentationDescriptor(
            PrimaryBadgeText: ResolvePrimaryBadgeText(entry, isCurrentWorkspace, localizationService),
            SecondaryBadgeText: ResolveSecondaryBadgeText(entry, isCurrentWorkspace, localizationService),
            AvailabilitySummary: BuildAvailabilitySummary(entry, localizationService),
            CanOpen: entry.IsAvailable);
    }

    private static string ResolvePrimaryBadgeText(
        WorkspaceContextEntry entry,
        bool isCurrentWorkspace,
        ILocalizationService localizationService)
    {
        if (isCurrentWorkspace)
        {
            return localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspaceCurrentBadge);
        }

        if (entry.HasLiveSession)
        {
            return localizationService.GetRequiredString(ResourceKeys.SettingsDiagnosticsWorkspaceBadgeLiveSession);
        }

        if (entry.IsPinned)
        {
            return localizationService.GetRequiredString(ResourceKeys.WorkspaceBadgePinned);
        }

        return localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspaceRegisteredBadge);
    }

    private static string? ResolveSecondaryBadgeText(
        WorkspaceContextEntry entry,
        bool isCurrentWorkspace,
        ILocalizationService localizationService)
    {
        if (!entry.IsAvailable)
        {
            return localizationService.GetRequiredString(ResourceKeys.WorkspaceBadgeUnavailable);
        }

        if ((isCurrentWorkspace || entry.HasLiveSession) && entry.IsPinned)
        {
            return localizationService.GetRequiredString(ResourceKeys.WorkspaceBadgePinned);
        }

        return null;
    }

    private static string? BuildAvailabilitySummary(
        WorkspaceContextEntry entry,
        ILocalizationService localizationService)
    {
        return entry.AvailabilityStatus switch
        {
            WorkspaceAvailabilityStatus.MissingSourceDirectory => localizationService.GetFormattedString(
                ResourceKeys.WorkspaceAvailabilityMissingSourceFormat,
                string.IsNullOrWhiteSpace(entry.SourcePath)
                    ? localizationService.GetRequiredString(ResourceKeys.WorkspaceSourceNotImported)
                    : entry.SourcePath),
            WorkspaceAvailabilityStatus.MissingWorkspaceDataRootDirectory => localizationService.GetFormattedString(
                ResourceKeys.WorkspaceAvailabilityMissingDataRootFormat,
                string.IsNullOrWhiteSpace(entry.WorkspaceDataRootPath)
                    ? localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspaceDataRootNotSpecified)
                    : entry.WorkspaceDataRootPath),
            _ => null
        };
    }
}
