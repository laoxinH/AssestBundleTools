using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private const int SpriteEditHistoryLimit = 20;

    private readonly List<WorkspaceSpriteEditGeometryDto> _spriteEditGeometryHistory = [];
    private int _spriteEditGeometryHistoryIndex = -1;
    private WorkspaceSpriteEditGeometryDto? _savedSpriteEditGeometry;
    private bool _isApplyingSpriteEditHistoryState;

    public bool HasUnsavedSpriteEditChanges =>
        IsSpriteEditModeActive &&
        !AreSpriteEditGeometriesEqual(CreateSpriteEditGeometry(), _savedSpriteEditGeometry);

    public bool CanSaveSpriteEdit => HasUnsavedSpriteEditChanges;

    public bool CanUndoSpriteEdit =>
        IsSpriteEditModeActive &&
        _spriteEditGeometryHistoryIndex > 0;

    public bool CanRedoSpriteEdit =>
        IsSpriteEditModeActive &&
        _spriteEditGeometryHistoryIndex >= 0 &&
        _spriteEditGeometryHistoryIndex < _spriteEditGeometryHistory.Count - 1;

    internal void UndoSpriteEdit()
    {
        if (!CanUndoSpriteEdit)
        {
            return;
        }

        ApplySpriteEditHistoryIndex(_spriteEditGeometryHistoryIndex - 1);
    }

    internal void RedoSpriteEdit()
    {
        if (!CanRedoSpriteEdit)
        {
            return;
        }

        ApplySpriteEditHistoryIndex(_spriteEditGeometryHistoryIndex + 1);
    }

    internal void MarkSpriteEditSaved()
    {
        _savedSpriteEditGeometry = CreateSpriteEditGeometry();
        NotifySpriteEditHistoryStateChanged();
    }

    internal void DiscardSpriteEditChanges()
    {
        if (_savedSpriteEditGeometry is null)
        {
            return;
        }

        ApplySpriteEditGeometrySnapshot(_savedSpriteEditGeometry);
        ResetSpriteEditHistory(_savedSpriteEditGeometry);
    }

    private void InitializeSpriteEditHistory()
    {
        var geometry = CreateSpriteEditGeometry();
        _savedSpriteEditGeometry = geometry;
        ResetSpriteEditHistory(geometry);
    }

    private void ClearSpriteEditHistory()
    {
        _spriteEditGeometryHistory.Clear();
        _spriteEditGeometryHistoryIndex = -1;
        _savedSpriteEditGeometry = null;
        _isApplyingSpriteEditHistoryState = false;
        NotifySpriteEditHistoryStateChanged();
    }

    private void ResetSpriteEditHistory(WorkspaceSpriteEditGeometryDto? geometry)
    {
        _spriteEditGeometryHistory.Clear();
        if (geometry is not null)
        {
            _spriteEditGeometryHistory.Add(geometry);
            _spriteEditGeometryHistoryIndex = 0;
        }
        else
        {
            _spriteEditGeometryHistoryIndex = -1;
        }

        NotifySpriteEditHistoryStateChanged();
    }

    private void RecordSpriteEditGeometryHistory()
    {
        if (_isApplyingSpriteEditHistoryState)
        {
            return;
        }

        var geometry = CreateSpriteEditGeometry();
        if (geometry is null)
        {
            NotifySpriteEditHistoryStateChanged();
            return;
        }

        if (_spriteEditGeometryHistoryIndex >= 0 &&
            _spriteEditGeometryHistoryIndex < _spriteEditGeometryHistory.Count &&
            AreSpriteEditGeometriesEqual(_spriteEditGeometryHistory[_spriteEditGeometryHistoryIndex], geometry))
        {
            NotifySpriteEditHistoryStateChanged();
            return;
        }

        if (_spriteEditGeometryHistoryIndex < _spriteEditGeometryHistory.Count - 1)
        {
            _spriteEditGeometryHistory.RemoveRange(
                _spriteEditGeometryHistoryIndex + 1,
                _spriteEditGeometryHistory.Count - _spriteEditGeometryHistoryIndex - 1);
        }

        _spriteEditGeometryHistory.Add(geometry);
        if (_spriteEditGeometryHistory.Count > SpriteEditHistoryLimit)
        {
            _spriteEditGeometryHistory.RemoveAt(0);
        }

        _spriteEditGeometryHistoryIndex = _spriteEditGeometryHistory.Count - 1;
        NotifySpriteEditHistoryStateChanged();
    }

    private void ApplySpriteEditHistoryIndex(int historyIndex)
    {
        if (historyIndex < 0 || historyIndex >= _spriteEditGeometryHistory.Count)
        {
            return;
        }

        ApplySpriteEditGeometrySnapshot(_spriteEditGeometryHistory[historyIndex]);
        _spriteEditGeometryHistoryIndex = historyIndex;
        NotifySpriteEditHistoryStateChanged();
    }

    private void ApplySpriteEditGeometrySnapshot(WorkspaceSpriteEditGeometryDto geometry)
    {
        _isApplyingSpriteEditHistoryState = true;
        try
        {
            ApplySpriteEditGeometry(
                geometry.RegionX,
                geometry.RegionY,
                geometry.RegionWidth,
                geometry.RegionHeight,
                geometry.PivotX,
                geometry.PivotY,
                refreshPivotRelativePosition: true,
                recordHistory: false);
        }
        finally
        {
            _isApplyingSpriteEditHistoryState = false;
        }
    }

    private void NotifySpriteEditHistoryStateChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedSpriteEditChanges));
        OnPropertyChanged(nameof(CanSaveSpriteEdit));
        OnPropertyChanged(nameof(CanUndoSpriteEdit));
        OnPropertyChanged(nameof(CanRedoSpriteEdit));
    }

    private SpriteEditSessionSnapshot? CaptureSpriteEditSessionSnapshot()
    {
        if (!_isSpriteEditModeRequested || !IsSpriteEditModeActive || !_overlayState.HasOverlay)
        {
            return null;
        }

        var currentGeometry = CreateSpriteEditGeometry();
        var hasUnsavedGeometry = !AreSpriteEditGeometriesEqual(currentGeometry, _savedSpriteEditGeometry);
        var hasTemporaryHistory = _spriteEditGeometryHistory.Count > 1;
        if (!hasUnsavedGeometry && !hasTemporaryHistory)
        {
            return null;
        }

        return new SpriteEditSessionSnapshot(
            currentGeometry,
            _savedSpriteEditGeometry,
            [.. _spriteEditGeometryHistory],
            _spriteEditGeometryHistoryIndex);
    }

    private void TryRestoreSpriteEditSessionSnapshot(SpriteEditSessionSnapshot? snapshot)
    {
        if (snapshot is null ||
            !_isSpriteEditModeRequested ||
            !IsSpriteEditModeActive ||
            !_overlayState.HasOverlay)
        {
            return;
        }

        var normalizedHistory = snapshot.History
            .Select(NormalizeSpriteEditGeometryForCurrentCanvas)
            .Where(static geometry => geometry is not null)
            .Select(static geometry => geometry!)
            .ToList();
        var normalizedCurrent = NormalizeSpriteEditGeometryForCurrentCanvas(snapshot.CurrentGeometry);
        if (normalizedCurrent is not null &&
            (normalizedHistory.Count == 0 ||
             !normalizedHistory.Any(geometry => AreSpriteEditGeometriesEqual(geometry, normalizedCurrent))))
        {
            normalizedHistory.Add(normalizedCurrent);
        }

        if (normalizedHistory.Count == 0)
        {
            return;
        }

        _savedSpriteEditGeometry = NormalizeSpriteEditGeometryForCurrentCanvas(snapshot.SavedGeometry);
        _spriteEditGeometryHistory.Clear();
        _spriteEditGeometryHistory.AddRange(normalizedHistory);
        _spriteEditGeometryHistoryIndex = Math.Clamp(
            snapshot.HistoryIndex,
            0,
            _spriteEditGeometryHistory.Count - 1);

        var activeGeometry = normalizedCurrent ?? _spriteEditGeometryHistory[_spriteEditGeometryHistoryIndex];
        ApplySpriteEditGeometrySnapshot(activeGeometry);

        var activeHistoryIndex = _spriteEditGeometryHistory.FindIndex(
            geometry => AreSpriteEditGeometriesEqual(geometry, activeGeometry));
        _spriteEditGeometryHistoryIndex = activeHistoryIndex >= 0
            ? activeHistoryIndex
            : _spriteEditGeometryHistoryIndex;
        NotifySpriteEditHistoryStateChanged();
    }

    private WorkspaceSpriteEditGeometryDto? NormalizeSpriteEditGeometryForCurrentCanvas(
        WorkspaceSpriteEditGeometryDto? geometry)
    {
        return geometry is null
            ? null
            : geometry with
            {
                CanvasWidth = NormalizePositive(CanvasWidth, fallback: 1d),
                CanvasHeight = NormalizePositive(CanvasHeight, fallback: 1d)
            };
    }

    private static bool AreSpriteEditGeometriesEqual(
        WorkspaceSpriteEditGeometryDto? left,
        WorkspaceSpriteEditGeometryDto? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return AreSpriteEditGeometryValuesClose(left.CanvasWidth, right.CanvasWidth) &&
               AreSpriteEditGeometryValuesClose(left.CanvasHeight, right.CanvasHeight) &&
               AreSpriteEditGeometryValuesClose(left.RegionX, right.RegionX) &&
               AreSpriteEditGeometryValuesClose(left.RegionY, right.RegionY) &&
               AreSpriteEditGeometryValuesClose(left.RegionWidth, right.RegionWidth) &&
               AreSpriteEditGeometryValuesClose(left.RegionHeight, right.RegionHeight) &&
               AreSpriteEditGeometryValuesClose(left.PivotX, right.PivotX) &&
               AreSpriteEditGeometryValuesClose(left.PivotY, right.PivotY);
    }

    private static bool AreSpriteEditGeometryValuesClose(double left, double right) =>
        Math.Abs(left - right) <= 0.0001d;

    private sealed record SpriteEditSessionSnapshot(
        WorkspaceSpriteEditGeometryDto? CurrentGeometry,
        WorkspaceSpriteEditGeometryDto? SavedGeometry,
        IReadOnlyList<WorkspaceSpriteEditGeometryDto> History,
        int HistoryIndex);
}
