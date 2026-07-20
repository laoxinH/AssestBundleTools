using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

public sealed partial class PluginManagementViewModel
{
    private IEnumerable<PluginManagementFilterOptionViewModel> CreateTypeFilters()
    {
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.All,
            GetString(ResourceKeys.PluginManagementTypeFilterAll));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.ImportDecryptor,
            GetString(ResourceKeys.PluginManagementTypeImportDecryptor));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.ExportProcessor,
            GetString(ResourceKeys.PluginManagementTypeExportProcessor));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.PostExport,
            GetString(ResourceKeys.PluginManagementTypePostExport));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Texture2D,
            GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Audio,
            GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Atlas,
            GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Skel,
            GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel));
    }

    private IEnumerable<PluginManagementFilterOptionViewModel> CreateStatusFilters()
    {
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.All,
            GetString(ResourceKeys.PluginManagementStatusFilterAll));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Available,
            GetString(ResourceKeys.PluginManagementStatusAvailable));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Warning,
            GetString(ResourceKeys.PluginManagementStatusWarning));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.Unavailable,
            GetString(ResourceKeys.PluginManagementStatusUnavailable));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.ReadOnly,
            GetString(ResourceKeys.PluginManagementBadgeReadOnly));
        yield return new PluginManagementFilterOptionViewModel(
            PluginManagementFilterKeys.UserManaged,
            GetString(ResourceKeys.PluginManagementBadgeUserManaged));
    }

    private void NotifyListState()
    {
        OnPropertyChanged(nameof(HasVisiblePlugins));
        OnPropertyChanged(nameof(HasVisibleDecryptorPlugins));
        OnPropertyChanged(nameof(HasVisibleResourceProcessorPlugins));
        OnPropertyChanged(nameof(HasVisiblePostExportPlugins));
        OnPropertyChanged(nameof(VisiblePluginCountText));
        OnPropertyChanged(nameof(VisibleDecryptorPluginCountText));
        OnPropertyChanged(nameof(VisibleResourceProcessorPluginCountText));
        OnPropertyChanged(nameof(VisiblePostExportPluginCountText));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(DecryptorGroupVisibility));
        OnPropertyChanged(nameof(ResourceProcessorGroupVisibility));
        OnPropertyChanged(nameof(PostExportGroupVisibility));
        NotifySelectionState();
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedPlugin));
        OnPropertyChanged(nameof(CanEditSelectedPlugin));
        OnPropertyChanged(nameof(CanDeleteSelectedPlugin));
        OnPropertyChanged(nameof(CanOpenSelectedPluginLocation));
        OnPropertyChanged(nameof(SelectedPluginIconGlyph));
        OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(DetailsEmptyVisibility));
    }
}
