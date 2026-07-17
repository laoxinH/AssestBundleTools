using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed class AssetDragExportListView : ListView
{
    private InputCursor? _dragCursor;

    internal void SetElevatedDragFeedback(bool isActive)
    {
        if (isActive)
        {
            _dragCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            ProtectedCursor = _dragCursor;
            return;
        }

        ProtectedCursor = null;
    }
}
