using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class WorkspaceWindowSizePreferenceMapper
{
    public static DpiAwareWindowSize? ToWindowSize(WorkspaceWindowSizeDto? size) =>
        size is { Width: > 0d, Height: > 0d } &&
        double.IsFinite(size.Width) &&
        double.IsFinite(size.Height)
            ? new DpiAwareWindowSize(size.Width, size.Height)
            : null;

    public static WorkspaceWindowSizeDto? ToWorkspaceSize(DpiAwareWindowSize? size) =>
        size is { IsValid: true } value
            ? new WorkspaceWindowSizeDto(value.Width, value.Height)
            : null;
}
