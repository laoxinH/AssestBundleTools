using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPanelViewModel
{
    private const string DetailsSectionKey = "details";
    private const string DependenciesSectionKey = "dependencies";
    private const string ReplacementSectionKey = "replacement";
    private const string ChangesSectionKey = "changes";

    private bool _isReplacementSectionAvailable;
    private int _selectedSectionIndex;

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

    public bool IsReplacementSectionAvailable
    {
        get => _isReplacementSectionAvailable;
        private set
        {
            if (!SetProperty(ref _isReplacementSectionAvailable, value))
            {
                return;
            }

            if (!value && SelectedSectionIndex == 2)
            {
                SelectedSectionIndex = 0;
            }
            else
            {
                NotifySectionSelectionStateChanged();
            }
        }
    }

    public Visibility DetailsSectionVisibility => SelectedSectionIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependenciesSectionVisibility => SelectedSectionIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReplacementSectionTabVisibility =>
        IsReplacementSectionAvailable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReplacementSectionVisibility =>
        IsReplacementSectionAvailable && SelectedSectionIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChangesSectionVisibility => SelectedSectionIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

    public void RestoreSelectedSection(string? sectionKey)
    {
        SelectedSectionIndex = GetSectionIndex(sectionKey);
    }

    public void SelectReplacementSection()
    {
        SelectedSectionIndex = GetSectionIndex(ReplacementSectionKey);
    }

    public void SetReplacementSectionAvailability(bool isAvailable)
    {
        IsReplacementSectionAvailable = isAvailable;
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
        OnPropertyChanged(nameof(ReplacementSectionTabVisibility));
        OnPropertyChanged(nameof(ReplacementSectionVisibility));
        OnPropertyChanged(nameof(ChangesSectionVisibility));
    }

    private int GetSectionIndex(string? sectionKey)
    {
        var sectionIndex = sectionKey?.Trim().ToLowerInvariant() switch
        {
            "overview" => 0,
            "preview" => 0,
            DependenciesSectionKey => 1,
            ReplacementSectionKey => 2,
            ChangesSectionKey => 3,
            _ => 0
        };

        return NormalizeSectionIndex(sectionIndex);
    }

    private string GetSectionKey(int sectionIndex)
    {
        return NormalizeSectionIndex(sectionIndex) switch
        {
            1 => DependenciesSectionKey,
            2 => ReplacementSectionKey,
            3 => ChangesSectionKey,
            _ => DetailsSectionKey
        };
    }

    private int NormalizeSectionIndex(int sectionIndex)
    {
        return sectionIndex switch
        {
            < 0 => 0,
            2 when !IsReplacementSectionAvailable => 0,
            > 3 => 3,
            _ => sectionIndex
        };
    }
}
