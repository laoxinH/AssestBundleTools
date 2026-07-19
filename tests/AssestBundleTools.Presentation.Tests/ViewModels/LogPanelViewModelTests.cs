using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class LogPanelViewModelTests
{
    [Fact]
    public void LevelFilter_ShouldProjectMatchingEntriesAndFilterEmptyState()
    {
        var viewModel = new LogPanelViewModel
        {
            EmptyStateTitleText = "empty-title",
            EmptyStateText = "empty",
            FilterEmptyStateTitleText = "filter-title",
            FilterEmptyStateText = "filter-empty"
        };
        viewModel.Append("info", WorkspaceDiagnosticLogLevel.Information);
        viewModel.Append("warning", WorkspaceDiagnosticLogLevel.Warning);
        viewModel.Append("error", WorkspaceDiagnosticLogLevel.Error);

        Assert.Equal(3, viewModel.FilteredEntries.Count);
        viewModel.SetFilter(LogPanelFilter.Warning);

        var warning = Assert.Single(viewModel.FilteredEntries);
        Assert.Equal(WorkspaceDiagnosticLogLevel.Warning, warning.Level);
        Assert.True(viewModel.IsWarningFilterSelected);
        Assert.Equal(Visibility.Visible, viewModel.EntriesVisibility);

        viewModel.Clear();

        Assert.Equal(Visibility.Visible, viewModel.EmptyStateVisibility);
        Assert.Equal("empty-title", viewModel.DisplayedEmptyStateTitleText);
        Assert.Equal("empty", viewModel.DisplayedEmptyStateText);
    }

    [Fact]
    public void CopyAll_ShouldIncludeEveryEntryRegardlessOfCurrentFilter()
    {
        var viewModel = new LogPanelViewModel();
        viewModel.Append("first\r\nline", WorkspaceDiagnosticLogLevel.Information);
        viewModel.Append("second", WorkspaceDiagnosticLogLevel.Error);
        viewModel.SetFilter(LogPanelFilter.Error);

        var copyText = viewModel.BuildCopyAllText();

        Assert.Single(viewModel.FilteredEntries);
        Assert.Contains("[Error] second", copyText);
        Assert.Contains("[Information] first line", copyText);
        Assert.Equal(2, copyText.Split(Environment.NewLine).Length);
    }

    [Fact]
    public void FilterWithoutMatches_ShouldUseFilterEmptyState()
    {
        var viewModel = new LogPanelViewModel
        {
            EmptyStateTitleText = "empty-title",
            EmptyStateText = "empty",
            FilterEmptyStateTitleText = "filter-title",
            FilterEmptyStateText = "filter-empty"
        };
        viewModel.Append("info", WorkspaceDiagnosticLogLevel.Information);

        viewModel.SetFilter(LogPanelFilter.Error);

        Assert.Empty(viewModel.FilteredEntries);
        Assert.Equal(Visibility.Visible, viewModel.EmptyStateVisibility);
        Assert.Equal("filter-title", viewModel.DisplayedEmptyStateTitleText);
        Assert.Equal("filter-empty", viewModel.DisplayedEmptyStateText);
        Assert.True(viewModel.CanCopyAll);
    }
}
