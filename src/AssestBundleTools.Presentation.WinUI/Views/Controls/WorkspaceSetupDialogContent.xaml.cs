using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

/// <summary>
/// 承载工作区新建/打开的集中式可视化配置表单。
/// </summary>
/// <remarks>
/// 新建工作区时，表单用于收集“工作区目录、展示名称、打开方式”这些输入。
/// 打开已有工作区时，表单职责收敛为“选择目录并导入现有工作区”，
/// 工作区名称只负责展示检测结果，不允许在这里修改已有元数据。
/// </remarks>
public sealed partial class WorkspaceSetupDialogContent : UserControl
{
    private readonly IAppInteractionFeedbackService _appInteractionFeedbackService;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;
    private readonly ILocalizationService _localizationService;
    private WorkspaceDialogMode _dialogMode = WorkspaceDialogMode.New;
    private IWorkspaceDataRootService? _workspaceDataRootService;
    private string? _lastSuggestedWorkspaceDisplayName;
    private bool _isApplyingSuggestedWorkspaceDisplayName;
    private bool _isApplyingTextBoxValue;

    public WorkspaceSetupDialogContent()
        : this(
            App.CurrentApp.AppInteractionFeedbackService,
            App.CurrentApp.AssetBundleDecryptorRegistry,
            App.CurrentApp.LocalizationService)
    {
    }

    public WorkspaceSetupDialogContent(
        IAppInteractionFeedbackService appInteractionFeedbackService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        ILocalizationService localizationService)
    {
        _appInteractionFeedbackService = appInteractionFeedbackService
            ?? throw new ArgumentNullException(nameof(appInteractionFeedbackService));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry
            ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
        _localizationService = localizationService
            ?? throw new ArgumentNullException(nameof(localizationService));
        InitializeComponent();
        PopulateDecryptorOptions();
        ApplyLocalizedText();
        CurrentWindowRadioButton.IsChecked = true;
        UpdateOpenTargetCardVisuals();
    }

    public string WorkspaceDisplayName
    {
        get => WorkspaceDisplayNameTextBox.Text.Trim();
        set => SetTextBoxValue(WorkspaceDisplayNameTextBox, value ?? string.Empty);
    }

    public string WorkspaceDataRootPath
    {
        get => DataRootPathTextBox.Text.Trim();
        set => SetTextBoxValue(DataRootPathTextBox, value ?? string.Empty);
    }

    public WorkspaceOpenTarget SelectedOpenTarget =>
        NewWindowRadioButton.IsChecked == true
            ? WorkspaceOpenTarget.NewWindow
            : WorkspaceOpenTarget.CurrentWindow;

    public string? AssetBundleDecryptorId => GetSelectedDecryptorId();

    public void Configure(
        WorkspaceDialogMode dialogMode,
        string defaultWorkspaceDataRootPath,
        IWorkspaceDataRootService workspaceDataRootService)
    {
        _workspaceDataRootService = workspaceDataRootService
            ?? throw new ArgumentNullException(nameof(workspaceDataRootService));
        _dialogMode = dialogMode;

        if (string.IsNullOrWhiteSpace(WorkspaceDataRootPath))
        {
            WorkspaceDataRootPath = defaultWorkspaceDataRootPath;
        }

        DescriptionText.Text = UiText.Get(
            dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupNewDescription
                : ResourceKeys.WorkspaceDialogSetupOpenDescription);

        ConfigureFormMode();
        RefreshWorkspaceDisplayNamePresentation(showInlineValidation: false);
        ClearValidationMessage();
    }

    public void ApplySubmission(WorkspaceSetupSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        WorkspaceDataRootPath = submission.WorkspaceDataRootPath;
        if (_dialogMode == WorkspaceDialogMode.New)
        {
            WorkspaceDisplayName = submission.WorkspaceDisplayName;
            SelectAssetBundleDecryptor(submission.AssetBundleDecryptorId);
            ClearWorkspaceNameValidationMessage();
        }
        else
        {
            RefreshWorkspaceDisplayNamePresentation(showInlineValidation: false);
        }

        if (submission.OpenTarget == WorkspaceOpenTarget.NewWindow)
        {
            NewWindowRadioButton.IsChecked = true;
        }
        else
        {
            CurrentWindowRadioButton.IsChecked = true;
        }

        UpdateOpenTargetCardVisuals();
        ClearValidationMessage();
    }

