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
        _outputDirectoryText = savePreview?.OutputDirectory ?? string.Empty;
        _primaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);

        Bundles = new ObservableCollection<WorkspaceExportBundleOptionViewModel>(
            CreateBundleOptions(savePreview));
        ResourceProcessors = new ObservableCollection<WorkspaceExportResourceProcessorOptionViewModel>
        {
            new(
                "texture2d",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2DDescription)),
            new(
                "audio",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudioDescription)),
            new(
                "atlas",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlasDescription)),
            new(
                "skel",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkelDescription))
        };
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

        RefreshPlanSummary();
    }

    public ObservableCollection<WorkspaceExportBundleOptionViewModel> Bundles { get; }

    public ObservableCollection<WorkspaceExportResourceProcessorOptionViewModel> ResourceProcessors { get; }

    public ObservableCollection<WorkspaceExportProcessorOptionViewModel> Processors { get; }

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
                plan.ChangeCount,
                plan.ModifiedAssetCount,
                plan.ExtractedAssetCount,
                plan.WorkspaceReplacementCount,
                plan.FileReplacementCount,
                lastChangedAtUtc: null);
        }
    }
}
