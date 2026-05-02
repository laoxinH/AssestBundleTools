using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 维护资源包导入时左侧窗格内的进度展示状态。
/// </summary>
public sealed class WorkspaceImportProgressViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private bool _isActive;
    private WorkspaceImportProgressDto? _progress;

    public WorkspaceImportProgressViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(OverlayVisibility));
            }
        }
    }

    public Visibility OverlayVisibility => IsActive
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string ProgressText => BuildProgressText();

    public string DetailText => BuildDetailText();

    public bool IsProgressBarIndeterminate =>
        _progress is null ||
        _progress.Stage != WorkspaceImportProgressStage.ProcessingBundles ||
        _progress.TotalBundleCount <= 0;

    public double ProgressBarValue
    {
        get
        {
            if (_progress is not { TotalBundleCount: > 0 } progress)
            {
                return 0d;
            }

            return Math.Clamp(
                progress.CompletedBundleCount * 100d / progress.TotalBundleCount,
                0d,
                100d);
        }
    }

    public IProgress<WorkspaceImportProgressDto> CreateProgressReporter() =>
        new Progress<WorkspaceImportProgressDto>(UpdateProgress);

    public void Start()
    {
        _progress = new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.Preparing,
            CompletedBundleCount: 0,
            TotalBundleCount: 0,
            CurrentBundleLogicalPath: null);
        IsActive = true;
        NotifyProgressChanged();
    }

    public void Complete()
    {
        _progress = new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.Completed,
            _progress?.TotalBundleCount ?? 0,
            _progress?.TotalBundleCount ?? 0,
            CurrentBundleLogicalPath: null);
        NotifyProgressChanged();
        IsActive = false;
    }

    private void UpdateProgress(WorkspaceImportProgressDto progress)
    {
        _progress = progress;
        NotifyProgressChanged();
    }

    private void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(IsProgressBarIndeterminate));
        OnPropertyChanged(nameof(ProgressBarValue));
    }

    private string BuildProgressText()
    {
        if (_progress is not { } progress ||
            progress.Stage is WorkspaceImportProgressStage.Preparing or WorkspaceImportProgressStage.Planning)
        {
            return GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressPreparing);
        }

        if (progress.Stage == WorkspaceImportProgressStage.ProcessingBundles &&
            progress.TotalBundleCount > 0)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceImportBusyOverlayProgressProcessingFormat,
                progress.CompletedBundleCount,
                progress.TotalBundleCount);
        }

        if (progress.Stage == WorkspaceImportProgressStage.Committing)
        {
            return GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressCommitting);
        }

        return GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressCompleted);
    }

    private string BuildDetailText()
    {
        if (_progress?.CurrentBundleLogicalPath is { Length: > 0 } logicalPath)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceImportBusyOverlayProgressCurrentBundleFormat,
                logicalPath);
        }

        if (_progress is { Stage: WorkspaceImportProgressStage.ProcessingBundles, TotalBundleCount: > 0 })
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceImportBusyOverlayProgressPercentFormat,
                ProgressBarValue);
        }

        return GetString(ResourceKeys.WorkspaceImportBusyOverlayHint);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
