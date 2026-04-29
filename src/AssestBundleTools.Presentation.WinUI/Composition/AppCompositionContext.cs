using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 为组合根提供当前 App 实例掌握的运行期回调。
/// </summary>
internal sealed record AppCompositionContext(
    Func<Window?> ActiveWindowAccessor,
    EventHandler<WorkspaceProcessActivationRequestedEventArgs> WorkspaceActivationRequestedHandler);
