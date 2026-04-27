using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示右侧详情检查器当前的展示状态。
/// </summary>
public sealed class InspectorPanelViewModel : ObservableObject
{
    private const string DetailsSectionKey = "details";
    private const string DependenciesSectionKey = "dependencies";
    private const string ReplacementSectionKey = "replacement";
    private const string ChangesSectionKey = "changes";
    private const string BasicInfoCardKey = "basic";
    private const string PreviewInfoCardKey = "preview-info";
    private const string PreviewDiagnosticCardKey = "preview-diagnostic";

    private string _emptyStateText = string.Empty;
    private string _assetName = string.Empty;
    private string _bundleDisplayName = string.Empty;
    private string _assetTypeText = string.Empty;
    private string _assetPath = string.Empty;
    private string _containerPath = string.Empty;
    private string _sizeText = string.Empty;
    private string _pathIdText = string.Empty;
    private string _previewStatusText = string.Empty;
    private string _previewText = string.Empty;
    private string _previewPayloadFilePath = string.Empty;
    private string _previewCacheStatusText = string.Empty;
    private string _previewFallbackReasonText = string.Empty;
    private string _previewDiagnosticDetailText = string.Empty;
    private string _audioPreviewSizeSummaryText = string.Empty;
    private string _audioPreviewContainerBadgeText = string.Empty;
    private string _audioPreviewDurationBadgeText = string.Empty;
    private string _audioPreviewChannelBadgeText = string.Empty;
    private string _audioPreviewFrequencyBadgeText = string.Empty;
    private ImageSource? _previewImageSource;
    private MediaSource? _previewAudioSource;
    private IRandomAccessStream? _previewAudioStream;
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
    private int _selectedSectionIndex;
    private InspectorInformationTextSet _informationTextSet = InspectorInformationTextSet.CreateFallback();
    private IReadOnlyList<InspectorInfoRowViewModel> _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();

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
        set => SetProperty(ref _containerPath, value);
    }

    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value);
    }

    public string PathIdText
    {
        get => _pathIdText;
        set => SetProperty(ref _pathIdText, value);
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

    /// <summary>
    /// 指向当前预览缓存内容文件的真实路径。
    /// 该路径只用于“打开预览文件所在位置”这类壳层操作，不参与业务判断。
    /// </summary>
    public string PreviewPayloadFilePath
    {
        get => _previewPayloadFilePath;
        private set
        {
            if (SetProperty(ref _previewPayloadFilePath, value))
            {
                OnPropertyChanged(nameof(CanOpenPreviewLocation));
                OnPropertyChanged(nameof(PreviewOpenLocationVisibility));
            }
        }
    }

    public ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        private set => SetProperty(ref _previewImageSource, value);
    }

    public MediaSource? PreviewAudioSource
    {
        get => _previewAudioSource;
        private set => SetProperty(ref _previewAudioSource, value);
    }

    public string AudioPreviewSizeSummaryText
    {
        get => _audioPreviewSizeSummaryText;
        private set
        {
            if (SetProperty(ref _audioPreviewSizeSummaryText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewSizeSummaryVisibility));
            }
        }
    }

    public string AudioPreviewContainerBadgeText
    {
        get => _audioPreviewContainerBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewContainerBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewContainerBadgeVisibility));
            }
        }
    }

    public string AudioPreviewDurationBadgeText
    {
        get => _audioPreviewDurationBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewDurationBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewDurationBadgeVisibility));
            }
        }
    }

    public string AudioPreviewChannelBadgeText
    {
        get => _audioPreviewChannelBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewChannelBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewChannelBadgeVisibility));
            }
        }
    }

    public string AudioPreviewFrequencyBadgeText
    {
        get => _audioPreviewFrequencyBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewFrequencyBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewFrequencyBadgeVisibility));
            }
        }
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set => SetProperty(ref _isPreviewLoading, value);
    }

    /// <summary>
    /// 图片预览继续使用固定高度，保证列表与详情页的视觉稳定；
    /// 音频预览改为内容自适应，避免播放器和标签内容被固定高度裁切。
    /// </summary>
    public double PreviewSurfaceHeight => _previewKind == AssetPreviewKind.Audio ? double.NaN : 320d;

    public double PreviewSurfaceMinHeight => _previewKind == AssetPreviewKind.Audio ? 0d : 320d;

    /// <summary>
    /// 用于右侧“替换 / 详情”区展示当前资源的可提取能力。
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

    /// <summary>
    /// 右侧详情区的页签位置属于页面级浏览状态，
    /// 因此这里直接把选中的索引挂在 ViewModel 上，便于运行期切换和工作区恢复复用。
    /// </summary>
    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set
        {
            var normalizedSectionIndex = NormalizeSectionIndex(value);
            if (SetProperty(ref _selectedSectionIndex, normalizedSectionIndex))
            {
                NotifySectionSelectionStateChanged();
            }
        }
    }

    public string SelectedSectionKey => GetSectionKey(SelectedSectionIndex);

    public bool IsDetailsSectionSelected => SelectedSectionIndex == 0;

    public bool IsDependenciesSectionSelected => SelectedSectionIndex == 1;

    public bool IsReplacementSectionSelected => SelectedSectionIndex == 2;

    public bool IsChangesSectionSelected => SelectedSectionIndex == 3;

    public ObservableCollection<AssetDependencyViewModel> AssetDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> ReferencedByDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> DependencyClosureItems { get; } = new();

    public ObservableCollection<BundleExternalDependencyViewModel> BundleExternalDependencies { get; } = new();

    public ObservableCollection<InspectorInfoCardViewModel> InformationCards { get; } = new();

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                OnPropertyChanged(nameof(DetailsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                NotifyPreviewStateChanged();
            }
        }
    }

    public Visibility DetailsVisibility => HasSelection ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => HasSelection ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailsSectionVisibility => SelectedSectionIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependenciesSectionVisibility => SelectedSectionIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReplacementSectionVisibility => SelectedSectionIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChangesSectionVisibility => SelectedSectionIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

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

    public Visibility PreviewAudioVisibility =>
        HasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Audio &&
        PreviewAudioSource is not null
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

    public bool CanOpenPreviewLocation => !string.IsNullOrWhiteSpace(PreviewPayloadFilePath);

    public Visibility PreviewOpenLocationVisibility =>
        CanOpenPreviewLocation
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AudioPreviewSizeSummaryVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewSizeSummaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewContainerBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewContainerBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewDurationBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewDurationBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewChannelBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewChannelBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewFrequencyBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewFrequencyBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public void ConfigureInformationText(InspectorInformationTextSet textSet)
    {
        _informationTextSet = textSet ?? throw new ArgumentNullException(nameof(textSet));
        RebuildInformationCards();
    }

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
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
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
        RebuildInformationCards();
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
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
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
        RebuildInformationCards();
    }

    public void SetPreviewLoading(string statusText)
    {
        ResetPreviewState();
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
        PreviewStatusText = statusText;
        IsPreviewLoading = true;
        NotifyPreviewStateChanged();
        RebuildInformationCards();
    }

    public void ShowPreviewMessage(string statusText)
    {
        ResetPreviewState();
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
        PreviewStatusText = statusText;
        NotifyPreviewStateChanged();
        RebuildInformationCards();
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
        _previewCacheStatusText = NormalizePreviewInfoText(cacheStatusText);
        _previewFallbackReasonText = NormalizePreviewInfoText(fallbackReasonText);
        _previewDiagnosticDetailText = NormalizePreviewInfoText(diagnosticDetailText);
        RebuildInformationCards();
    }

    public void OverrideSizeText(string sizeText)
    {
        SizeText = string.IsNullOrWhiteSpace(sizeText)
            ? string.Empty
            : sizeText.Trim();
        RebuildInformationCards();
    }

    public void RestoreSelectedSection(string? sectionKey)
    {
        SelectedSectionIndex = GetSectionIndex(sectionKey);
    }

    public void ApplyImagePreview(
        ImageSource imageSource,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        ResetPreviewState();
        _previewKind = AssetPreviewKind.Image;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewImageSource = imageSource;
        NotifyPreviewStateChanged();
        RebuildInformationCards();
    }

    /// <summary>
    /// 音频流需要在预览存活期间保持底层随机访问流不被回收，
    /// 因此这里同时保存 <see cref="MediaSource"/> 和对应的源流句柄。
    /// </summary>
    public void ApplyAudioPreview(
        MediaSource audioSource,
        IRandomAccessStream audioStream,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AudioPreviewDisplayState? audioPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(audioSource);
        ArgumentNullException.ThrowIfNull(audioStream);

        ResetPreviewState();
        _previewKind = AssetPreviewKind.Audio;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        _previewAudioStream = audioStream;
        PreviewAudioSource = audioSource;
        ApplyAudioPreviewDisplayState(audioPreviewDisplayState);
        NotifyPreviewStateChanged();
        RebuildInformationCards();
    }

    public void ApplyTextPreview(
        string textContent,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        ResetPreviewState();
        _previewKind = AssetPreviewKind.Text;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewText = textContent;
        NotifyPreviewStateChanged();
        RebuildInformationCards();
    }

    public void ApplySummaryPreview(
        string summaryText,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        ResetPreviewState();
        _previewKind = AssetPreviewKind.Summary;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewText = summaryText;
        NotifyPreviewStateChanged();
        RebuildInformationCards();
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
        PreviewPayloadFilePath = string.Empty;
        PreviewImageSource = null;
        PreviewAudioSource = null;
        _previewAudioStream?.Dispose();
        _previewAudioStream = null;
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
        _previewCacheStatusText = string.Empty;
        _previewFallbackReasonText = string.Empty;
        _previewDiagnosticDetailText = string.Empty;
        _previewKind = AssetPreviewKind.None;
        ClearAudioPreviewPresentation();
        NotifyPreviewStateChanged();
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(PreviewLoadingVisibility));
        OnPropertyChanged(nameof(PreviewImageVisibility));
        OnPropertyChanged(nameof(PreviewTextVisibility));
        OnPropertyChanged(nameof(PreviewAudioVisibility));
        OnPropertyChanged(nameof(PreviewSummaryVisibility));
        OnPropertyChanged(nameof(PreviewMessageVisibility));
        OnPropertyChanged(nameof(PreviewSurfaceHeight));
        OnPropertyChanged(nameof(PreviewSurfaceMinHeight));
        NotifyAudioPreviewPresentationChanged();
    }

    /// <summary>
    /// 信息卡是对“当前资源基础信息 + 预览补充信息 + 预览诊断”的结构化投影。
    /// 每次状态变化时统一在这里重建，避免 XAML 自己解析文本或散落条件判断。
    /// </summary>
    private void RebuildInformationCards()
    {
        var expandedStates = CaptureCardExpandedStates();
        InformationCards.Clear();

        if (!HasSelection)
        {
            return;
        }

        InformationCards.Add(CreateBasicInfoCard(expandedStates));

        var previewInfoRows = _previewInfoRows;
        if (previewInfoRows.Count > 0)
        {
            InformationCards.Add(CreateCard(
                key: PreviewInfoCardKey,
                title: _informationTextSet.PreviewInfoTitle,
                rows: previewInfoRows,
                expandedStates: expandedStates,
                defaultExpanded: true));
        }

        var diagnosticRows = CreatePreviewDiagnosticRows();
        if (diagnosticRows.Count > 0)
        {
            InformationCards.Add(CreateCard(
                key: PreviewDiagnosticCardKey,
                title: _informationTextSet.PreviewDiagnosticTitle,
                rows: diagnosticRows,
                expandedStates: expandedStates,
                defaultExpanded: true));
        }
    }

    private Dictionary<string, bool> CaptureCardExpandedStates()
    {
        return InformationCards.ToDictionary(
            static card => card.Key,
            static card => card.IsExpanded,
            StringComparer.OrdinalIgnoreCase);
    }

    private InspectorInfoCardViewModel CreateBasicInfoCard(IReadOnlyDictionary<string, bool> expandedStates)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(_informationTextSet.AssetNameLabel, AssetName),
            new(_informationTextSet.AssetTypeLabel, AssetTypeText),
            new(_informationTextSet.BundleLabel, BundleDisplayName),
            new(_informationTextSet.AssetPathLabel, AssetPath),
            new(_informationTextSet.AssetSizeLabel, SizeText)
        };

        if (!string.IsNullOrWhiteSpace(ContainerPath))
        {
            rows.Add(new InspectorInfoRowViewModel(_informationTextSet.ContainerPathLabel, ContainerPath));
        }

        if (!string.IsNullOrWhiteSpace(PathIdText))
        {
            rows.Add(new InspectorInfoRowViewModel(_informationTextSet.PathIdLabel, PathIdText));
        }

        return CreateCard(
            key: BasicInfoCardKey,
            title: _informationTextSet.BasicInfoTitle,
            rows: rows,
            expandedStates: expandedStates,
            defaultExpanded: true);
    }

    private InspectorInfoCardViewModel CreateCard(
        string key,
        string title,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        IReadOnlyDictionary<string, bool> expandedStates,
        bool defaultExpanded)
    {
        var isExpanded = expandedStates.TryGetValue(key, out var existingExpanded)
            ? existingExpanded
            : defaultExpanded;

        return new InspectorInfoCardViewModel(
            key: key,
            title: title,
            copyToolTipText: _informationTextSet.CopyToolTipText,
            summaryText: _informationTextSet.FormatItemCount(rows.Count),
            isExpanded: isExpanded,
            rows: rows);
    }

    private List<InspectorInfoRowViewModel> CreatePreviewDiagnosticRows()
    {
        var rows = new List<InspectorInfoRowViewModel>();

        AddRowIfHasValue(rows, _informationTextSet.PreviewCacheStatusLabel, _previewCacheStatusText);
        AddRowIfHasValue(rows, _informationTextSet.PreviewFallbackReasonLabel, _previewFallbackReasonText);
        AddRowIfHasValue(rows, _informationTextSet.PreviewDiagnosticDetailLabel, _previewDiagnosticDetailText);

        return rows;
    }

    private static void AddRowIfHasValue(ICollection<InspectorInfoRowViewModel> rows, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        rows.Add(new InspectorInfoRowViewModel(label, value));
    }

    /// <summary>
    /// 分段切换条和内容区显隐都依赖同一组选中状态。
    /// 当前详情页已经把“概览 + 预览”合并到同一页，因此恢复时也要兼容旧版 overview / preview 键。
    /// </summary>
    private void NotifySectionSelectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectedSectionKey));
        OnPropertyChanged(nameof(IsDetailsSectionSelected));
        OnPropertyChanged(nameof(IsDependenciesSectionSelected));
        OnPropertyChanged(nameof(IsReplacementSectionSelected));
        OnPropertyChanged(nameof(IsChangesSectionSelected));
        OnPropertyChanged(nameof(DetailsSectionVisibility));
        OnPropertyChanged(nameof(DependenciesSectionVisibility));
        OnPropertyChanged(nameof(ReplacementSectionVisibility));
        OnPropertyChanged(nameof(ChangesSectionVisibility));
    }

    private static string NormalizePreviewInfoText(string? previewInfoText) =>
        string.IsNullOrWhiteSpace(previewInfoText)
            ? string.Empty
            : previewInfoText.Trim();

    private static string NormalizePreviewPayloadFilePath(string? previewPayloadFilePath) =>
        string.IsNullOrWhiteSpace(previewPayloadFilePath)
            ? string.Empty
            : previewPayloadFilePath.Trim();

    private static int GetSectionIndex(string? sectionKey)
    {
        return sectionKey?.Trim().ToLowerInvariant() switch
        {
            "overview" => 0,
            "preview" => 0,
            DependenciesSectionKey => 1,
            ReplacementSectionKey => 2,
            ChangesSectionKey => 3,
            _ => 0
        };
    }

    private static string GetSectionKey(int sectionIndex)
    {
        return NormalizeSectionIndex(sectionIndex) switch
        {
            1 => DependenciesSectionKey,
            2 => ReplacementSectionKey,
            3 => ChangesSectionKey,
            _ => DetailsSectionKey
        };
    }

    private static int NormalizeSectionIndex(int sectionIndex)
    {
        return sectionIndex switch
        {
            < 0 => 0,
            > 3 => 3,
            _ => sectionIndex
        };
    }

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

    private void ClearAudioPreviewPresentation()
    {
        AudioPreviewSizeSummaryText = string.Empty;
        AudioPreviewContainerBadgeText = string.Empty;
        AudioPreviewDurationBadgeText = string.Empty;
        AudioPreviewChannelBadgeText = string.Empty;
        AudioPreviewFrequencyBadgeText = string.Empty;
    }

    private void NotifyAudioPreviewPresentationChanged()
    {
        OnPropertyChanged(nameof(AudioPreviewSizeSummaryVisibility));
        OnPropertyChanged(nameof(AudioPreviewContainerBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewDurationBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewChannelBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewFrequencyBadgeVisibility));
    }

    private void ApplyAudioPreviewDisplayState(AudioPreviewDisplayState? audioPreviewDisplayState)
    {
        AudioPreviewSizeSummaryText = audioPreviewDisplayState?.SizeSummaryText ?? string.Empty;
        AudioPreviewContainerBadgeText = audioPreviewDisplayState?.ContainerBadgeText ?? string.Empty;
        AudioPreviewDurationBadgeText = audioPreviewDisplayState?.DurationBadgeText ?? string.Empty;
        AudioPreviewChannelBadgeText = audioPreviewDisplayState?.ChannelBadgeText ?? string.Empty;
        AudioPreviewFrequencyBadgeText = audioPreviewDisplayState?.FrequencyBadgeText ?? string.Empty;
    }

    private static IReadOnlyList<InspectorInfoRowViewModel> CloneRows(
        IReadOnlyList<InspectorInfoRowViewModel>? rows)
    {
        return rows is { Count: > 0 }
            ? rows.ToArray()
            : Array.Empty<InspectorInfoRowViewModel>();
    }
}
