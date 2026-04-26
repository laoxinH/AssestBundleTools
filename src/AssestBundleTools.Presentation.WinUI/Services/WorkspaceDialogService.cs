using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 使用 WinUI 对话框收集工作区管理所需的用户输入，并协调后续目录决策。
/// </summary>
public sealed class WorkspaceDialogService : IWorkspaceDialogService
{
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceDataRootService _workspaceDataRootService;
    private readonly IAppInteractionFeedbackService _appInteractionFeedbackService;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;

    public WorkspaceDialogService(
        ILocalizationService localizationService,
        IWorkspaceDataRootService workspaceDataRootService,
        IAppInteractionFeedbackService appInteractionFeedbackService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _workspaceDataRootService = workspaceDataRootService ?? throw new ArgumentNullException(nameof(workspaceDataRootService));
        _appInteractionFeedbackService = appInteractionFeedbackService ?? throw new ArgumentNullException(nameof(appInteractionFeedbackService));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
    }

    public Task<WorkspaceNavigationRequest?> RequestNewWorkspaceAsync(
        XamlRoot xamlRoot,
        CancellationToken cancellationToken = default)
    {
        return RequestWorkspaceSelectionAsync(xamlRoot, WorkspaceDialogMode.New, cancellationToken);
    }

    public Task<WorkspaceNavigationRequest?> RequestOpenWorkspaceFromDirectoryAsync(
        XamlRoot xamlRoot,
        CancellationToken cancellationToken = default)
    {
        return RequestWorkspaceSelectionAsync(xamlRoot, WorkspaceDialogMode.Open, cancellationToken);
    }

