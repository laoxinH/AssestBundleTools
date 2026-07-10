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
        var windowCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceExportWindow.xaml.cs"));
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceExportDialogContent.xaml"));
        var contentCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceExportDialogContent.xaml.cs"));
        var viewModel = string.Concat(
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Dialogs",
                "WorkspaceExportDialogViewModel.cs")),
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Dialogs",
                "WorkspaceExportDialogViewModel.Bundles.cs")));
        var resourceKeys = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Localization",
            "ResourceKeys.cs"));
        var zhResources = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Strings",
            "zh-CN",
            "Resources.resw"));
        var enResources = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Strings",
            "en-US",
            "Resources.resw"));
        var optionFactory = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Dialogs",
            "WorkspaceExportDialogOptionFactory.cs"));

        Assert.Contains("var windowNotificationService = new AppNotificationService();", coordinator);
        Assert.Contains("new WorkspaceExportWindow(viewModel, windowNotificationService)", coordinator);
        Assert.Contains("App.CurrentApp.RegisterAuxiliaryWindow(", coordinator);
        Assert.Contains("_owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null", coordinator);
        Assert.Contains("GetWorkspaceSavePreviewAsync", coordinator);
        Assert.Contains("GetWorkspaceChangeSummaryAsync", coordinator);
        Assert.Contains("IResourceProcessorCatalogService", coordinator);
        Assert.Contains("_resourceProcessorCatalogService.GetEntriesAsync()", coordinator);
        Assert.Contains("processorCatalogEntries", coordinator);
        Assert.Contains("IFilePickerService", coordinator);
        Assert.Contains("IBackgroundWorkScheduler", coordinator);
        Assert.Contains("BackgroundWorkProfile.SaveRebuild", coordinator);
        Assert.Contains("_backgroundWorkScheduler.StartAsync", coordinator);
        Assert.Contains("_activeExportHandle?.Cancel()", coordinator);
        Assert.Contains("viewModel.ExportSettingsChanged += OnExportSettingsChanged;", coordinator);
        Assert.Contains("PersistWorkspaceExportSettings(viewModel.CaptureExportSettings());", coordinator);
        Assert.Contains("viewModel.ExportSettingsChanged -= OnExportSettingsChanged;", coordinator);
        Assert.Contains("PickWorkspaceExportOutputDirectoryAsync", coordinator);
        Assert.Contains("SaveWorkspaceAsNewBundleAsync", coordinator);
        Assert.Contains("Path.Combine(dataRootPath.Trim(), \"exports\")", coordinator);
        Assert.Contains("<shared:AppNotificationPresenter", windowXaml);
        Assert.Contains("Value=\"{Binding ExecutionProgressValue}\"", windowXaml);
        Assert.Contains("Visibility=\"{Binding ExecutionProgressVisibility}\"", windowXaml);
        Assert.Contains("IsEnabled=\"{Binding CanUseSecondaryButton}\"", windowXaml);
        Assert.Contains("IsEnabled=\"{Binding CanStartExport}\"", windowXaml);
        Assert.Contains("StartExportRequested", windowCode);
        Assert.Contains("CancelExportRequested", windowCode);
        Assert.Contains("Text=\"{Binding BundleSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding BundleSortOptions}\"", contentXaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedBundleSortOption, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding VisibleBundles}\"", contentXaml);
        Assert.Contains("Visibility=\"{Binding HeaderVisibility}\"", contentXaml);
        Assert.Contains("Visibility=\"{Binding BundleVisibility}\"", contentXaml);
        Assert.Contains("BundleEmptyStateTitleText", contentXaml);
        Assert.Contains("BundleEmptyStateDetailText", contentXaml);
        Assert.Contains("Visibility=\"{Binding BundleEmptyStateVisibility}\"", contentXaml);
        Assert.Contains("WorkspaceExportDialogBundleEmptyTitle", contentCode);
        Assert.Contains("WorkspaceExportDialogBundleEmptyDetail", contentCode);
        Assert.Contains("WorkspaceExportDialogBundleEmptyTitle", resourceKeys);
        Assert.Contains("WorkspaceExportDialogBundleEmptyDetail", resourceKeys);
        Assert.Contains("WorkspaceExportDialog.Bundle.EmptyTitle", zhResources);
        Assert.Contains("WorkspaceExportDialog.Bundle.EmptyDetail", zhResources);
        Assert.Contains("WorkspaceExportDialog.Bundle.EmptyTitle", enResources);
        Assert.Contains("WorkspaceExportDialog.Bundle.EmptyDetail", enResources);
        Assert.Contains("ItemsSource=\"{Binding ResourceProcessors}\"", contentXaml);
        Assert.Contains("IsChecked=\"{Binding UseSourceCodec, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("IsChecked=\"{Binding ExportToWorkspaceDirectory, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("WorkspaceExportDialogUseSourceCodec", contentCode);
        Assert.Contains("WorkspaceExportDialogUseSourceCodec", resourceKeys);
        Assert.Contains("WorkspaceExportDialog.UseSourceCodec", zhResources);
        Assert.Contains("WorkspaceExportDialog.UseSourceCodec", enResources);
        Assert.Contains("ItemsSource=\"{Binding Processors}\"", contentXaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedProcessor, Mode=TwoWay}\"", contentXaml);
        Assert.DoesNotContain("OutputDirectoryText", contentXaml);
        Assert.DoesNotContain("OutputDirectoryLabelText", contentXaml);
        Assert.Contains("ResourceProcessorSectionTitleText", contentXaml);
        Assert.DoesNotContain("x:Name=\"ProcessorSectionTitleText\"", contentXaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding RecentChanges}\"", contentXaml);
        Assert.DoesNotContain("RecentSectionTitleText", contentXaml);
        Assert.DoesNotContain("OutputSectionTitleText", contentXaml);
        Assert.Contains("RefreshPlanSummary();", viewModel);
        Assert.Contains("RefreshVisibleBundles();", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleGroupRecent", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleGroupPrevious", viewModel);
        Assert.Contains("BundleEmptyStateVisibility", viewModel);
        Assert.Contains("AppendTimeGroupedVisibleBundles", viewModel);
        Assert.Contains("bundle.DisplayName.Contains(query", viewModel);
        Assert.Contains("IReadOnlyList<ResourceProcessorCatalogEntryDto>? resourceProcessorCatalogEntries", viewModel);
        Assert.Contains("CreateSubmission", viewModel);
        Assert.Contains("WorkspaceExportDialogOptionFactory", viewModel);
        Assert.Contains("WorkspaceExportDialogBundleSortByTime", optionFactory);
        Assert.Contains("WorkspaceExportDialogBundleSortByName", optionFactory);
        Assert.Contains("IsCompatibleResourceProcessor", optionFactory);
        Assert.Contains("PluginManagementResourceProcessorDisplayNameFormat", optionFactory);
        Assert.Contains("ResolveProcessorDescription", optionFactory);
        Assert.Contains("WorkspaceExportDialogResourceProcessorTexture2D", optionFactory);
        Assert.Contains("WorkspaceExportDialogResourceProcessorAudio", optionFactory);
        Assert.Contains("WorkspaceExportDialogResourceProcessorAtlas", optionFactory);
        Assert.Contains("WorkspaceExportDialogResourceProcessorSkel", optionFactory);
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
