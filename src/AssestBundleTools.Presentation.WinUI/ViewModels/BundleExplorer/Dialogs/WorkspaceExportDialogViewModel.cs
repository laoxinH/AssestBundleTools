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
    private string _bundleSearchText = string.Empty;
    private bool _exportToWorkspaceDirectory = true;
    private bool _overwriteExistingFiles = true;
    private bool _isLoading;
    private bool _isExecuting;
    private WorkspaceExportBundleSortOptionViewModel? _selectedBundleSortOption;
    private WorkspaceExportCompressionOptionViewModel? _selectedCompressionOption;

    public WorkspaceExportDialogViewModel(
        ILocalizationService localizationService,
        WorkspaceChangeSummaryDto? changeSummary,
        WorkspaceSavePreviewDto? savePreview,
        IReadOnlyList<ResourceProcessorCatalogEntryDto>? resourceProcessorCatalogEntries = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _failureDisplayFormatter = new WorkspaceExportFailureDisplayFormatter(_localizationService);
        _optionFactory = new WorkspaceExportDialogOptionFactory(_localizationService);
        _outputDirectoryText = savePreview?.OutputDirectory ?? string.Empty;
        _primaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);

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

    public string BundleSearchText
    {
        get => _bundleSearchText;
        set
        {
            if (SetProperty(ref _bundleSearchText, value ?? string.Empty))
            {
                RefreshVisibleBundles();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set => SetProperty(ref _isExecuting, value);
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

    public WorkspaceExportBundleSortOptionViewModel? SelectedBundleSortOption
    {
        get => _selectedBundleSortOption;
        set
        {
            if (SetProperty(ref _selectedBundleSortOption, value))
            {
                RefreshVisibleBundles();
            }
        }
    }

    public bool HasExportTargets => Bundles.Any(static bundle => bundle.IsSelected);

    public void SelectAllBundles()
    {
        foreach (var bundle in VisibleBundles.Select(static item => item.Bundle).OfType<WorkspaceExportBundleOptionViewModel>())
        {
            bundle.IsSelected = true;
        }
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in VisibleBundles.Select(static item => item.Bundle).OfType<WorkspaceExportBundleOptionViewModel>())
        {
            bundle.IsSelected = false;
        }
    }

    public void MarkExportExecutionDeferred()
    {
        IsExecuting = false;
        ValidationText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogExecutionDeferred);
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
    }

    public WorkspaceExportCompressionOptionViewModel? SelectedCompressionOption
    {
        get => _selectedCompressionOption;
        set => SetProperty(ref _selectedCompressionOption, value);
    }

    public void SetExecuting()
    {
        IsExecuting = true;
        ValidationText = string.Empty;
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
        ValidationText = string.Empty;
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
    }

    public void ApplyExecutionUnavailable()
    {
        IsExecuting = false;
        ValidationText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogExecutionFailed);
        PrimaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);
    }

    public void ApplyExecutionResult(WorkspaceSaveExecutionResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IsExecuting = false;
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

        return new WorkspaceExportDialogSubmission(
            ExportToWorkspaceDirectory,
            selectedBundleIds,
            processorSelections,
            SelectedCompressionOption?.Mode ?? WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
            OverwriteExistingFiles
                ? WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting
                : WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists);
    }

    private void OnBundleSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasExportTargets));
        RefreshPlanSummary();
    }

    private void RefreshPlanSummary()
    {
        var selectedBundles = Bundles
            .Where(static bundle => bundle.IsSelected)
            .ToArray();
        PlanSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogPlanSummaryFormat,
            selectedBundles.Length,
            selectedBundles.Sum(static bundle => bundle.ChangeCount),
            selectedBundles.Sum(static bundle => bundle.ModifiedAssetCount),
            selectedBundles.Sum(static bundle => bundle.WorkspaceReplacementCount + bundle.FileReplacementCount),
            selectedBundles.Sum(static bundle => bundle.ExtractedAssetCount));
    }

    private void RefreshVisibleBundles()
    {
        var query = BundleSearchText.Trim();
        var candidates = Bundles.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            candidates = candidates.Where(bundle =>
                bundle.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                bundle.OutputPath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        candidates = SelectedBundleSortOption?.Id == "name"
            ? candidates
                .OrderBy(static bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static bundle => bundle.LastChangedAtUtc ?? DateTimeOffset.MinValue)
            : candidates
                .OrderByDescending(static bundle => bundle.LastChangedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(static bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase);

        var orderedBundles = candidates.ToArray();
        VisibleBundles.Clear();
        if (SelectedBundleSortOption?.Id == "time" && string.IsNullOrWhiteSpace(query))
        {
            AppendTimeGroupedVisibleBundles(orderedBundles);
            return;
        }

        foreach (var bundle in orderedBundles)
        {
            VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
        }
    }

    private void AppendTimeGroupedVisibleBundles(IReadOnlyList<WorkspaceExportBundleOptionViewModel> orderedBundles)
    {
        if (orderedBundles.Count == 0)
        {
            return;
        }

        var latestChangedAt = orderedBundles
            .Select(static bundle => bundle.LastChangedAtUtc)
            .Where(static value => value is not null)
            .Max();
        var recentCutoff = latestChangedAt?.AddHours(-24);
        var recentBundles = recentCutoff is null
            ? Array.Empty<WorkspaceExportBundleOptionViewModel>()
            : orderedBundles
                .Where(bundle => bundle.LastChangedAtUtc >= recentCutoff)
                .ToArray();
        var previousBundles = recentCutoff is null
            ? orderedBundles
            : orderedBundles
                .Where(bundle => bundle.LastChangedAtUtc < recentCutoff || bundle.LastChangedAtUtc is null)
                .ToArray();

        if (recentBundles.Length > 0)
        {
            VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(
                bundle: null,
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleGroupRecent)));
            foreach (var bundle in recentBundles)
            {
                VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
            }
        }

        if (previousBundles.Count > 0)
        {
            VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(
                bundle: null,
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleGroupPrevious)));
            foreach (var bundle in previousBundles)
            {
                VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
            }
        }
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