    public async Task<WorkspaceOpenTarget?> RequestWorkspaceOpenTargetAsync(
        XamlRoot xamlRoot,
        string workspaceDisplayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentWindowText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogOpenTargetCurrentWindow);
        var newWindowText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogOpenTargetNewWindow);
        var selectionControl = new RadioButtons
        {
            SelectedIndex = 0,
            ItemsSource = new[]
            {
                currentWindowText,
                newWindowText
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogOpenTargetTitle),
            PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogOpenTargetPrimaryButton),
            CloseButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogOpenTargetCloseButton),
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = _localizationService.GetFormattedString(
                            ResourceKeys.WorkspaceDialogOpenTargetDescriptionFormat,
                            workspaceDisplayName),
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    selectionControl
                }
            }
        };

        var result = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return selectionControl.SelectedIndex == 1
            ? WorkspaceOpenTarget.NewWindow
            : WorkspaceOpenTarget.CurrentWindow;
    }

    public async Task<bool> ConfirmDeleteWorkspaceAsync(
        XamlRoot xamlRoot,
        string workspaceDisplayName,
        string workspaceDataRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogDeleteTitle),
            PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogDeletePrimaryButton),
            CloseButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogDeleteCloseButton),
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceDialogDeleteDescriptionFormat,
                    workspaceDisplayName,
                    workspaceDataRootPath),
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

        var result = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result == ContentDialogResult.Primary;
    }

    private async Task<WorkspaceNavigationRequest?> RequestWorkspaceSelectionAsync(
        XamlRoot xamlRoot,
        WorkspaceDialogMode dialogMode,
        CancellationToken cancellationToken)
    {
        WorkspaceSetupSubmission? lastSubmission = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = new WorkspaceSetupDialogContent(
                _appInteractionFeedbackService,
                _assetBundleDecryptorRegistry,
                _localizationService);
            content.Configure(
                dialogMode,
                _workspaceDataRootService.GetDefaultRootDirectoryPath(),
                _workspaceDataRootService);

            if (lastSubmission is not null)
            {
                content.ApplySubmission(lastSubmission);
            }

            WorkspaceSetupSubmission? submittedForm = null;
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = _localizationService.GetRequiredString(
                    dialogMode == WorkspaceDialogMode.New
                        ? ResourceKeys.WorkspaceDialogSetupNewTitle
                        : ResourceKeys.WorkspaceDialogSetupOpenTitle),
                PrimaryButtonText = _localizationService.GetRequiredString(
                    dialogMode == WorkspaceDialogMode.New
                        ? ResourceKeys.WorkspaceDialogSetupNewPrimaryButton
                        : ResourceKeys.WorkspaceDialogSetupOpenPrimaryButton),
                CloseButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogSetupCloseButton),
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                content.ClearValidationMessage();

                if (!content.TryCreateSubmission(out var submission) ||
                    submission is null)
                {
                    args.Cancel = true;
                    return;
                }

                var inspection = _workspaceDataRootService.Inspect(submission.WorkspaceDataRootPath);
                if (inspection.MatchKind == WorkspaceDataRootMatchKind.AmbiguousMultipleWorkspaces)
                {
                    if (dialogMode == WorkspaceDialogMode.Open)
                    {
                        content.ShowOpenWorkspaceInspectionValidation(inspection);
                    }
                    else
                    {
                        content.ShowDataRootValidationMessage(_localizationService.GetFormattedString(
                            ResourceKeys.WorkspaceDialogSetupValidationMultipleWorkspacesDetectedFormat,
                            inspection.WorkspaceCount));
                    }

                    args.Cancel = true;
                    return;
                }

                if (dialogMode == WorkspaceDialogMode.Open &&
                    !inspection.HasMatchedWorkspace)
                {
                    content.ShowOpenWorkspaceInspectionValidation(inspection);
                    args.Cancel = true;
                    return;
                }

                submittedForm = submission;
            };

            var result = await dialog.ShowAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (result != ContentDialogResult.Primary ||
                submittedForm is null)
            {
                return null;
            }

            lastSubmission = submittedForm;
            var inspection = _workspaceDataRootService.Inspect(submittedForm.WorkspaceDataRootPath);
            if (dialogMode == WorkspaceDialogMode.Open)
            {
                if (inspection.HasMatchedWorkspace && inspection.Workspace is not null)
                {
                    return BuildNavigationRequestFromExistingWorkspace(inspection.Workspace, submittedForm);
                }

                continue;
            }

            var navigationRequest = await ResolveNewWorkspaceNavigationRequestAsync(
                xamlRoot,
                submittedForm,
                inspection,
                cancellationToken);
            if (navigationRequest is not null)
            {
                return navigationRequest;
            }
        }
    }

    private async Task<WorkspaceNavigationRequest?> ResolveNewWorkspaceNavigationRequestAsync(
        XamlRoot xamlRoot,
        WorkspaceSetupSubmission submission,
        WorkspaceDataRootInspectionDto inspection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!inspection.HasMatchedWorkspace || inspection.Workspace is null)
        {
            return BuildNewWorkspaceNavigationRequest(submission);
        }

        var action = await RequestExistingWorkspaceActionAsync(
            xamlRoot,
            inspection.Workspace,
            submission.WorkspaceDataRootPath,
            cancellationToken);
        if (action is null)
        {
            return null;
        }

        if (action == ExistingWorkspaceAction.Reset)
        {
            _workspaceDataRootService.ResetWorkspace(
                submission.WorkspaceDataRootPath,
                inspection.Workspace.WorkspaceKey);
            return BuildNewWorkspaceNavigationRequest(submission);
        }

        return BuildNavigationRequestFromExistingWorkspace(inspection.Workspace, submission);
    }

    private async Task<ExistingWorkspaceAction?> RequestExistingWorkspaceActionAsync(
        XamlRoot xamlRoot,
        WorkspaceDataRootWorkspaceDto workspace,
        string workspaceDataRootPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogExistingWorkspaceTitle),
            PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogExistingWorkspaceOpenButton),
            SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogExistingWorkspaceResetButton),
            CloseButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceDialogExistingWorkspaceCloseButton),
            DefaultButton = ContentDialogButton.Primary,
            Content = new TextBlock
            {
                Text = _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceDialogExistingWorkspaceDescriptionFormat,
                    workspace.DisplayName,
                    workspaceDataRootPath),
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

        var result = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return result switch
        {
            ContentDialogResult.Primary => ExistingWorkspaceAction.Open,
            ContentDialogResult.Secondary => ExistingWorkspaceAction.Reset,
            _ => null
        };
    }

    private static WorkspaceNavigationRequest BuildNewWorkspaceNavigationRequest(WorkspaceSetupSubmission submission)
    {
        return new WorkspaceNavigationRequest(
            SourcePath: string.Empty,
            WorkspaceDisplayName: submission.WorkspaceDisplayName,
            WorkspaceDataRootPath: submission.WorkspaceDataRootPath,
            OpenTarget: submission.OpenTarget,
            AssetBundleDecryptorId: submission.AssetBundleDecryptorId);
    }

    private static WorkspaceNavigationRequest BuildNavigationRequestFromExistingWorkspace(
        WorkspaceDataRootWorkspaceDto workspace,
        WorkspaceSetupSubmission submission)
    {
        return new WorkspaceNavigationRequest(
            SourcePath: workspace.SourcePath,
            WorkspaceDisplayName: workspace.DisplayName,
            WorkspaceDataRootPath: submission.WorkspaceDataRootPath,
            OpenTarget: submission.OpenTarget,
            AssetBundleDecryptorId: workspace.AssetBundleDecryptorId);
    }

    private enum ExistingWorkspaceAction
    {
        Open,
        Reset
    }
}
