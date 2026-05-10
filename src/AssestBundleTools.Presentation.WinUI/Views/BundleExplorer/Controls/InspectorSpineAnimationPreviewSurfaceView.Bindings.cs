using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private void UpdateBindingsNow()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindingsNow);
            return;
        }

        _isBindingsUpdateQueued = false;
        Bindings.Update();
        ReconcileSelectorSelections();
    }

    private void QueueBindingsUpdate()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBindingsUpdate);
            return;
        }

        if (_isBindingsUpdateQueued)
        {
            return;
        }

        _isBindingsUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(UpdateBindingsNow))
        {
            _isBindingsUpdateQueued = false;
        }
    }

    private static bool ShouldUpdateBindings(string? propertyName) =>
        propertyName is not nameof(InspectorPreviewSpineAnimationViewModel.CurrentFrame);
}
