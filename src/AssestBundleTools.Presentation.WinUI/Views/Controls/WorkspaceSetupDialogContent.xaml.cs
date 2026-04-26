using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI;
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
        UpdateSummary();
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
        UpdateSummary();
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
        UpdateSummary();
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

        UpdateSummary();
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
        UpdateSummary();
    }

    private void OnDecryptorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedDecryptorPresentation();
        UpdateSummary();
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

        SummarySectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupSummarySectionTitle);
        SummaryWorkspaceNameLabelText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupSummaryWorkspaceNameLabel);
        SummaryDecryptorLabelText.Text = UiText.Get(ResourceKeys.WorkspaceDialogSetupSummaryDecryptorLabel);
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
        DataRootSectionTitleText.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootSectionTitle
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootSectionTitle);
        DataRootPathFieldLabelText.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootPathHeader
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootPathHeader);
        DataRootPathTextBox.PlaceholderText = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupDataRootPathPlaceholder
                : ResourceKeys.WorkspaceDialogSetupOpenDataRootPathPlaceholder);
        SummaryDataRootPathLabelText.Text = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupSummaryDataRootPathLabel
                : ResourceKeys.WorkspaceDialogSetupOpenSummaryDataRootPathLabel);
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

    private void UpdateSummary()
    {
        var pendingValue = UiText.Get(ResourceKeys.WorkspaceDialogSetupSummaryPendingValue);
        SummaryWorkspaceNameValueText.Text = string.IsNullOrWhiteSpace(WorkspaceDisplayName)
            ? pendingValue
            : WorkspaceDisplayName;
        SummaryDecryptorValueText.Text = ResolveSelectedDecryptorDisplayName() ?? pendingValue;
        SummaryDataRootPathValueText.Text = string.IsNullOrWhiteSpace(WorkspaceDataRootPath)
            ? pendingValue
            : WorkspaceDataRootPath;
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
            UpdateSummary();
            return;
        }

        WorkspaceDisplayName = string.Empty;
        SelectAssetBundleDecryptor(null, allowFallback: false);
        UpdateSummary();

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

    private void SetDataRootValidationMessage(string validationMessage, bool announce)
    {
        ApplyFieldValidationState(DataRootPathTextBox, DataRootValidationTextBlock, validationMessage);

        if (!announce)
        {
            return;
        }

        FocusValidationTarget(DataRootPathTextBox);
        PlayValidationFeedback();
    }

    private void ShowWorkspaceNameValidationMessage(
        string validationMessage,
        bool announce,
        string? placeholderResourceKey = null)
    {
        ApplyFieldValidationState(WorkspaceDisplayNameTextBox, WorkspaceNameValidationTextBlock, validationMessage);

        if (!string.IsNullOrWhiteSpace(placeholderResourceKey))
        {
            WorkspaceDisplayNameTextBox.PlaceholderText = UiText.Get(placeholderResourceKey);
        }

        if (!announce)
        {
            return;
        }

        FocusValidationTarget(_dialogMode == WorkspaceDialogMode.Open
            ? DataRootPathTextBox
            : WorkspaceDisplayNameTextBox);
        PlayValidationFeedback();
    }

    private void ClearDataRootValidationMessage()
    {
        ApplyFieldValidationState(DataRootPathTextBox, DataRootValidationTextBlock, null);
    }

    private void ClearWorkspaceNameValidationMessage()
    {
        ApplyFieldValidationState(WorkspaceDisplayNameTextBox, WorkspaceNameValidationTextBlock, null);
        RestoreWorkspaceNamePlaceholder();
    }

    private void ApplyFieldValidationState(
        TextBox textBox,
        TextBlock validationTextBlock,
        string? validationMessage)
    {
        var hasValidationError = !string.IsNullOrWhiteSpace(validationMessage);
        validationTextBlock.Text = validationMessage ?? string.Empty;
        validationTextBlock.Visibility = hasValidationError
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyTextBoxValidationStyle(textBox, hasValidationError);
    }

    private static void ApplyTextBoxValidationStyle(TextBox textBox, bool hasValidationError)
    {
        textBox.BorderBrush = hasValidationError
            ? GetBrush("SystemFillColorCriticalBrush") ?? new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28))
            : GetBrush("ControlStrokeColorDefaultBrush");

        UpdateTextBoxResource(textBox, "TextControlBorderBrush", hasValidationError
            ? textBox.BorderBrush
            : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushFocused", hasValidationError
            ? textBox.BorderBrush
            : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushPointerOver", hasValidationError
            ? textBox.BorderBrush
            : null);
    }

    private static void UpdateTextBoxResource(TextBox textBox, string resourceKey, object? resourceValue)
    {
        if (resourceValue is null)
        {
            if (textBox.Resources.ContainsKey(resourceKey))
            {
                textBox.Resources.Remove(resourceKey);
            }

            return;
        }

        textBox.Resources[resourceKey] = resourceValue;
    }

    private void RestoreWorkspaceNamePlaceholder()
    {
        WorkspaceDisplayNameTextBox.PlaceholderText = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupWorkspaceNamePlaceholder
                : ResourceKeys.WorkspaceDialogSetupWorkspaceNameReadOnlyPlaceholder);
    }

    private void FocusValidationTarget(Control focusTarget)
    {
        FormScrollViewer.ChangeView(null, 0d, null, disableAnimation: false);
        _ = focusTarget.Focus(FocusState.Programmatic);
    }

    private void PlayValidationFeedback() =>
        _appInteractionFeedbackService.Play(AppInteractionFeedbackKind.ValidationError);

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

    private void PopulateDecryptorOptions()
    {
        DecryptorComboBox.Items.Clear();

        // “自动识别”不是注册表里的真实解密器实现，而是工作区导入阶段的一种解析策略。
        // 这里把它作为首选项显式展示出来，避免用户把“未选择”误解成“默认直通”。
        DecryptorComboBox.Items.Add(CreateDecryptorComboBoxItem(
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            AssetBundleDecryptorDisplayText.GetDisplayName(_localizationService, _assetBundleDecryptorRegistry, AssetBundleDecryptorIds.BuiltInAutoDetect),
            AssetBundleDecryptorDisplayText.GetDescription(_localizationService, _assetBundleDecryptorRegistry, AssetBundleDecryptorIds.BuiltInAutoDetect)));

        foreach (var decryptor in _assetBundleDecryptorRegistry.GetAll()
                     .OrderByDescending(static decryptor => decryptor.Descriptor.IsFallback)
                     .ThenBy(static decryptor => decryptor.Descriptor.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            DecryptorComboBox.Items.Add(CreateDecryptorComboBoxItem(
                decryptor.Descriptor.Id,
                AssetBundleDecryptorDisplayText.GetDisplayName(_localizationService, decryptor.Descriptor),
                AssetBundleDecryptorDisplayText.GetDescription(_localizationService, decryptor.Descriptor)));
        }
    }

    private void SelectAssetBundleDecryptor(string? decryptorId, bool allowFallback = true)
    {
        ComboBoxItem? matchedItem = null;

        if (!string.IsNullOrWhiteSpace(decryptorId))
        {
            matchedItem = FindDecryptorComboBoxItem(decryptorId);
            matchedItem ??= EnsureDecryptorComboBoxItem(
                decryptorId.Trim(),
                decryptorId.Trim(),
                decryptorId.Trim());
        }
        else
        {
            matchedItem = FindAutoDetectDecryptorComboBoxItem();
            if (matchedItem is null && allowFallback)
            {
                matchedItem = FindFallbackDecryptorComboBoxItem()
                              ?? DecryptorComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
            }
        }

        if (ReferenceEquals(DecryptorComboBox.SelectedItem, matchedItem))
        {
            RefreshSelectedDecryptorPresentation();
            return;
        }

        DecryptorComboBox.SelectedItem = matchedItem;
        RefreshSelectedDecryptorPresentation();
    }

    private void RefreshSelectedDecryptorPresentation()
    {
        var selectedItem = DecryptorComboBox.SelectedItem as ComboBoxItem;
        var selectedOption = selectedItem?.Tag as WorkspaceSetupDecryptorOption;
        if (selectedOption is null)
        {
            DecryptorDescriptionTextBlock.Text = UiText.Get(
                _dialogMode == WorkspaceDialogMode.New
                    ? ResourceKeys.WorkspaceDialogSetupDecryptorHint
                    : ResourceKeys.WorkspaceDialogSetupOpenDecryptorHint);
            return;
        }

        DecryptorDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(selectedOption.Description)
            ? selectedItem?.Content?.ToString() ?? string.Empty
            : selectedOption.Description;
    }

    private string? ResolveSelectedDecryptorDisplayName()
    {
        return (DecryptorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
    }

    private string? GetSelectedDecryptorId()
    {
        return (DecryptorComboBox.SelectedItem as ComboBoxItem)?.Tag is WorkspaceSetupDecryptorOption option
            ? option.Id
            : null;
    }

    private ComboBoxItem? FindFallbackDecryptorComboBoxItem()
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(static item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                           string.Equals(option.Id, AssetBundleDecryptorIds.BuiltInPassThrough, StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem? FindAutoDetectDecryptorComboBoxItem()
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(static item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                           string.Equals(option.Id, AssetBundleDecryptorIds.BuiltInAutoDetect, StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem? FindDecryptorComboBoxItem(string decryptorId)
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                    string.Equals(option.Id, decryptorId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem EnsureDecryptorComboBoxItem(
        string decryptorId,
        string displayName,
        string? description)
    {
        var existingItem = FindDecryptorComboBoxItem(decryptorId);
        if (existingItem is not null)
        {
            return existingItem;
        }

        var createdItem = CreateDecryptorComboBoxItem(decryptorId, displayName, description);
        DecryptorComboBox.Items.Add(createdItem);
        return createdItem;
    }

    private static ComboBoxItem CreateDecryptorComboBoxItem(
        string decryptorId,
        string displayName,
        string? description)
    {
        return new ComboBoxItem
        {
            Content = displayName,
            Tag = new WorkspaceSetupDecryptorOption(decryptorId, description)
        };
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

file sealed record WorkspaceSetupDecryptorOption(string Id, string? Description);
