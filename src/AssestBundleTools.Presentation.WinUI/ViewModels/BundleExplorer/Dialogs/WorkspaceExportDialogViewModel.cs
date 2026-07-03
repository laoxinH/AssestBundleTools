using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssestBundleTools.Application.Bundles.Dtos;
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
    private string _bundleSearchText = string.Empty;
    private bool _isLoading;
    private bool _isExecuting;
    private WorkspaceExportBundleSortOptionViewModel? _selectedBundleSortOption;

    public WorkspaceExportDialogViewModel(
        ILocalizationService localizationService,
        WorkspaceChangeSummaryDto? changeSummary,
        WorkspaceSavePreviewDto? savePreview,
        IReadOnlyList<ResourceProcessorCatalogEntryDto>? resourceProcessorCatalogEntries = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _outputDirectoryText = savePreview?.OutputDirectory ?? string.Empty;
        _primaryButtonText = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogPrimaryButton);

        var bundleLastChangedAtLookup = BuildBundleLastChangedAtLookup(changeSummary);
        Bundles = new ObservableCollection<WorkspaceExportBundleOptionViewModel>(
            CreateBundleOptions(savePreview, bundleLastChangedAtLookup));
        VisibleBundles = new ObservableCollection<WorkspaceExportBundleListItemViewModel>();
        BundleSortOptions = new ObservableCollection<WorkspaceExportBundleSortOptionViewModel>
        {
            new(
                "time",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleSortByTime)),
            new(
                "name",
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleSortByName))
        };
        ResourceProcessors = new ObservableCollection<WorkspaceExportResourceProcessorOptionViewModel>(
            CreateResourceProcessorOptions(resourceProcessorCatalogEntries ?? Array.Empty<ResourceProcessorCatalogEntryDto>()));
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

    private IEnumerable<WorkspaceExportBundleOptionViewModel> CreateBundleOptions(
        WorkspaceSavePreviewDto? savePreview,
        IReadOnlyDictionary<BundleId, DateTimeOffset> bundleLastChangedAtLookup)
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
                bundleLastChangedAtLookup.TryGetValue(plan.BundleId, out var lastChangedAtUtc)
                    ? lastChangedAtUtc
                    : null);
        }
    }

    private IEnumerable<WorkspaceExportResourceProcessorOptionViewModel> CreateResourceProcessorOptions(
        IReadOnlyList<ResourceProcessorCatalogEntryDto> catalogEntries)
    {
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "texture2d",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2DDescription),
            CreateProcessorOptions("texture2d", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "audio",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudioDescription),
            CreateProcessorOptions("audio", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "atlas",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlasDescription),
            CreateProcessorOptions("atlas", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "skel",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkelDescription),
            CreateProcessorOptions("skel", catalogEntries));
    }

    private IEnumerable<WorkspaceExportProcessorOptionViewModel> CreateProcessorOptions(
        string resourceKind,
        IReadOnlyList<ResourceProcessorCatalogEntryDto> catalogEntries)
    {
        yield return new WorkspaceExportProcessorOptionViewModel(
            $"{resourceKind}:none",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNone),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNoneDescription));

        foreach (var entry in catalogEntries.Where(entry => IsCompatibleResourceProcessor(entry, resourceKind)))
        {
            yield return new WorkspaceExportProcessorOptionViewModel(
                entry.Id,
                ResolveProcessorDisplayName(entry, resourceKind),
                ResolveProcessorDescription(entry, resourceKind));
        }
    }

    private static bool IsCompatibleResourceProcessor(
        ResourceProcessorCatalogEntryDto entry,
        string resourceProcessorId)
    {
        return entry.Status == ResourceProcessorStatus.Available &&
               entry.ResourceKinds.Any(resourceKind =>
                   string.Equals(
                       NormalizeResourceKind(resourceKind),
                       NormalizeResourceKind(resourceProcessorId),
                       StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveProcessorDisplayName(
        ResourceProcessorCatalogEntryDto entry,
        string resourceKind)
    {
        return entry.IsBuiltIn
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat,
                ResolveResourceKindDisplayName(resourceKind))
            : string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.Id
                : entry.DisplayName;
    }

    private string ResolveProcessorDescription(
        ResourceProcessorCatalogEntryDto entry,
        string resourceKind)
    {
        return string.IsNullOrWhiteSpace(entry.Description)
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDescriptionFormat,
                ResolveResourceKindDisplayName(resourceKind))
            : entry.Description;
    }

    private string ResolveResourceKindDisplayName(string resourceKind)
    {
        return NormalizeResourceKind(resourceKind) switch
        {
            "texture2d" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
            "audio" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
            "atlas" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
            "skel" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
            _ => resourceKind
        };
    }

    private static string NormalizeResourceKind(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static IReadOnlyDictionary<BundleId, DateTimeOffset> BuildBundleLastChangedAtLookup(
        WorkspaceChangeSummaryDto? changeSummary)
    {
        if (changeSummary is null)
        {
            return new Dictionary<BundleId, DateTimeOffset>();
        }

        return changeSummary.Items
            .Where(static item => item.LastChangedAtUtc is not null)
            .GroupBy(static item => item.BundleId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Max(static item => item.LastChangedAtUtc!.Value));
    }
}
