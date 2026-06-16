namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportBundleListItemViewModel
{
    public WorkspaceExportBundleListItemViewModel(
        WorkspaceExportBundleOptionViewModel? bundle,
        string? headerText = null)
    {
        Bundle = bundle;
        HeaderText = headerText ?? string.Empty;
    }

    public WorkspaceExportBundleOptionViewModel? Bundle { get; }

    public string HeaderText { get; }

    public bool IsHeader => Bundle is null;

    public bool IsBundle => Bundle is not null;

    public Microsoft.UI.Xaml.Visibility HeaderVisibility =>
        IsHeader ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility BundleVisibility =>
        IsBundle ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}
