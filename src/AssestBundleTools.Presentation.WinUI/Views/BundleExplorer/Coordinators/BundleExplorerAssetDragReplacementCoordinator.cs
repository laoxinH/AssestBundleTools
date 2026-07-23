using System.IO;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

/// <summary>
/// 生成行级拖入替换候选、请求确认，并把确认结果交给现有替换链路。
/// </summary>
internal sealed class BundleExplorerAssetDragReplacementCoordinator
{
    private readonly BundleExplorerPage _owner;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private bool _isRunning;

    public BundleExplorerAssetDragReplacementCoordinator(
        BundleExplorerPage owner,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task HandleAsync(object target, IReadOnlyList<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if (_isRunning || _owner.ViewModel.IsBusy)
        {
            ShowWarning(GetString(ResourceKeys.AssetPaneDragReplacementBusy));
            return;
        }

        var paths = sourcePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _isRunning = true;
        try
        {
            switch (target)
            {
                case AssetListEntryViewModel asset:
                    await HandleAssetAsync(asset, paths);
                    break;
                case SpineAssetGroupEntryViewModel spineGroup:
                    await HandleSpineGroupAsync(spineGroup, paths);
                    break;
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task HandleAssetAsync(
        AssetListEntryViewModel target,
        IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            ShowInvalidSourceWarning();
            return;
        }

        var sourceFilePath = sourcePaths[0];
        AppendReceivedLog(target.DisplayName, sourceFilePath);
        var candidate = await _owner.ViewModel.DragReplacementCommands.PrepareAssetAsync(
            target,
            sourceFilePath);
        if (candidate is null || !await ConfirmAsync(
                _localizationService.GetFormattedString(
                    ResourceKeys.AssetPaneDragReplacementAssetConfirmFormat,
                    target.DisplayName,
                    candidate.SourceFileName)))
        {
            return;
        }

        await _owner.ViewModel.DragReplacementCommands.ApplyAssetAsync(target, candidate);
    }

    private async Task HandleSpineGroupAsync(
        SpineAssetGroupEntryViewModel target,
        IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count == 0)
        {
            ShowInvalidSourceWarning();
            return;
        }

        AppendReceivedLog(target.DisplayName, string.Join("; ", sourcePaths));
        var preparation = await _owner.ViewModel.DragReplacementCommands.PrepareSpineGroupAsync(
            target,
            sourcePaths);
        var draft = preparation.Draft;
        if (draft is null)
        {
            if (!preparation.FailureWasReported)
            {
                ShowWarning(_localizationService.GetFormattedString(
                    ResourceKeys.AssetPaneDragReplacementNoSpineMatchesFormat,
                    target.DisplayName));
            }

            return;
        }

        if (!await ConfirmAsync(
                _localizationService.GetFormattedString(
                    ResourceKeys.AssetPaneDragReplacementSpineConfirmFormat,
                    target.DisplayName,
                    draft.ChangedParts.Count,
                    ResolveSourceLabel(sourcePaths))))
        {
            return;
        }

        await _owner.ViewModel.DragReplacementCommands.ApplySpineGroupAsync(draft);
    }

    private static string ResolveSourceLabel(IReadOnlyList<string> sourcePaths) =>
        sourcePaths.Count == 1
            ? Path.GetFileName(sourcePaths[0].Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : string.Join(", ", sourcePaths.Select(Path.GetFileName).Where(static name => !string.IsNullOrWhiteSpace(name)));

    private async Task<bool> ConfirmAsync(string content)
    {
        if (_owner.XamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = _owner.XamlRoot,
            Title = GetString(ResourceKeys.AssetPaneDragReplacementConfirmTitle),
            Content = content,
            PrimaryButtonText = GetString(ResourceKeys.AssetPaneDragReplacementConfirmButton),
            CloseButtonText = GetString(ResourceKeys.CommonCancelButton),
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ShowInvalidSourceWarning() =>
        ShowWarning(GetString(ResourceKeys.AssetPaneDragReplacementInvalidSource));

    private void ShowWarning(string message)
    {
        _appLogService.AppendWarning(message);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationWarningTitle),
            message,
            AppNotificationSeverity.Warning);
    }

    private void AppendReceivedLog(string targetName, string sourcePath) =>
        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.AssetPaneDragReplacementReceivedLogFormat,
            targetName,
            sourcePath));

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}
