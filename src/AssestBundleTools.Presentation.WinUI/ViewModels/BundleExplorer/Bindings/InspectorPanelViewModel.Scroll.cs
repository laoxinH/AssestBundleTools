using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal readonly record struct InspectorPanelScrollPosition(
    double HorizontalOffset,
    double VerticalOffset);

public sealed partial class InspectorPanelViewModel
{
    private InspectorPanelScrollPosition _sectionSelectorScrollPosition;
    private InspectorPanelScrollPosition _detailsSectionScrollPosition;
    private InspectorPanelScrollPosition _dependenciesSectionScrollPosition;
    private InspectorPanelScrollPosition _replacementSectionScrollPosition;
    private InspectorPanelScrollPosition _changesSectionScrollPosition;

    internal event EventHandler? SectionScrollPositionChanged;

    internal InspectorPanelScrollPosition GetSectionSelectorScrollPosition() =>
        _sectionSelectorScrollPosition;

    internal void CaptureSectionSelectorScrollPosition(
        double horizontalOffset,
        double verticalOffset)
    {
        var position = CreateNormalizedScrollPosition(horizontalOffset, verticalOffset);
        if (position.Equals(_sectionSelectorScrollPosition))
        {
            return;
        }

        _sectionSelectorScrollPosition = position;
        SectionScrollPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal InspectorPanelScrollPosition GetSectionScrollPosition(int sectionIndex)
    {
        return NormalizeSectionIndex(sectionIndex) switch
        {
            1 => _dependenciesSectionScrollPosition,
            2 => _replacementSectionScrollPosition,
            3 => _changesSectionScrollPosition,
            _ => _detailsSectionScrollPosition
        };
    }

    internal void CaptureSectionScrollPosition(
        int sectionIndex,
        double horizontalOffset,
        double verticalOffset)
    {
        var position = CreateNormalizedScrollPosition(horizontalOffset, verticalOffset);

        var previousPosition = GetSectionScrollPosition(sectionIndex);
        if (position.Equals(previousPosition))
        {
            return;
        }

        switch (NormalizeSectionIndex(sectionIndex))
        {
            case 1:
                _dependenciesSectionScrollPosition = position;
                break;
            case 2:
                _replacementSectionScrollPosition = position;
                break;
            case 3:
                _changesSectionScrollPosition = position;
                break;
            default:
                _detailsSectionScrollPosition = position;
                break;
        }

        SectionScrollPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal WorkspaceInspectorScrollStateDto? CaptureInspectorScrollState()
    {
        var state = new WorkspaceInspectorScrollStateDto(
            DetailsSection: CreateSectionScrollPositionDto(_detailsSectionScrollPosition),
            DependenciesSection: CreateSectionScrollPositionDto(_dependenciesSectionScrollPosition),
            ReplacementSection: CreateSectionScrollPositionDto(_replacementSectionScrollPosition),
            ChangesSection: CreateSectionScrollPositionDto(_changesSectionScrollPosition),
            SectionSelector: CreateSectionScrollPositionDto(_sectionSelectorScrollPosition));

        return state is
        {
            DetailsSection: null,
            DependenciesSection: null,
            ReplacementSection: null,
            ChangesSection: null,
            SectionSelector: null
        }
            ? null
            : state;
    }

    internal void RestoreInspectorScrollState(WorkspaceInspectorScrollStateDto? scrollState)
    {
        if (scrollState is null)
        {
            ResetSectionScrollPositions();
            return;
        }

        _sectionSelectorScrollPosition = CreateScrollPosition(scrollState.SectionSelector);
        _detailsSectionScrollPosition = CreateScrollPosition(scrollState.DetailsSection);
        _dependenciesSectionScrollPosition = CreateScrollPosition(scrollState.DependenciesSection);
        _replacementSectionScrollPosition = CreateScrollPosition(scrollState.ReplacementSection);
        _changesSectionScrollPosition = CreateScrollPosition(scrollState.ChangesSection);
    }

    private void ResetSectionScrollPositions()
    {
        _sectionSelectorScrollPosition = default;
        _detailsSectionScrollPosition = default;
        _dependenciesSectionScrollPosition = default;
        _replacementSectionScrollPosition = default;
        _changesSectionScrollPosition = default;
    }

    private static double NormalizeScrollOffset(double offset)
    {
        return double.IsFinite(offset) && offset > 0d
            ? offset
            : 0d;
    }

    private static InspectorPanelScrollPosition CreateNormalizedScrollPosition(
        double horizontalOffset,
        double verticalOffset)
    {
        return new InspectorPanelScrollPosition(
            NormalizeScrollOffset(horizontalOffset),
            NormalizeScrollOffset(verticalOffset));
    }

    private static WorkspaceInspectorSectionScrollPositionDto? CreateSectionScrollPositionDto(
        InspectorPanelScrollPosition position)
    {
        return position is { HorizontalOffset: <= 0d, VerticalOffset: <= 0d }
            ? null
            : new WorkspaceInspectorSectionScrollPositionDto(
                position.HorizontalOffset,
                position.VerticalOffset);
    }

    private static InspectorPanelScrollPosition CreateScrollPosition(
        WorkspaceInspectorSectionScrollPositionDto? position)
    {
        return position is null
            ? default
            : new InspectorPanelScrollPosition(
                NormalizeScrollOffset(position.HorizontalOffset),
                NormalizeScrollOffset(position.VerticalOffset));
    }
}
