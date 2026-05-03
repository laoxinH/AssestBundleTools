using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
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
    private BundleId _bundleId;
    private AssetId _assetId;
    private string _displayName;
    private string _toolTipText;
    private bool _canEdit;
    private long _previewRequestVersion;
    private AssetPreviewRequest? _activePreviewRequest;
    private bool _isRestoringPreviewImageDisplayOptions;

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
        PaneId = PaneId.New();
        Inspector = new InspectorPanelViewModel(
            inspectorInformationTextSet ?? throw new ArgumentNullException(nameof(inspectorInformationTextSet)));
        Inspector.Preview.Image.DisplayOptions.DisplayOptionsChanged += OnPreviewImageDisplayOptionsChanged;
    }

    public event EventHandler? PreviewImageDisplayOptionsChanged;

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

    public WorkspaceReplacementCandidateDto? PendingWorkspaceReplacementCandidate { get; private set; }

    public FileReplacementCandidateDto? PendingFileReplacementCandidate { get; private set; }

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

    public void RestoreSelectedInspectorSection(string? sectionKey)
    {
        Inspector.RestoreSelectedSection(sectionKey);
    }

    public void RestorePreviewImageDisplayOptions(
        string? backgroundMode,
        string? spriteDisplayMode)
    {
        _isRestoringPreviewImageDisplayOptions = true;
        try
        {
            Inspector.Preview.Image.DisplayOptions.RestoreDisplayOptions(
                backgroundMode,
                spriteDisplayMode);
        }
        finally
        {
            _isRestoringPreviewImageDisplayOptions = false;
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
}
