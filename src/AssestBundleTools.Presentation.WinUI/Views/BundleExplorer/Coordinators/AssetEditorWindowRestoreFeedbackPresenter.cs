using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestoreFeedbackPresenter
{
    public static void Publish(
        int restoredWindowCount,
        int unavailableWindowCount,
        int conflictedWindowCount,
        int conflictedAssetTabCount)
    {
        if (restoredWindowCount == 0 &&
            unavailableWindowCount == 0 &&
            conflictedWindowCount == 0)
        {
            return;
        }

        var localizationService = App.CurrentApp.LocalizationService;
        var notificationService = App.CurrentApp.AppNotificationService;
        var logService = App.CurrentApp.AppLogService;

        string message;
        string title;
        AppNotificationSeverity severity;
        if (restoredWindowCount > 0 &&
            unavailableWindowCount == 0 &&
            conflictedWindowCount == 0)
        {
            message = localizationService.GetFormattedString(
                ResourceKeys.AssetEditorWindowRestorePendingRestoredFormat,
                restoredWindowCount);
            title = localizationService.GetRequiredString(ResourceKeys.AppNotificationSuccessTitle);
            severity = AppNotificationSeverity.Success;
            logService.AppendInfo(message);
        }
        else
        {
            message = CreateWarningMessage(
                localizationService,
                restoredWindowCount,
                unavailableWindowCount,
                conflictedWindowCount,
                conflictedAssetTabCount);
            title = localizationService.GetRequiredString(ResourceKeys.AppNotificationWarningTitle);
            severity = AppNotificationSeverity.Warning;
            logService.AppendWarning(message);
        }

        notificationService.Show(title, message, severity);
    }

    private static string CreateWarningMessage(
        ILocalizationService localizationService,
        int restoredWindowCount,
        int unavailableWindowCount,
        int conflictedWindowCount,
        int conflictedAssetTabCount)
    {
        if (conflictedWindowCount > 0)
        {
            var remainingWindowCount = unavailableWindowCount + conflictedWindowCount;
            return restoredWindowCount > 0
                ? localizationService.GetFormattedString(
                    ResourceKeys.AssetEditorWindowRestorePendingPartialWithConflictsFormat,
                    restoredWindowCount,
                    remainingWindowCount,
                    conflictedAssetTabCount)
                : localizationService.GetFormattedString(
                    ResourceKeys.AssetEditorWindowRestorePendingBlockedWithConflictsFormat,
                    remainingWindowCount,
                    conflictedAssetTabCount);
        }

        return restoredWindowCount > 0
            ? localizationService.GetFormattedString(
                ResourceKeys.AssetEditorWindowRestorePendingPartialFormat,
                restoredWindowCount,
                unavailableWindowCount)
            : localizationService.GetFormattedString(
                ResourceKeys.AssetEditorWindowRestorePendingUnavailableFormat,
                unavailableWindowCount);
    }
}
