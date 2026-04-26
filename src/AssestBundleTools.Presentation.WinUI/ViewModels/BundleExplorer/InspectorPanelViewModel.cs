using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示右侧详情检查器当前的展示状态。
/// </summary>
public sealed class InspectorPanelViewModel : ObservableObject
{
    private string _emptyStateText = string.Empty;
    private string _assetName = string.Empty;
    private string _bundleDisplayName = string.Empty;
    private string _assetTypeText = string.Empty;
    private string _assetPath = string.Empty;
    private string _containerPath = string.Empty;
    private string _sizeText = string.Empty;
    private string _pathIdText = string.Empty;
    private string _previewInfoText = string.Empty;
    private string _previewStatusText = string.Empty;
    private string _previewText = string.Empty;
    private string _previewCacheStatusText = string.Empty;
    private string _previewFallbackReasonText = string.Empty;
    private string _previewDiagnosticDetailText = string.Empty;
    private ImageSource? _previewImageSource;
    private AssetPreviewKind _previewKind;
    private bool _isPreviewLoading;
    private string _extractCapabilityText = string.Empty;
    private string _fileReplacementCapabilityText = string.Empty;
    private string _workspaceReplacementCapabilityText = string.Empty;
    private string _replacementSourceText = string.Empty;
    private string _replacementStatusText = string.Empty;
    private string _changeSummaryText = string.Empty;
    private bool _canExtract;
    private bool _canSelectFileReplacementSource;
    private bool _canSelectReplacementSource;
    private bool _canApplyReplacement;
    private bool _canUndoReplacement;
    private bool _hasSelection;

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public string AssetName
    {
        get => _assetName;
        set => SetProperty(ref _assetName, value);
    }

    public string BundleDisplayName
    {
        get => _bundleDisplayName;
        set => SetProperty(ref _bundleDisplayName, value);
    }

    public string AssetTypeText
    {
        get => _assetTypeText;
        set => SetProperty(ref _assetTypeText, value);
    }

    public string AssetPath
    {
        get => _assetPath;
        set => SetProperty(ref _assetPath, value);
    }

