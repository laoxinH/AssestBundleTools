using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceExportDialogViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly WorkspaceExportFailureDisplayFormatter _failureDisplayFormatter;
    private readonly WorkspaceExportDialogOptionFactory _optionFactory;
    private string _outputDirectoryText = string.Empty;
    private string _planSummaryText = string.Empty;
    private string _validationText = string.Empty;
    private string _primaryButtonText = string.Empty;
    private string _secondaryButtonText = string.Empty;
    private string _executionProgressText = string.Empty;
    private bool _exportToWorkspaceDirectory;
    private bool _overwriteExistingFiles;
    private bool _forceAudioPayloadWrite;
    private bool _isLoading;
    private bool _isExecuting;
    private bool _isExecutionCancellationRequested;
    private bool _isExecutionProgressIndeterminate;
    private double _executionProgressValue;
    private Visibility _executionProgressVisibility = Visibility.Collapsed;
    private WorkspaceExportCompressionOptionViewModel? _selectedCompressionOption;

    public WorkspaceExportDialogViewModel(
        ILocalizationService localizationService,
        WorkspaceChangeSummaryDto? changeSummary,
        WorkspaceSavePreviewDto? savePreview,
        IReadOnlyList<ResourceProcessorCatalogEntryDto>? resourceProcessorCatalogEntries = null,
        WorkspaceExportSettingsSnapshotDto? exportSettings = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _failureDisplayFormatter = new WorkspaceExportFailureDisplayFormatter(_localizationService);
        _optionFactory = new WorkspaceExportDialogOptionFactory(_localizationService);
        _outputDirectoryText = savePreview?.OutputDirectory ?? string.Empty;
        _primaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        _secondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);

        Bundles = new ObservableCollection<WorkspaceExportBundleOptionViewModel>(
            _optionFactory.CreateBundleOptions(savePreview, changeSummary));
        VisibleBundles = new ObservableCollection<WorkspaceExportBundleListItemViewModel>();
        BundleSortOptions = new ObservableCollection<WorkspaceExportBundleSortOptionViewModel>(
            _optionFactory.CreateBundleSortOptions());
        ResourceProcessors = new ObservableCollection<WorkspaceExportResourceProcessorOptionViewModel>(
            _optionFactory.CreateResourceProcessorOptions(resourceProcessorCatalogEntries ?? Array.Empty<ResourceProcessorCatalogEntryDto>()));
        CompressionOptions = new ObservableCollection<WorkspaceExportCompressionOptionViewModel>(
            _optionFactory.CreateCompressionOptions());
        _selectedCompressionOption = CompressionOptions.FirstOrDefault(static option =>
            option.Mode == WorkspaceBundleExportCompressionModeDto.PreserveOriginal);
        _selectedBundleSortOption = BundleSortOptions.FirstOrDefault();
        ApplyExportSettings(exportSettings);
        foreach (var bundle in Bundles)
        {
            bundle.SelectionChanged += OnBundleSelectionChanged;
        }

        RefreshVisibleBundles();
        RefreshPlanSummary();
    }

    public ObservableCollection<WorkspaceExportBundleOptionViewModel> Bundles { get; }

    public ObservableCollection<WorkspaceExportBundleListItemViewModel> VisibleBundles { get; }

    public ObservableCollection<WorkspaceExportBundleSortOptionViewModel> BundleSortOptions { get; }

    public ObservableCollection<WorkspaceExportResourceProcessorOptionViewModel> ResourceProcessors { get; }

    public ObservableCollection<WorkspaceExportCompressionOptionViewModel> CompressionOptions { get; }

    public string OutputDirectoryText
    {
        get => _outputDirectoryText;
        private set => SetProperty(ref _outputDirectoryText, value);
    }

    public string PlanSummaryText
    {
        get => _planSummaryText;
        private set => SetProperty(ref _planSummaryText, value);
    }

    public string ValidationText
    {
        get => _validationText;
        private set => SetProperty(ref _validationText, value);
    }

    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        private set => SetProperty(ref _primaryButtonText, value);
    }

    public string SecondaryButtonText
    {
        get => _secondaryButtonText;
        private set => SetProperty(ref _secondaryButtonText, value);
    }

    public string ExecutionProgressText
    {
        get => _executionProgressText;
        private set => SetProperty(ref _executionProgressText, value);
    }

    public double ExecutionProgressValue
    {
        get => _executionProgressValue;
        private set => SetProperty(ref _executionProgressValue, value);
    }

    public bool IsExecutionProgressIndeterminate
    {
        get => _isExecutionProgressIndeterminate;
        private set => SetProperty(ref _isExecutionProgressIndeterminate, value);
    }

    public Visibility ExecutionProgressVisibility
    {
        get => _executionProgressVisibility;
        private set => SetProperty(ref _executionProgressVisibility, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (SetProperty(ref _isExecuting, value))
            {
                OnPropertyChanged(nameof(CanStartExport));
                OnPropertyChanged(nameof(CanUseSecondaryButton));
            }
        }
    }

    public bool IsExecutionCancellationRequested
    {
        get => _isExecutionCancellationRequested;
        private set
        {
            if (SetProperty(ref _isExecutionCancellationRequested, value))
            {
                OnPropertyChanged(nameof(CanUseSecondaryButton));
            }
        }
    }

    public bool ExportToWorkspaceDirectory
    {
        get => _exportToWorkspaceDirectory;
        set => SetProperty(ref _exportToWorkspaceDirectory, value);
    }

    public bool OverwriteExistingFiles
    {
        get => _overwriteExistingFiles;
        set => SetProperty(ref _overwriteExistingFiles, value);
    }

    public bool ForceAudioPayloadWrite
    {
        get => _forceAudioPayloadWrite;
        set
        {
            if (SetProperty(ref _forceAudioPayloadWrite, value))
            {
                OnPropertyChanged(nameof(ForceAudioWarningText));
                OnPropertyChanged(nameof(ForceAudioWarningVisibility));
            }
        }
    }

    public string ForceAudioWarningText =>
        ForceAudioPayloadWrite
            ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogForceAudioPayloadWriteWarning)
            : string.Empty;

    public Visibility ForceAudioWarningVisibility =>
        ForceAudioPayloadWrite ? Visibility.Visible : Visibility.Collapsed;

    public bool CanUseSecondaryButton => !IsExecuting || !IsExecutionCancellationRequested;

    public void MarkExportExecutionDeferred()
    {
        IsExecuting = false;
        IsExecutionCancellationRequested = false;
        HideExecutionProgress();
        ValidationText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogExecutionDeferred);
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);
    }

    public WorkspaceExportCompressionOptionViewModel? SelectedCompressionOption
    {
        get => _selectedCompressionOption;
        set => SetProperty(ref _selectedCompressionOption, value);
    }

    public void SetExecuting(int totalCount)
    {
        IsExecuting = true;
        IsExecutionCancellationRequested = false;
        ValidationText = string.Empty;
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);
        ApplyExecutionProgress(
            completedCount: 0,
            totalCount: Math.Max(1, totalCount),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogStatusPreparing));
    }

    public void ApplyExecutionProgress(int completedCount, int totalCount, string? detail)
    {
        var safeTotalCount = Math.Max(1, totalCount);
        var safeCompletedCount = Math.Clamp(completedCount, 0, safeTotalCount);
        ExecutionProgressText = string.IsNullOrWhiteSpace(detail)
            ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogStatusPreparing)
            : detail.Trim();
        ExecutionProgressValue = safeCompletedCount * 100d / safeTotalCount;
        IsExecutionProgressIndeterminate = false;
        ExecutionProgressVisibility = Visibility.Visible;
    }

    public void ApplyExecutionCancellationRequested()
    {
        if (!IsExecuting)
        {
            return;
        }

        IsExecutionCancellationRequested = true;
        ValidationText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogExecutionCancellationRequested);
    }

    public void ApplySelectedOutputDirectory(string outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            OutputDirectoryText = outputDirectory.Trim();
        }
    }

    public void ApplyExecutionCancelled()
    {
        IsExecuting = false;
        IsExecutionCancellationRequested = false;
        HideExecutionProgress();
        ValidationText = string.Empty;
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);
    }

    public void ApplyExecutionUnavailable()
    {
        IsExecuting = false;
        IsExecutionCancellationRequested = false;
        HideExecutionProgress();
        ValidationText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogExecutionFailed);
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);
    }

    public void ApplyExecutionResult(WorkspaceSaveExecutionResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IsExecuting = false;
        IsExecutionCancellationRequested = false;
        HideExecutionProgress();
        OutputDirectoryText = result.OutputDirectory;
        var failureDetail = BuildFailureDetail(result);
        ValidationText = string.IsNullOrWhiteSpace(failureDetail)
            ? _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogExecutionCompletedFormat,
                result.WrittenBundleCount,
                result.FailedBundleCount)
            : _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogExecutionCompletedWithDiagnosticsFormat,
                result.WrittenBundleCount,
                result.FailedBundleCount,
                failureDetail);
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
        SecondaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCloseButton);
    }

    public WorkspaceExportDialogSubmission CreateSubmission()
    {
        var selectedBundleIds = Bundles
            .Where(static bundle => bundle.IsSelected)
            .Select(static bundle => bundle.BundleId)
            .ToArray();
        var processorSelections = ResourceProcessors
            .Select(processor => _optionFactory.CreateProcessorSelection(processor))
            .ToArray();
        var compressionMode = SelectedCompressionOption?.Mode ?? WorkspaceBundleExportCompressionModeDto.PreserveOriginal;
        var audioWriteMode = ForceAudioPayloadWrite
            ? WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer
            : WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer;
        var exportSettings = new WorkspaceExportSettingsSnapshotDto(
            ExportToWorkspaceDirectory,
            OverwriteExistingFiles,
            compressionMode,
            audioWriteMode,
            ResourceProcessors
                .Select(processor => _optionFactory.CreateProcessorSettingSnapshot(processor))
                .ToArray());

        return new WorkspaceExportDialogSubmission(
            ExportToWorkspaceDirectory,
            selectedBundleIds,
            processorSelections,
            compressionMode,
            OverwriteExistingFiles
                ? WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting
                : WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists,
            audioWriteMode,
            exportSettings);
    }

    private void ApplyExportSettings(WorkspaceExportSettingsSnapshotDto? exportSettings)
    {
        if (exportSettings is null)
        {
            return;
        }

        ExportToWorkspaceDirectory = exportSettings.ExportToWorkspaceDirectory;
        OverwriteExistingFiles = exportSettings.OverwriteExistingFiles;
        ForceAudioPayloadWrite = exportSettings.AudioWriteMode == WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer;
        SelectedCompressionOption = CompressionOptions.FirstOrDefault(option => option.Mode == exportSettings.CompressionMode)
                                    ?? CompressionOptions.FirstOrDefault(static option =>
                                        option.Mode == WorkspaceBundleExportCompressionModeDto.PreserveOriginal);
        ApplyProcessorSettings(exportSettings.ProcessorSettings ?? Array.Empty<WorkspaceExportProcessorSettingSnapshotDto>());
    }

    private void ApplyProcessorSettings(IReadOnlyList<WorkspaceExportProcessorSettingSnapshotDto> processorSettings)
    {
        if (processorSettings.Count == 0)
        {
            return;
        }

        var settingLookup = processorSettings
            .Where(static setting => !string.IsNullOrWhiteSpace(setting.ResourceKind))
            .GroupBy(static setting => NormalizeResourceKind(setting.ResourceKind))
            .ToDictionary(static group => group.Key, static group => group.Last());
        foreach (var slot in ResourceProcessors)
        {
            if (!settingLookup.TryGetValue(NormalizeResourceKind(slot.Id), out var setting))
            {
                continue;
            }

            slot.IsEnabled = setting.IsEnabled;
            slot.SelectedProcessor = string.IsNullOrWhiteSpace(setting.ProcessorId)
                ? slot.Processors.FirstOrDefault()
                : slot.Processors.FirstOrDefault(processor =>
                    string.Equals(processor.Id, setting.ProcessorId, StringComparison.OrdinalIgnoreCase))
                  ?? slot.Processors.FirstOrDefault();
        }
    }

    private static string NormalizeResourceKind(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private void HideExecutionProgress()
    {
        ExecutionProgressText = string.Empty;
        ExecutionProgressValue = 0d;
        IsExecutionProgressIndeterminate = false;
        ExecutionProgressVisibility = Visibility.Collapsed;
    }

    private string? BuildFailureDetail(WorkspaceSaveExecutionResultDto result)
    {
        var failedBundle = result.BundleResults.FirstOrDefault(static bundle =>
            bundle.Status == WorkspaceSaveBundleExecutionStatusDto.Failed);
        if (failedBundle is null)
        {
            return null;
        }

        return _failureDisplayFormatter.Format(failedBundle);
    }
}
