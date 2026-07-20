using System;
using System.Linq;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceExportDialogViewModel
{
    private WorkspaceExportPostPluginOptionViewModel? _selectedPostExportPlugin;

    public WorkspaceExportPostPluginOptionViewModel? SelectedPostExportPlugin
    {
        get => _selectedPostExportPlugin;
        set
        {
            if (SetProperty(ref _selectedPostExportPlugin, value))
            {
                RaiseExportSettingsChanged();
            }
        }
    }

    private string? CapturePostExportPluginId() =>
        SelectedPostExportPlugin?.IsNone == false
            ? SelectedPostExportPlugin.Id
            : null;

    private void RestorePostExportPluginSelection(string? pluginId)
    {
        SelectedPostExportPlugin = string.IsNullOrWhiteSpace(pluginId)
            ? PostExportPluginOptions.FirstOrDefault(static option => option.IsNone)
            : PostExportPluginOptions.FirstOrDefault(option =>
                string.Equals(option.Id, pluginId, StringComparison.OrdinalIgnoreCase))
              ?? PostExportPluginOptions.FirstOrDefault(static option => option.IsNone);
    }
}
