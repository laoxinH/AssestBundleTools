using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
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
        if (sourcePaths.Count == 0)
        {
            ShowInvalidSourceWarning();
            return;
        }

        AppendReceivedLog(target.DisplayName, string.Join("; ", sourcePaths));
        var pane = _owner.ViewModel.AssetWorkflow.PrimaryPane;
        var preparation = await _owner.ViewModel.DragReplacementCommands.PrepareAssetsAsync(
            target,
            pane.Items,
            pane.SpineGroupItems,
            sourcePaths);
        var plan = preparation.Plan;
        if (plan is null)
        {
            if (!preparation.FailureWasReported)
            {
                ShowInvalidSourceWarning();
            }

            return;
        }

        if (plan.HasConflicts)
        {
            await ShowConflictsAsync(plan.Conflicts);
            return;
        }

        if (plan.Matches.Count == 0)
        {
            ShowWarning(GetString(ResourceKeys.AssetPaneDragReplacementNoAssetMatches));
            return;
        }

        var confirmation = plan.Matches.Count == 1
            ? _localizationService.GetFormattedString(
                ResourceKeys.AssetPaneDragReplacementAssetConfirmFormat,
                ResolveTargetName(plan.Matches[0].Target),
                plan.Matches[0].SourceFileName)
            : _localizationService.GetFormattedString(
                ResourceKeys.AssetPaneDragReplacementAssetsConfirmFormat,
                plan.Matches.Count);
        if (!await ConfirmAsync(confirmation))
        {
            return;
        }

        await _owner.ViewModel.DragReplacementCommands.ApplyAssetsAsync(plan.Matches);
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

    private async Task ShowConflictsAsync(IReadOnlyList<AssetReplacementSourceConflictDto> conflicts)
    {
        var details = BuildConflictDetails(conflicts);
        _appLogService.AppendWarning(details);
        if (_owner.XamlRoot is null)
        {
            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationWarningTitle),
                details,
                AppNotificationSeverity.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = _owner.XamlRoot,
            Title = GetString(ResourceKeys.AssetPaneDragReplacementConflictTitle),
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new TextBlock
                {
                    Text = details,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            CloseButtonText = GetString(ResourceKeys.AssetPaneDragReplacementConflictCloseButton),
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }

    private string BuildConflictDetails(IReadOnlyList<AssetReplacementSourceConflictDto> conflicts)
    {
        var builder = new StringBuilder(GetString(ResourceKeys.AssetPaneDragReplacementConflictIntro));
        foreach (var conflict in conflicts)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(_localizationService.GetFormattedString(
                ResourceKeys.AssetPaneDragReplacementConflictTargetFormat,
                ResolveTargetName(conflict.Target)));
            foreach (var path in conflict.SourceFilePaths)
            {
                builder.AppendLine();
                builder.Append("  - ");
                builder.Append(path);
            }
        }

        return builder.ToString();
    }

    private static string ResolveTargetName(AssetListItemDto target) =>
        string.IsNullOrWhiteSpace(target.DisplayName) ? target.Name : target.DisplayName;

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
