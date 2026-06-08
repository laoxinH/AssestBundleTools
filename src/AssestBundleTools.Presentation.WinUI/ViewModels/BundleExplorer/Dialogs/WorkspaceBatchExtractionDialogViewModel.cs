using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

/// <summary>
/// 承载工作区级批量提取对话框的选择状态与摘要投影。
/// </summary>
public sealed class WorkspaceBatchExtractionDialogViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _valueFormatter;
    private readonly List<WorkspaceBatchExtractionBundleOptionViewModel> _allBundles = [];
    private bool _isApplyingCategorySelection;
    private string _bundleSearchKeyword = string.Empty;
    private string _planSummaryText;
    private string _validationText;
    private bool _isPreviewLoading;
    private bool _useWorkspaceExtractionDirectory;

    public WorkspaceBatchExtractionDialogViewModel(
        ILocalizationService localizationService,
        IEnumerable<BundleNodeViewModel> bundleNodes,
        BundleId? activeBundleId)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _valueFormatter = new LocalizedValueFormatter(_localizationService);
        _planSummaryText = GetString(ResourceKeys.WorkspaceBatchExtractionDialogPreviewWaiting);
        _validationText = string.Empty;

        foreach (var bundleNode in bundleNodes)
        {
            var isSelected = activeBundleId is not null &&
                             bundleNode.BundleId.Equals(activeBundleId);
            var item = new WorkspaceBatchExtractionBundleOptionViewModel(
                bundleNode.BundleId,
                bundleNode.DisplayName,
                bundleNode.LogicalPath,
                bundleNode.AssetCount,
                _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceBatchExtractionDialogBundleAssetCountFormat,
                    bundleNode.AssetCount.ToString(CultureInfo.CurrentCulture)),
                isSelected);
            item.SelectionChanged += OnBundleSelectionChanged;
            _allBundles.Add(item);
        }

        RebuildVisibleBundles();
        BuildInitialCategoryOptions();
        RefreshState();
    }

    public event EventHandler? StateChanged;

    public ObservableCollection<WorkspaceBatchExtractionBundleOptionViewModel> VisibleBundles { get; } = [];

    public ObservableCollection<WorkspaceBatchExtractionCategoryOptionViewModel> CategoryOptions { get; } = [];

    public string BundleSearchKeyword
    {
        get => _bundleSearchKeyword;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _bundleSearchKeyword, normalized))
            {
                RebuildVisibleBundles();
                OnPropertyChanged(nameof(BundleEmptyStateVisibility));
                NotifyStateChanged();
            }
        }
    }

    public bool UseWorkspaceExtractionDirectory
    {
        get => _useWorkspaceExtractionDirectory;
        set
        {
            if (SetProperty(ref _useWorkspaceExtractionDirectory, value))
            {
                RefreshState();
            }
        }
    }

    public string PlanSummaryText
    {
        get => _planSummaryText;
        private set => SetProperty(ref _planSummaryText, value);
    }

    public string ValidationText
    {
        get => _validationText;
        private set
        {
            if (SetProperty(ref _validationText, value))
            {
                OnPropertyChanged(nameof(ValidationVisibility));
            }
        }
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set
        {
            if (SetProperty(ref _isPreviewLoading, value))
            {
                OnPropertyChanged(nameof(PreviewLoadingVisibility));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public Visibility PreviewLoadingVisibility => IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ValidationVisibility =>
        string.IsNullOrWhiteSpace(ValidationText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility BundleEmptyStateVisibility =>
        VisibleBundles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool CanSubmit =>
        !IsPreviewLoading &&
        SelectedBundleIds.Count > 0 &&
        SelectedCategories.Count > 0;

    public IReadOnlyList<BundleId> SelectedBundleIds =>
        _allBundles
            .Where(static bundle => bundle.IsSelected)
            .Select(static bundle => bundle.BundleId)
            .ToArray();

    public IReadOnlyList<WorkspaceBatchExtractionCategoryDto> SelectedCategories =>
        CategoryOptions
            .Where(static option => option.IsSelected)
            .Select(static option => option.Category)
            .ToArray();

    public int SelectedBundleCount => SelectedBundleIds.Count;

    public string SelectedBundleSummaryText =>
        _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchExtractionDialogSelectedBundleSummaryFormat,
            SelectedBundleCount.ToString(CultureInfo.CurrentCulture),
            _allBundles.Count.ToString(CultureInfo.CurrentCulture));

    public void SelectAllBundles()
    {
        foreach (var bundle in _allBundles)
        {
            bundle.IsSelected = true;
        }

        RefreshState();
        NotifyStateChanged();
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in _allBundles)
        {
            bundle.IsSelected = false;
        }

        RefreshState();
        NotifyStateChanged();
    }

    public void SetPreviewLoading()
    {
        IsPreviewLoading = true;
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchExtractionDialogPreviewLoading);
    }

    public void ApplyPreview(WorkspaceBatchExtractionPreviewDto preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        IsPreviewLoading = false;
        ApplyCategoryPlans(preview.CategoryPlans ?? Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>());
        var candidateAssetCount = preview.CandidateAssetCount.ToString(CultureInfo.CurrentCulture);
        var candidateBundleCount = preview.CandidateBundleCount.ToString(CultureInfo.CurrentCulture);
        var candidateSpineGroupCount = preview.CandidateSpineGroupCount.ToString(CultureInfo.CurrentCulture);
        PlanSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchExtractionDialogPreviewSummaryFormat,
            candidateBundleCount,
            candidateAssetCount,
            candidateSpineGroupCount,
            _valueFormatter.FormatCompactByteSize(preview.TotalSizeInBytes));
    }

    public void ApplyPreviewUnavailable()
    {
        IsPreviewLoading = false;
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchExtractionDialogPreviewUnavailable);
    }

    public bool TryCreateSubmission(out WorkspaceBatchExtractionDialogSubmission? submission)
    {
        RefreshState();
        if (!CanSubmit)
        {
            submission = null;
            return false;
        }

        submission = new WorkspaceBatchExtractionDialogSubmission(
            UseWorkspaceExtractionDirectory,
            SelectedBundleIds,
            SelectedCategories);
        return true;
    }

    private void BuildInitialCategoryOptions() =>
        AddCategory(WorkspaceBatchExtractionCategoryDto.All, candidateCount: 0, isSelected: true);

    private void ApplyCategoryPlans(IReadOnlyList<WorkspaceBatchExtractionCategoryPlanDto> categoryPlans)
    {
        var selectedCategories = SelectedCategories.ToHashSet();
        var hadCategoryOptions = CategoryOptions.Count > 0;
        var shouldSelectAllByDefault = (selectedCategories.Count == 0 && !hadCategoryOptions) ||
                                       selectedCategories.Contains(WorkspaceBatchExtractionCategoryDto.All);
        _isApplyingCategorySelection = true;
        try
        {
            foreach (var option in CategoryOptions)
            {
                option.SelectionChanged -= OnCategorySelectionChanged;
            }

            CategoryOptions.Clear();
            foreach (var categoryPlan in categoryPlans.Where(static plan => plan.CandidateCount > 0))
            {
                var isSelected = shouldSelectAllByDefault
                    ? categoryPlan.Category.Kind == WorkspaceBatchExtractionCategoryKind.All
                    : selectedCategories.Contains(categoryPlan.Category);
                AddCategory(categoryPlan.Category, categoryPlan.CandidateCount, isSelected);
            }

            if (CategoryOptions.Count > 0 &&
                selectedCategories.Count > 0 &&
                SelectedCategories.Count == 0)
            {
                var allOption = CategoryOptions.FirstOrDefault(static option => option.IsAllCategory);
                if (allOption is not null)
                {
                    allOption.IsSelected = true;
                }
            }
        }
        finally
        {
            _isApplyingCategorySelection = false;
        }

        RefreshState();
    }

    private void AddCategory(
        WorkspaceBatchExtractionCategoryDto category,
        int candidateCount,
        bool isSelected)
    {
        var item = new WorkspaceBatchExtractionCategoryOptionViewModel(
            category,
            ResolveCategoryDisplayName(category),
            _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchExtractionDialogCategoryCandidateCountFormat,
                candidateCount.ToString(CultureInfo.CurrentCulture)),
            candidateCount,
            isSelected);
        item.SelectionChanged += OnCategorySelectionChanged;
        CategoryOptions.Add(item);
    }

    private string ResolveCategoryDisplayName(WorkspaceBatchExtractionCategoryDto category)
    {
        return category.Kind switch
        {
            WorkspaceBatchExtractionCategoryKind.All => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryAll),
            WorkspaceBatchExtractionCategoryKind.SpineGroup => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategorySpine),
            WorkspaceBatchExtractionCategoryKind.UnityAssetType => ResolveAssetTypeDisplayName(
                category.AssetType,
                category.AssetTypeDisplayName),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };
    }

    private string ResolveAssetTypeDisplayName(AssetType? assetType, string? assetTypeDisplayName)
    {
        if (assetType == AssetType.Unknown && !string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryTexture2D),
            AssetType.Sprite => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategorySprite),
            AssetType.TextAsset => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryTextAsset),
            AssetType.AudioClip => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryMesh),
            AssetType.Material => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };
    }

    private void OnBundleSelectionChanged(object? sender, EventArgs e)
    {
        RefreshState();
        NotifyStateChanged();
    }

    private void OnCategorySelectionChanged(object? sender, EventArgs e)
    {
        if (_isApplyingCategorySelection ||
            sender is not WorkspaceBatchExtractionCategoryOptionViewModel changedOption)
        {
            return;
        }

        _isApplyingCategorySelection = true;
        try
        {
            if (changedOption.IsAllCategory && changedOption.IsSelected)
            {
                foreach (var option in CategoryOptions.Where(static option => !option.IsAllCategory))
                {
                    option.IsSelected = false;
                }
            }
            else if (!changedOption.IsAllCategory && changedOption.IsSelected)
            {
                var allOption = CategoryOptions.FirstOrDefault(static option => option.IsAllCategory);
                if (allOption is not null)
                {
                    allOption.IsSelected = false;
                }
            }
        }
        finally
        {
            _isApplyingCategorySelection = false;
        }

        RefreshState();
        NotifyStateChanged();
    }

    private void RebuildVisibleBundles()
    {
        VisibleBundles.Clear();
        var keyword = string.IsNullOrWhiteSpace(BundleSearchKeyword)
            ? null
            : BundleSearchKeyword.Trim();

        foreach (var bundle in _allBundles.Where(bundle => Matches(bundle, keyword)))
        {
            VisibleBundles.Add(bundle);
        }

        OnPropertyChanged(nameof(BundleEmptyStateVisibility));
    }

    private static bool Matches(WorkspaceBatchExtractionBundleOptionViewModel bundle, string? keyword)
    {
        if (keyword is null)
        {
            return true;
        }

        return bundle.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.LogicalPath.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.BundleId.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshState()
    {
        ValidationText = ResolveValidationText();
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SelectedBundleCount));
        OnPropertyChanged(nameof(SelectedBundleSummaryText));
    }

    private string ResolveValidationText()
    {
        if (SelectedBundleIds.Count == 0)
        {
            return GetString(ResourceKeys.WorkspaceBatchExtractionDialogValidationBundleRequired);
        }

        if (SelectedCategories.Count == 0)
        {
            return GetString(ResourceKeys.WorkspaceBatchExtractionDialogValidationCategoryRequired);
        }

        return string.Empty;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
