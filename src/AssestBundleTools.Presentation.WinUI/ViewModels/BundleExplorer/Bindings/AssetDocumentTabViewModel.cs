using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示右侧详情区域中的单个资产文档标签页。
/// </summary>
/// <remarks>
/// 该标签页只承载页面级浏览状态和当前交互候选，
/// 真正的资源事实仍然来自工作区会话与应用层查询结果。
/// </remarks>
public sealed class AssetDocumentTabViewModel : ObservableObject
{
    private static long s_previewOpenSequence;

    private BundleId _bundleId;
    private AssetId _assetId;
    private string _displayName;
    private string _toolTipText;
    private bool _canEdit;
    private readonly long _previewOpenSequence;
    private long _previewActivationSequence;
    private long _previewRequestVersion;
    private AssetPreviewRequest? _activePreviewRequest;
    private AssetDocumentEditDraftReference? _editDraftReference;
    private bool _isRestoringPreviewImageDisplayOptions;
    private bool _isRestoringPreviewMeshDisplayOptions;
    private bool _isSpriteEditModeRequested;

    public AssetDocumentTabViewModel(
        BundleId bundleId,
        AssetId assetId,
        string displayName,
        InspectorInformationTextSet inspectorInformationTextSet)
    {
        _bundleId = bundleId;
        _assetId = assetId;
        _displayName = string.IsNullOrWhiteSpace(displayName)
            ? assetId.Value
            : displayName.Trim();
        _toolTipText = _displayName;
        _previewOpenSequence = Interlocked.Increment(ref s_previewOpenSequence);
        PaneId = PaneId.New();
        Inspector = new InspectorPanelViewModel(
            inspectorInformationTextSet ?? throw new ArgumentNullException(nameof(inspectorInformationTextSet)));
        Inspector.SectionScrollPositionChanged += OnInspectorSectionScrollPositionChanged;
        Inspector.Preview.Image.DisplayOptions.DisplayOptionsChanged += OnPreviewImageDisplayOptionsChanged;
        Inspector.Preview.Image.ViewStateChanged += OnPreviewImageViewStateChanged;
        Inspector.Preview.Mesh.DisplayOptionsChanged += OnPreviewMeshDisplayOptionsChanged;
    }

    public event EventHandler? InspectorScrollStateChanged;

    public event EventHandler? PreviewImageDisplayOptionsChanged;

    public event EventHandler? PreviewImageViewStateChanged;

    public event EventHandler? PreviewMeshDisplayOptionsChanged;

    public BundleId BundleId
    {
        get => _bundleId;
        private set => SetProperty(ref _bundleId, value);
    }

    public AssetId AssetId
    {
        get => _assetId;
        private set => SetProperty(ref _assetId, value);
    }

    public PaneId PaneId { get; }

