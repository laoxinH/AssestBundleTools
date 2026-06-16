using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceExportDialogViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private string _sourceSummaryText = string.Empty;
    private string _recentChangesSummaryText = string.Empty;
    private string _outputDirectoryText = string.Empty;
    private string _planSummaryText = string.Empty;
    private string _validationText = string.Empty;
    private string _primaryButtonText = string.Empty;
    private bool _isLoading;
    private bool _isExecuting;
    private WorkspaceExportProcessorOptionViewModel? _selectedProcessor;

    public WorkspaceExportDialogViewModel(
        ILocalizationService localizationService,
        WorkspaceChangeSummaryDto? changeSummary,
        WorkspaceSavePreviewDto? savePreview)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _sourceSummaryText = changeSummary is null
            ? string.Empty
            : _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogSourceSummaryFormat,
                changeSummary.ModifiedAssetCount,
                changeSummary.TotalChangeCount);
        _recentChangesSummaryText = changeSummary is null
            ? string.Empty
            : _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogRecentChangesSummaryFormat,
                Math.Min(changeSummary.Items.Count, 8));
        _outputDirectoryText = savePreview?.OutputDirectory ?? string.Empty;
        _planSummaryText = savePreview is null
            ? string.Empty
            : _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogPlanSummaryFormat,
                savePreview.PlannedBundleCount,
                savePreview.TotalChangeCount);
        _primaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);

        Bundles = new ObservableCollection<WorkspaceExportBundleOptionViewModel>(
            CreateBundleOptions(savePreview));
        RecentChanges = new ObservableCollection<WorkspaceChangeItemDto>(
            changeSummary?.Items
                .OrderByDescending(static item => item.LastChangedAtUtc ?? DateTimeOffset.MinValue)
                .Take(8)
                .ToArray() ?? Array.Empty<WorkspaceChangeItemDto>());
        Processors = new ObservableCollection<WorkspaceExportProcessorOptionViewModel>
        {
            new(
                "none",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNone),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNoneDescription)),
            new(
                "placeholder",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorPlaceholder),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorPlaceholderDescription))
        };
        _selectedProcessor = Processors.FirstOrDefault();
        foreach (var bundle in Bundles)
        {
            bundle.SelectionChanged += OnBundleSelectionChanged;
        }
    }

    public ObservableCollection<WorkspaceExportBundleOptionViewModel> Bundles { get; }

    public ObservableCollection<WorkspaceChangeItemDto> RecentChanges { get; }

    public ObservableCollection<WorkspaceExportProcessorOptionViewModel> Processors { get; }

    public string SourceSummaryText
    {
        get => _sourceSummaryText;
        private set => SetProperty(ref _sourceSummaryText, value);
    }

    public string RecentChangesSummaryText
    {
        get => _recentChangesSummaryText;
        private set => SetProperty(ref _recentChangesSummaryText, value);
    }

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

    public WorkspaceExportProcessorOptionViewModel? SelectedProcessor
    {
        get => _selectedProcessor;
        set => SetProperty(ref _selectedProcessor, value);
    }

    public bool HasExportTargets => Bundles.Any(static bundle => bundle.IsSelected);

    public void SelectAllBundles()
    {
        foreach (var bundle in Bundles)
        {
            bundle.IsSelected = true;
        }
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in Bundles)
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

    private void OnBundleSelectionChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(HasExportTargets));

    private IEnumerable<WorkspaceExportBundleOptionViewModel> CreateBundleOptions(WorkspaceSavePreviewDto? savePreview)
    {
        if (savePreview is null)
        {
            yield break;
        }

        foreach (var plan in savePreview.BundlePlans)
        {
            yield return new WorkspaceExportBundleOptionViewModel(
                plan.BundleId,
                plan.BundleDisplayName,
                plan.OutputFilePath,
                _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceExportDialogBundleSummaryFormat,
                    plan.ChangeCount,
                    plan.ModifiedAssetCount,
                    plan.ExtractedAssetCount,
                    plan.WorkspaceReplacementCount,
                    plan.FileReplacementCount),
                lastChangedAtUtc: null);
        }
    }
}