    public string ContainerPath
    {
        get => _containerPath;
        set
        {
            if (SetProperty(ref _containerPath, value))
            {
                OnPropertyChanged(nameof(ContainerPathVisibility));
            }
        }
    }

    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value);
    }

    public string PathIdText
    {
        get => _pathIdText;
        set
        {
            if (SetProperty(ref _pathIdText, value))
            {
                OnPropertyChanged(nameof(PathIdVisibility));
            }
        }
    }

    public string PreviewInfoText
    {
        get => _previewInfoText;
        private set
        {
            if (SetProperty(ref _previewInfoText, value))
            {
                OnPropertyChanged(nameof(PreviewInfoVisibility));
            }
        }
    }

    public string PreviewStatusText
    {
        get => _previewStatusText;
        private set => SetProperty(ref _previewStatusText, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string PreviewCacheStatusText
    {
        get => _previewCacheStatusText;
        private set
        {
            if (SetProperty(ref _previewCacheStatusText, value))
            {
                NotifyPreviewDiagnosticStateChanged();
            }
        }
    }

    public string PreviewFallbackReasonText
    {
        get => _previewFallbackReasonText;
        private set
        {
            if (SetProperty(ref _previewFallbackReasonText, value))
            {
                NotifyPreviewDiagnosticStateChanged();
            }
        }
    }

    public string PreviewDiagnosticDetailText
    {
        get => _previewDiagnosticDetailText;
        private set
        {
            if (SetProperty(ref _previewDiagnosticDetailText, value))
            {
                NotifyPreviewDiagnosticStateChanged();
            }
        }
    }

    public ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        private set => SetProperty(ref _previewImageSource, value);
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set => SetProperty(ref _isPreviewLoading, value);
    }

    /// <summary>
    /// 用于右侧“替换 / 概览”区展示当前资源的可提取能力。
    /// 这里使用已经本地化后的展示文本，避免把文案拼装逻辑放回 XAML。
    /// </summary>
    public string ExtractCapabilityText
    {
        get => _extractCapabilityText;
        set => SetProperty(ref _extractCapabilityText, value);
    }

    public string FileReplacementCapabilityText
    {
        get => _fileReplacementCapabilityText;
        set => SetProperty(ref _fileReplacementCapabilityText, value);
    }

    public string WorkspaceReplacementCapabilityText
    {
        get => _workspaceReplacementCapabilityText;
        set => SetProperty(ref _workspaceReplacementCapabilityText, value);
    }

    public string ReplacementSourceText
    {
        get => _replacementSourceText;
        set => SetProperty(ref _replacementSourceText, value);
    }

    public string ReplacementStatusText
    {
        get => _replacementStatusText;
        set => SetProperty(ref _replacementStatusText, value);
    }

    public string ChangeSummaryText
    {
        get => _changeSummaryText;
        set => SetProperty(ref _changeSummaryText, value);
    }

    public bool CanExtract
    {
        get => _canExtract;
        set => SetProperty(ref _canExtract, value);
    }

    public bool CanSelectFileReplacementSource
    {
        get => _canSelectFileReplacementSource;
        set => SetProperty(ref _canSelectFileReplacementSource, value);
    }

    public bool CanSelectReplacementSource
    {
        get => _canSelectReplacementSource;
        set => SetProperty(ref _canSelectReplacementSource, value);
    }

    public bool CanApplyReplacement
    {
        get => _canApplyReplacement;
        set => SetProperty(ref _canApplyReplacement, value);
    }

    public bool CanUndoReplacement
    {
        get => _canUndoReplacement;
        set => SetProperty(ref _canUndoReplacement, value);
    }

    public ObservableCollection<AssetDependencyViewModel> AssetDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> ReferencedByDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> DependencyClosureItems { get; } = new();

    public ObservableCollection<BundleExternalDependencyViewModel> BundleExternalDependencies { get; } = new();

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                OnPropertyChanged(nameof(DetailsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(ContainerPathVisibility));
                OnPropertyChanged(nameof(PathIdVisibility));
                OnPropertyChanged(nameof(PreviewInfoVisibility));
                NotifyPreviewStateChanged();
                NotifyPreviewDiagnosticStateChanged();
            }
        }
    }

    public Visibility DetailsVisibility => HasSelection ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => HasSelection ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AssetDependenciesVisibility => AssetDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReferencedByDependenciesVisibility => ReferencedByDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependencyClosureVisibility => DependencyClosureItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BundleExternalDependenciesVisibility => BundleExternalDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependenciesEmptyStateVisibility =>
        AssetDependencies.Count == 0 &&
        ReferencedByDependencies.Count == 0 &&
        DependencyClosureItems.Count == 0 &&
        BundleExternalDependencies.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewLoadingVisibility => HasSelection && IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewImageVisibility =>
        HasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Image &&
        PreviewImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewTextVisibility =>
        HasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Text &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewSummaryVisibility =>
        HasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Summary &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMessageVisibility =>
        HasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.None &&
        !string.IsNullOrWhiteSpace(PreviewStatusText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PathIdVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(PathIdText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ContainerPathVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(ContainerPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewInfoVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(PreviewInfoText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewDiagnosticVisibility =>
        HasSelection &&
        (!string.IsNullOrWhiteSpace(PreviewCacheStatusText) ||
         !string.IsNullOrWhiteSpace(PreviewFallbackReasonText) ||
         !string.IsNullOrWhiteSpace(PreviewDiagnosticDetailText))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewCacheStatusVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(PreviewCacheStatusText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewFallbackReasonVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(PreviewFallbackReasonText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewDiagnosticDetailVisibility =>
        HasSelection && !string.IsNullOrWhiteSpace(PreviewDiagnosticDetailText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public void Clear(string emptyStateText)
    {
        EmptyStateText = emptyStateText;
        AssetName = string.Empty;
        BundleDisplayName = string.Empty;
        AssetTypeText = string.Empty;
        AssetPath = string.Empty;
        ContainerPath = string.Empty;
        SizeText = string.Empty;
        PathIdText = string.Empty;
        PreviewInfoText = string.Empty;
        ExtractCapabilityText = string.Empty;
        FileReplacementCapabilityText = string.Empty;
        WorkspaceReplacementCapabilityText = string.Empty;
        ReplacementSourceText = string.Empty;
        ReplacementStatusText = string.Empty;
        ChangeSummaryText = string.Empty;
        CanExtract = false;
        CanSelectFileReplacementSource = false;
        CanSelectReplacementSource = false;
        CanApplyReplacement = false;
        CanUndoReplacement = false;
        AssetDependencies.Clear();
        ReferencedByDependencies.Clear();
        DependencyClosureItems.Clear();
        BundleExternalDependencies.Clear();
        ResetPreviewState();
        NotifyDependencyStateChanged();
        HasSelection = false;
    }

    public void Apply(
        string assetName,
        string bundleDisplayName,
        string assetTypeText,
        string assetPath,
        string containerPath,
        string sizeText,
        string pathIdText,
        string extractCapabilityText,
        string fileReplacementCapabilityText,
        string workspaceReplacementCapabilityText,
        string replacementSourceText,
        string replacementStatusText,
        string changeSummaryText,
        bool canExtract,
        bool canSelectFileReplacementSource,
        bool canSelectReplacementSource,
        bool canApplyReplacement,
        bool canUndoReplacement,
        IEnumerable<AssetDependencyViewModel> assetDependencies,
        IEnumerable<AssetDependencyViewModel> referencedByDependencies,
        IEnumerable<AssetDependencyViewModel> dependencyClosureItems,
        IEnumerable<BundleExternalDependencyViewModel> bundleExternalDependencies)
    {
        AssetName = assetName;
        BundleDisplayName = bundleDisplayName;
        AssetTypeText = assetTypeText;
        AssetPath = assetPath;
        ContainerPath = NormalizeDisplayContainerPath(assetPath, containerPath);
        SizeText = sizeText;
        PathIdText = pathIdText;
        PreviewInfoText = string.Empty;
        ExtractCapabilityText = extractCapabilityText;
        FileReplacementCapabilityText = fileReplacementCapabilityText;
        WorkspaceReplacementCapabilityText = workspaceReplacementCapabilityText;
        ReplacementSourceText = replacementSourceText;
        ReplacementStatusText = replacementStatusText;
        ChangeSummaryText = changeSummaryText;
        CanExtract = canExtract;
        CanSelectFileReplacementSource = canSelectFileReplacementSource;
        CanSelectReplacementSource = canSelectReplacementSource;
        CanApplyReplacement = canApplyReplacement;
        CanUndoReplacement = canUndoReplacement;

        AssetDependencies.Clear();
        foreach (var dependency in assetDependencies)
        {
            AssetDependencies.Add(dependency);
        }

        ReferencedByDependencies.Clear();
        foreach (var dependency in referencedByDependencies)
        {
            ReferencedByDependencies.Add(dependency);
        }

        DependencyClosureItems.Clear();
        foreach (var dependency in dependencyClosureItems)
        {
            DependencyClosureItems.Add(dependency);
        }

        BundleExternalDependencies.Clear();
        foreach (var dependency in bundleExternalDependencies)
        {
            BundleExternalDependencies.Add(dependency);
        }

        NotifyDependencyStateChanged();
        HasSelection = true;
    }

    public void SetPreviewLoading(string statusText)
    {
        ResetPreviewState();
        PreviewInfoText = string.Empty;
        PreviewStatusText = statusText;
        IsPreviewLoading = true;
        NotifyPreviewStateChanged();
    }

    public void ShowPreviewMessage(string statusText)
    {
        ResetPreviewState();
        PreviewInfoText = string.Empty;
        PreviewStatusText = statusText;
        NotifyPreviewStateChanged();
    }

    /// <summary>
    /// 预览诊断是预览 DTO 的结构化补充信息，
    /// 由 Presentation 层在本地化后统一写入，避免把 UI 文案拼装逻辑散落到 XAML。
    /// </summary>
    public void ApplyPreviewDiagnostic(
        string cacheStatusText,
        string fallbackReasonText,
        string diagnosticDetailText)
    {
        PreviewCacheStatusText = NormalizePreviewInfoText(cacheStatusText);
        PreviewFallbackReasonText = NormalizePreviewInfoText(fallbackReasonText);
        PreviewDiagnosticDetailText = NormalizePreviewInfoText(diagnosticDetailText);
    }

    public void OverrideSizeText(string sizeText)
    {
        SizeText = string.IsNullOrWhiteSpace(sizeText)
            ? string.Empty
            : sizeText.Trim();
    }

    public void ApplyImagePreview(ImageSource imageSource, string? previewInfoText = null)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        ResetPreviewState();
        _previewKind = AssetPreviewKind.Image;
        PreviewInfoText = NormalizePreviewInfoText(previewInfoText);
        PreviewImageSource = imageSource;
        NotifyPreviewStateChanged();
    }

    public void ApplyTextPreview(string textContent, string? previewInfoText = null)
    {
        ResetPreviewState();
        _previewKind = AssetPreviewKind.Text;
        PreviewInfoText = NormalizePreviewInfoText(previewInfoText);
        PreviewText = textContent;
        NotifyPreviewStateChanged();
    }

    public void ApplySummaryPreview(string summaryText, string? previewInfoText = null)
    {
        ResetPreviewState();
        _previewKind = AssetPreviewKind.Summary;
        PreviewInfoText = NormalizePreviewInfoText(previewInfoText);
        PreviewText = summaryText;
        NotifyPreviewStateChanged();
    }

    private void NotifyDependencyStateChanged()
    {
        OnPropertyChanged(nameof(AssetDependenciesVisibility));
        OnPropertyChanged(nameof(ReferencedByDependenciesVisibility));
        OnPropertyChanged(nameof(DependencyClosureVisibility));
        OnPropertyChanged(nameof(BundleExternalDependenciesVisibility));
        OnPropertyChanged(nameof(DependenciesEmptyStateVisibility));
    }

    private void ResetPreviewState()
    {
        IsPreviewLoading = false;
        PreviewStatusText = string.Empty;
        PreviewText = string.Empty;
        PreviewImageSource = null;
        PreviewCacheStatusText = string.Empty;
        PreviewFallbackReasonText = string.Empty;
        PreviewDiagnosticDetailText = string.Empty;
        _previewKind = AssetPreviewKind.None;
        NotifyPreviewStateChanged();
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(PreviewLoadingVisibility));
        OnPropertyChanged(nameof(PreviewImageVisibility));
        OnPropertyChanged(nameof(PreviewTextVisibility));
        OnPropertyChanged(nameof(PreviewSummaryVisibility));
        OnPropertyChanged(nameof(PreviewMessageVisibility));
    }

    private void NotifyPreviewDiagnosticStateChanged()
    {
        OnPropertyChanged(nameof(PreviewDiagnosticVisibility));
        OnPropertyChanged(nameof(PreviewCacheStatusVisibility));
        OnPropertyChanged(nameof(PreviewFallbackReasonVisibility));
        OnPropertyChanged(nameof(PreviewDiagnosticDetailVisibility));
    }

    private static string NormalizePreviewInfoText(string? previewInfoText) =>
        string.IsNullOrWhiteSpace(previewInfoText)
            ? string.Empty
            : previewInfoText.Trim();

    private static string NormalizeDisplayContainerPath(string assetPath, string containerPath)
    {
        if (string.IsNullOrWhiteSpace(containerPath))
        {
            return string.Empty;
        }

        var normalizedContainerPath = containerPath.Trim().Replace('\\', '/');
        var normalizedAssetPath = string.IsNullOrWhiteSpace(assetPath)
            ? string.Empty
            : assetPath.Trim().Replace('\\', '/');

        return string.Equals(normalizedContainerPath, normalizedAssetPath, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalizedContainerPath;
    }
}