    public string DocumentId => AssetId.Value;

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        private set => SetProperty(ref _toolTipText, value);
    }

    public bool CanEdit
    {
        get => _canEdit;
        set => SetProperty(ref _canEdit, value);
    }

    public InspectorPanelViewModel Inspector { get; }

    public AssetDetailsDto? Details { get; private set; }

    internal AssetDetailsDto? PendingPreviewInvalidationBaseline { get; private set; }

    public WorkspaceReplacementCandidateDto? PendingWorkspaceReplacementCandidate { get; private set; }

    public FileReplacementCandidateDto? PendingFileReplacementCandidate { get; private set; }

    public AssetDocumentEditDraftReference? EditDraftReference => _editDraftReference;

    public bool HasEditDraft => _editDraftReference is not null;

    public bool IsSpriteEditModeRequested => _isSpriteEditModeRequested;

    internal long PreviewOpenSequence => _previewOpenSequence;

    internal long PreviewActivationSequence => Interlocked.Read(ref _previewActivationSequence);

    internal void MarkPreviewActivated(long sequence)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _previewActivationSequence);
            if (sequence <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _previewActivationSequence, sequence, current) == current)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 资产标签页会在运行期保留自己的详情和候选状态，
    /// 这样切换到其他资产标签后再回来时，不会丢失当前浏览上下文。
    /// </summary>
    public void ApplyDetails(AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        Details = details;
        DisplayName = string.IsNullOrWhiteSpace(details.Name)
            ? AssetId.Value
            : details.Name.Trim();
        ToolTipText = string.Format(
            CultureInfo.InvariantCulture,
            "{0} [{1}]",
            DisplayName,
            AssetId.Value);
    }

    internal void ApplyOperationProjection(AssetDetailsDto details)
    {
        PendingPreviewInvalidationBaseline ??= Details;
        ApplyDetails(details);
    }

    internal AssetDetailsDto? ConsumePreviewInvalidationBaseline()
    {
        var baseline = PendingPreviewInvalidationBaseline;
        PendingPreviewInvalidationBaseline = null;
        return baseline;
    }

    public void UpdatePendingWorkspaceReplacementCandidate(WorkspaceReplacementCandidateDto? candidate)
    {
        PendingWorkspaceReplacementCandidate = candidate;
        if (candidate is not null)
        {
            PendingFileReplacementCandidate = null;
        }
    }

    public void UpdatePendingFileReplacementCandidate(FileReplacementCandidateDto? candidate)
    {
        PendingFileReplacementCandidate = candidate;
        if (candidate is not null)
        {
            PendingWorkspaceReplacementCandidate = null;
        }
    }

    public void ClearPendingReplacementCandidates()
    {
        PendingWorkspaceReplacementCandidate = null;
        PendingFileReplacementCandidate = null;
    }

    public void SetEditDraftReference(AssetDocumentEditDraftReference? draftReference)
    {
        if (Equals(_editDraftReference, draftReference))
        {
            return;
        }

        _editDraftReference = draftReference;
        OnPropertyChanged(nameof(EditDraftReference));
        OnPropertyChanged(nameof(HasEditDraft));
    }

    public void RestoreEditDraftReference(AssetDocumentEditDraftReference? draftReference)
    {
        SetEditDraftReference(draftReference);
    }

    public void ClearEditDraftReference()
    {
        SetEditDraftReference(null);
    }

    public void RequestSpriteEditMode()
    {
        if (!_isSpriteEditModeRequested)
        {
            _isSpriteEditModeRequested = true;
            OnPropertyChanged(nameof(IsSpriteEditModeRequested));
        }

        Inspector.Preview.Image.RequestSpriteEditMode();
    }

    public void CancelSpriteEditMode()
    {
        if (_isSpriteEditModeRequested)
        {
            _isSpriteEditModeRequested = false;
            OnPropertyChanged(nameof(IsSpriteEditModeRequested));
        }

        Inspector.Preview.Image.CancelSpriteEditMode();
    }

    internal void TryApplySpriteEditModeRequest()
    {
        if (_isSpriteEditModeRequested)
        {
            Inspector.Preview.Image.RequestSpriteEditMode();
        }
    }

    public void RestoreSelectedInspectorSection(string? sectionKey)
    {
        Inspector.RestoreSelectedSection(sectionKey);
    }

    public void RestoreInspectorScrollState(WorkspaceInspectorScrollStateDto? scrollState)
    {
        Inspector.RestoreInspectorScrollState(scrollState);
    }

    public void RestorePreviewImageDisplayOptions(
        string? backgroundMode,
        string? spriteDisplayMode,
        double? zoom = null,
        double? panX = null,
        double? panY = null)
    {
        _isRestoringPreviewImageDisplayOptions = true;
        try
        {
            Inspector.Preview.Image.DisplayOptions.RestoreDisplayOptions(
                backgroundMode,
                spriteDisplayMode);
            Inspector.Preview.Image.RestoreViewState(zoom, panX, panY);
        }
        finally
        {
            _isRestoringPreviewImageDisplayOptions = false;
        }
    }

    public void RestorePreviewMeshDisplayOptions(
        string? displayMode,
        bool? textureOverlaySelected,
        bool? meshGridSelected,
        double? zoom,
        double? rotationX,
        double? rotationY)
    {
        _isRestoringPreviewMeshDisplayOptions = true;
        try
        {
            Inspector.Preview.Mesh.RestoreDisplayOptions(
                displayMode,
                textureOverlaySelected,
                meshGridSelected,
                zoom,
                rotationX,
                rotationY);
        }
        finally
        {
            _isRestoringPreviewMeshDisplayOptions = false;
        }
    }

    internal AssetPreviewRequest BeginPreviewRequest()
    {
        _previewRequestVersion++;
        _activePreviewRequest?.Cancel();
        _activePreviewRequest = new AssetPreviewRequest(_previewRequestVersion);
        return _activePreviewRequest;
    }

    internal void CancelPreviewRequest()
    {
        _previewRequestVersion++;
        _activePreviewRequest?.Cancel();
        _activePreviewRequest = null;
        if (Inspector.Preview.IsPreviewLoading)
        {
            Inspector.Preview.ResetState();
        }
    }

    internal void InvalidateHostBoundPreviewForExternalHostTransfer()
    {
        if (Details?.AssetType != AssetType.AudioClip &&
            Inspector.Preview.PreviewAudioSource is null)
        {
            return;
        }

        CancelPreviewRequest();
        Inspector.Preview.ResetState();
    }

    internal void CompletePreviewRequest(AssetPreviewRequest previewRequest)
    {
        ArgumentNullException.ThrowIfNull(previewRequest);

        if (ReferenceEquals(_activePreviewRequest, previewRequest))
        {
            _activePreviewRequest = null;
        }

        previewRequest.Dispose();
    }

    internal bool IsPreviewRequestCurrent(AssetDetailsDto details, AssetPreviewRequest previewRequest)
    {
        ArgumentNullException.ThrowIfNull(previewRequest);

        return ReferenceEquals(_activePreviewRequest, previewRequest) &&
               _previewRequestVersion == previewRequest.Version &&
               !previewRequest.CancellationToken.IsCancellationRequested &&
               Details is not null &&
               Details.BundleId.Equals(details.BundleId) &&
               Details.AssetId.Equals(details.AssetId);
    }

    private void OnPreviewImageDisplayOptionsChanged(object? sender, EventArgs e)
    {
        if (_isRestoringPreviewImageDisplayOptions)
        {
            return;
        }

        PreviewImageDisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnInspectorSectionScrollPositionChanged(object? sender, EventArgs e)
    {
        InspectorScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewImageViewStateChanged(object? sender, EventArgs e)
    {
        if (_isRestoringPreviewImageDisplayOptions)
        {
            return;
        }

        PreviewImageViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewMeshDisplayOptionsChanged(object? sender, EventArgs e)
    {
        if (_isRestoringPreviewMeshDisplayOptions)
        {
            return;
        }

        PreviewMeshDisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
