namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceExportWindowContractTests
{
    [Fact]
    public void SaveAsBundleToolbarCommand_ShouldOpenWorkspaceExportWindow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml.cs"));

        Assert.Contains("private readonly BundleExplorerWorkspaceExportCoordinator _workspaceExportCoordinator;", pageCode);
        Assert.Contains("new BundleExplorerWorkspaceExportCoordinator(", pageCode);
        Assert.Contains("private async void OnSaveAsBundleChipButtonClick", pageCode);
        Assert.Contains("await _workspaceExportCoordinator.ShowAsync();", pageCode);
    }

    [Fact]
    public void WorkspaceExport_ShouldUseIndependentWindowWithProcessorSelection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceExportCoordinator.cs"));
        var windowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceExportWindow.xaml"));
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceExportDialogContent.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Dialogs",
            "WorkspaceExportDialogViewModel.cs"));

        Assert.Contains("var windowNotificationService = new AppNotificationService();", coordinator);
        Assert.Contains("new WorkspaceExportWindow(viewModel, windowNotificationService)", coordinator);
        Assert.Contains("App.CurrentApp.RegisterAuxiliaryWindow(", coordinator);
        Assert.Contains("_owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null", coordinator);
        Assert.Contains("GetWorkspaceSavePreviewAsync", coordinator);
        Assert.Contains("GetWorkspaceChangeSummaryAsync", coordinator);
        Assert.Contains("IResourceProcessorCatalogService", coordinator);
        Assert.Contains("_resourceProcessorCatalogService.GetEntriesAsync()", coordinator);
        Assert.Contains("processorCatalogEntries", coordinator);
        Assert.Contains("<shared:AppNotificationPresenter", windowXaml);
        Assert.Contains("Text=\"{Binding BundleSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding BundleSortOptions}\"", contentXaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedBundleSortOption, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding VisibleBundles}\"", contentXaml);
        Assert.Contains("Visibility=\"{Binding HeaderVisibility}\"", contentXaml);
        Assert.Contains("Visibility=\"{Binding BundleVisibility}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding ResourceProcessors}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding Processors}\"", contentXaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedProcessor, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("ResourceProcessorSectionTitleText", contentXaml);
        Assert.DoesNotContain("x:Name=\"ProcessorSectionTitleText\"", contentXaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding RecentChanges}\"", contentXaml);
        Assert.DoesNotContain("RecentSectionTitleText", contentXaml);
        Assert.DoesNotContain("OutputSectionTitleText", contentXaml);
        Assert.Contains("RefreshPlanSummary();", viewModel);
        Assert.Contains("RefreshVisibleBundles();", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleSortByTime", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleSortByName", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleGroupRecent", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleGroupPrevious", viewModel);
        Assert.Contains("AppendTimeGroupedVisibleBundles", viewModel);
        Assert.Contains("bundle.DisplayName.Contains(query", viewModel);
        Assert.Contains("IReadOnlyList<ResourceProcessorCatalogEntryDto>? resourceProcessorCatalogEntries", viewModel);
        Assert.Contains("IsCompatibleResourceProcessor", viewModel);
        Assert.Contains("WorkspaceExportDialogProcessorCliPlaceholder", viewModel);
        Assert.Contains("WorkspaceExportDialogResourceProcessorTexture2D", viewModel);
        Assert.Contains("WorkspaceExportDialogResourceProcessorAudio", viewModel);
        Assert.Contains("WorkspaceExportDialogResourceProcessorAtlas", viewModel);
        Assert.Contains("WorkspaceExportDialogResourceProcessorSkel", viewModel);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "AssestBundleTools.Presentation.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