    /// <summary>
    /// 仅校验表单必填项，并返回原始提交结果。
    /// 后续是否直接创建、打开已有工作区，还是先重置后再创建，
    /// 由上层服务继续判断。
    /// </summary>
    public bool TryCreateSubmission(out WorkspaceSetupSubmission? submission)
    {
        ClearValidationMessage();

        if (string.IsNullOrWhiteSpace(WorkspaceDataRootPath))
        {
            ShowDataRootValidationMessage(UiText.Get(
                _dialogMode == WorkspaceDialogMode.New
                    ? ResourceKeys.WorkspaceDialogSetupValidationDataRootRequired
                    : ResourceKeys.WorkspaceDialogSetupValidationOpenWorkspaceDirectoryRequired));
            submission = null;
            return false;
        }

        if (_dialogMode == WorkspaceDialogMode.New &&
            string.IsNullOrWhiteSpace(WorkspaceDisplayName))
        {
            ShowWorkspaceNameValidationMessage(
                UiText.Get(ResourceKeys.WorkspaceDialogSetupValidationWorkspaceNameRequired),
                announce: true);
            submission = null;
            return false;
        }

        submission = new WorkspaceSetupSubmission(
            DialogMode: _dialogMode,
            WorkspaceDisplayName: WorkspaceDisplayName,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            OpenTarget: SelectedOpenTarget,
            AssetBundleDecryptorId: AssetBundleDecryptorId);

        return true;
    }

    public void ShowDataRootValidationMessage(string validationMessage)
    {
        SetDataRootValidationMessage(
            validationMessage,
            announce: true);
    }

    public void ShowOpenWorkspaceInspectionValidation(WorkspaceDataRootInspectionDto inspection)
    {
        ApplyOpenWorkspaceInspectionState(inspection, showInlineValidation: true, announce: true);
    }

    public void ClearValidationMessage()
    {
        ClearDataRootValidationMessage();
        ClearWorkspaceNameValidationMessage();
    }

    private async void OnBrowseDataRootButtonClick(object sender, RoutedEventArgs e)
    {
        var selectedDirectoryPath = await App.CurrentApp.FilePickerService.PickWorkspaceDataDirectoryAsync();
        if (!string.IsNullOrWhiteSpace(selectedDirectoryPath))
        {
            WorkspaceDataRootPath = selectedDirectoryPath;
        }
    }

    private void OnWorkspaceDisplayNameTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingTextBoxValue)
        {
            return;
        }

        ClearWorkspaceNameValidationMessage();
    }

    private void OnDataRootPathTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingTextBoxValue)
        {
            return;
        }

        ClearDataRootValidationMessage();
        RefreshWorkspaceDisplayNamePresentation(showInlineValidation: _dialogMode == WorkspaceDialogMode.Open);
    }

    private void OnDecryptorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedDecryptorPresentation();
    }

    private void OnOpenTargetChecked(object sender, RoutedEventArgs e)
    {
        UpdateOpenTargetCardVisuals();
        ClearValidationMessage();
    }

    private void ApplyLocalizedText()
    {
        WorkspaceNameFieldLabelText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupWorkspaceNameHeader);
        DecryptorFieldLabelText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupDecryptorHeader);

        BrowseDataRootButtonText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupDataRootBrowseButton);

        OpenTargetSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupOpenTargetSectionTitle);
        OpenTargetSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupOpenTargetSectionHint);
        CurrentWindowOptionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceDialogOpenTargetCurrentWindow);
        CurrentWindowOptionDescriptionText.Text = UiText.Get(ResourceKeys.WorkspaceDialogOpenTargetCurrentWindowDescription);
        NewWindowOptionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceDialogOpenTargetNewWindow);
        NewWindowOptionDescriptionText.Text = UiText.Get(ResourceKeys.WorkspaceDialogOpenTargetNewWindowDescription);
    }

    private void ConfigureFormMode()
    {
        WorkspaceDisplayNameTextBox.IsReadOnly = _dialogMode == WorkspaceDialogMode.Open;
        DecryptorComboBox.IsEnabled = _dialogMode == WorkspaceDialogMode.New;
        RestoreWorkspaceNamePlaceholder();

        DataRootSectionHintText.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootSectionHint
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootSectionHint);
        DataRootPathFieldLabelText.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootPathHeader
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootPathHeader);
        DataRootPathTextBox.PlaceholderText = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootPathPlaceholder
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootPathPlaceholder);
        DecryptorDescriptionTextBlock.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDecryptorHint
                : ResourceKeys.WorkspaceDialogSetupOpenDecryptorHint);

        if (_dialogMode == WorkspaceDialogMode.New)
        {
            SelectAssetBundleDecryptor(GetSelectedDecryptorId());
        }
        else
        {
            SelectAssetBundleDecryptor(null, allowFallback: false);
        }
    }

    /// <summary>
    /// 工作区名称默认优先取“目录内已存在的工作区元数据名称”。
    /// 新建模式下，如果目录还没有工作区，则退回到“当前目录名”；
    /// 打开模式下只展示已检测到的名称，不在此处承担重命名职责。
    /// 一旦用户在新建模式下手动修改过名称，就不再自动覆盖。
    /// </summary>
    private void TryApplySuggestedWorkspaceDisplayName()
    {
        var suggestedWorkspaceDisplayName = ResolveSuggestedWorkspaceDisplayName();

        if (_dialogMode == WorkspaceDialogMode.Open)
        {
            _isApplyingSuggestedWorkspaceDisplayName = true;
            WorkspaceDisplayName = suggestedWorkspaceDisplayName ?? string.Empty;
            _isApplyingSuggestedWorkspaceDisplayName = false;
            _lastSuggestedWorkspaceDisplayName = suggestedWorkspaceDisplayName;
            return;
        }

        if (string.IsNullOrWhiteSpace(suggestedWorkspaceDisplayName) ||
            !CanReplaceWorkspaceDisplayName())
        {
            return;
        }

        _isApplyingSuggestedWorkspaceDisplayName = true;
        WorkspaceDisplayName = suggestedWorkspaceDisplayName;
        _isApplyingSuggestedWorkspaceDisplayName = false;
        _lastSuggestedWorkspaceDisplayName = suggestedWorkspaceDisplayName;
    }

    private bool CanReplaceWorkspaceDisplayName()
    {
        if (_isApplyingSuggestedWorkspaceDisplayName)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(WorkspaceDisplayName) ||
               string.Equals(WorkspaceDisplayName, _lastSuggestedWorkspaceDisplayName, StringComparison.Ordinal);
    }

    private string? ResolveSuggestedWorkspaceDisplayName()
    {
        if (_dialogMode == WorkspaceDialogMode.Open)
        {
            return null;
        }

        var storedWorkspaceDisplayName = TryLoadStoredWorkspaceDisplayName();
        if (!string.IsNullOrWhiteSpace(storedWorkspaceDisplayName))
        {
            return storedWorkspaceDisplayName;
        }

        return BuildWorkspaceDisplayNameFromWorkspaceDirectoryPath(WorkspaceDataRootPath);
    }

    private string? TryLoadStoredWorkspaceDisplayName()
    {
        if (_workspaceDataRootService is null ||
            string.IsNullOrWhiteSpace(WorkspaceDataRootPath))
        {
            return null;
        }

        var inspection = _workspaceDataRootService.Inspect(WorkspaceDataRootPath);
        return inspection.HasMatchedWorkspace &&
               !string.IsNullOrWhiteSpace(inspection.Workspace?.DisplayName)
            ? inspection.Workspace.DisplayName.Trim()
            : null;
    }

    private void UpdateOpenTargetCardVisuals()
    {
        ApplySelectionState(CurrentWindowOptionBorder, CurrentWindowRadioButton.IsChecked == true);
        ApplySelectionState(NewWindowOptionBorder, NewWindowRadioButton.IsChecked == true);
    }

    private static void ApplySelectionState(Border optionBorder, bool isSelected)
    {
        optionBorder.Background = GetBrush(isSelected
            ? "CardBackgroundFillColorDefaultBrush"
            : "CardBackgroundFillColorSecondaryBrush");
        optionBorder.BorderBrush = GetBrush(isSelected
            ? "AccentFillColorDefaultBrush"
            : "CardStrokeColorDefaultBrush");
    }

    private static Brush? GetBrush(string resourceKey)
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as Brush
            : null;
    }

    private void RefreshWorkspaceDisplayNamePresentation(bool showInlineValidation)
    {
        if (_dialogMode == WorkspaceDialogMode.New)
        {
            TryApplySuggestedWorkspaceDisplayName();
            ClearWorkspaceNameValidationMessage();
            return;
        }

        if (_workspaceDataRootService is null ||
            string.IsNullOrWhiteSpace(WorkspaceDataRootPath))
        {
            WorkspaceDisplayName = string.Empty;
            SelectAssetBundleDecryptor(null, allowFallback: false);
            ClearWorkspaceNameValidationMessage();
            return;
        }

        var inspection = _workspaceDataRootService.Inspect(WorkspaceDataRootPath);
        ApplyOpenWorkspaceInspectionState(inspection, showInlineValidation, announce: false);
    }

    private void ApplyOpenWorkspaceInspectionState(
        WorkspaceDataRootInspectionDto inspection,
        bool showInlineValidation,
        bool announce)
    {
        if (_dialogMode != WorkspaceDialogMode.Open)
        {
            return;
        }

        if (inspection.HasMatchedWorkspace &&
            !string.IsNullOrWhiteSpace(inspection.Workspace?.DisplayName))
        {
            WorkspaceDisplayName = inspection.Workspace.DisplayName.Trim();
            SelectAssetBundleDecryptor(inspection.Workspace.AssetBundleDecryptorId, allowFallback: false);
            ClearWorkspaceNameValidationMessage();
            return;
        }

        WorkspaceDisplayName = string.Empty;
        SelectAssetBundleDecryptor(null, allowFallback: false);

        if (!showInlineValidation)
        {
            ClearWorkspaceNameValidationMessage();
            return;
        }

        if (inspection.MatchKind == WorkspaceDataRootMatchKind.AmbiguousMultipleWorkspaces)
        {
            ShowWorkspaceNameValidationMessage(
                UiText.GetFormatted(
                    ResourceKeys.WorkspaceDialogSetupValidationMultipleWorkspacesDetectedInlineFormat,
                    inspection.WorkspaceCount),
                announce,
                ResourceKeys.WorkspaceDialogSetupWorkspaceNameAmbiguousPlaceholder);
            return;
        }

        ShowWorkspaceNameValidationMessage(
            UiText.Get(ResourceKeys.WorkspaceDialogSetupValidationOpenWorkspaceNotFoundInline),
            announce,
            ResourceKeys.WorkspaceDialogSetupWorkspaceNameNotDetectedPlaceholder);
    }

    private void SetTextBoxValue(TextBox textBox, string value)
    {
        if (string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        _isApplyingTextBoxValue = true;
        textBox.Text = value;
        _isApplyingTextBoxValue = false;
    }

    private static string? BuildWorkspaceDisplayNameFromWorkspaceDirectoryPath(string workspaceDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceDirectoryPath))
        {
            return null;
        }

        var trimmedPath = workspaceDirectoryPath.Trim().TrimEnd('\\', '/');
        var lastSlashIndex = trimmedPath.LastIndexOfAny(['\\', '/']);

        return lastSlashIndex >= 0
            ? trimmedPath[(lastSlashIndex + 1)..]
            : trimmedPath;
    }
}
