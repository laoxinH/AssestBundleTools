namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceBatchExtractionDialogContractTests
{
    [Fact]
    public void BundleExplorerTopBatchExtractCommand_ShouldOpenWorkspaceDialog()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml.cs"));

        Assert.Contains("Click=\"OnWorkspaceBatchExtractChipButtonClick\"", xaml);
        Assert.Contains("private async void OnWorkspaceBatchExtractChipButtonClick", codeBehind);
        Assert.Contains("_workspaceBatchExtractionCoordinator.ShowAsync()", codeBehind);
    }

    [Fact]
    public void WorkspaceBatchExtraction_ShouldUseIndependentWindow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));
        var windowXamlPath = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml");

        Assert.True(File.Exists(windowXamlPath));
        Assert.Contains("new WorkspaceBatchExtractionWindow(viewModel, windowNotificationService)", coordinator);
        Assert.Contains("App.CurrentApp.RegisterAuxiliaryWindow(", coordinator);
        Assert.Contains("_owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null", coordinator);
        Assert.Contains("ShowForSubmissionAsync()", coordinator);
        Assert.DoesNotContain("ContentDialog", coordinator);
    }

    [Fact]
    public void WorkspaceBatchReplacement_ShouldRegisterIndependentWindowWithMainOwner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appProjectRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var appCode = string.Concat(
            File.ReadAllText(Path.Combine(appProjectRoot, "App.xaml.cs")),
            File.ReadAllText(Path.Combine(appProjectRoot, "App.WorkspaceWindows.cs")));
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchReplacementCoordinator.cs"));

        Assert.Contains("new WorkspaceBatchReplacementWindow(viewModel, windowNotificationService)", coordinator);
        Assert.Contains("App.CurrentApp.RegisterAuxiliaryWindow(", coordinator);
        Assert.Contains("_owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null", coordinator);
        Assert.Contains("_auxiliaryWindowOwners", appCode);
        Assert.Contains("CloseAuxiliaryWindowsOwnedBy(mainWindow);", appCode);
    }

    [Fact]
    public void WorkspaceBatchWindows_ShouldUseLocalNotificationPresenter()
    {
        var repositoryRoot = FindRepositoryRoot();
        var extractionCoordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));
        var replacementCoordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchReplacementCoordinator.cs"));
        var extractionWindowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml"));
        var replacementWindowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchReplacementWindow.xaml"));
        var extractionWindowCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml.cs"));
        var replacementWindowCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchReplacementWindow.xaml.cs"));

        Assert.Contains("var windowNotificationService = new AppNotificationService();", extractionCoordinator);
        Assert.Contains("var windowNotificationService = new AppNotificationService();", replacementCoordinator);
        Assert.Contains("_activeBatchExtractionWindow?.NotificationService ?? _appNotificationService", extractionCoordinator);
        Assert.Contains("_activeWindow?.NotificationService ?? _appNotificationService", replacementCoordinator);
        Assert.Contains("<shared:AppNotificationPresenter", extractionWindowXaml);
        Assert.Contains("<shared:AppNotificationPresenter", replacementWindowXaml);
        Assert.Contains("NotificationService=\"{x:Bind NotificationService, Mode=OneWay}\"", extractionWindowXaml);
        Assert.Contains("NotificationService=\"{x:Bind NotificationService, Mode=OneWay}\"", replacementWindowXaml);
        Assert.Contains("public IAppNotificationService NotificationService { get; }", extractionWindowCode);
        Assert.Contains("public IAppNotificationService NotificationService { get; }", replacementWindowCode);
    }

    [Fact]
    public void WorkspaceBatchReplacementSourceHelp_ShouldUseIconButtonAndImmediateHoverTip()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchReplacementDialogContent.xaml"));
        var contentCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchReplacementDialogContent.xaml.cs"));

        Assert.Contains("BatchReplacementHelpButtonStyle", contentXaml);
        Assert.Contains("SourceSectionTitleIcon", contentXaml);
        Assert.Contains("BatchReplacementCardTitleIconStyle", contentXaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"16\" />", contentXaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"16\" />", contentXaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"{ThemeResource SubtleFillColorTransparentBrush}\" />", contentXaml);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", contentXaml);
        Assert.Contains("PointerEntered=\"OnSourceSectionHelpButtonPointerEntered\"", contentXaml);
        Assert.Contains("PointerExited=\"OnSourceSectionHelpButtonPointerExited\"", contentXaml);
        Assert.Contains("FontIcon", contentXaml);
        Assert.Contains("FontSize=\"9\"", contentXaml);
        Assert.Contains("Glyph=\"&#xEF07;\"", contentXaml);
        Assert.DoesNotContain("Text=\"?\"", contentXaml);
        Assert.Contains("IsEnabled=\"{Binding HasBundles}\"", contentXaml);
        Assert.Contains("_sourceSectionHelpToolTip.IsOpen = true;", contentCode);
        Assert.Contains("_sourceSectionHelpToolTip.IsOpen = false;", contentCode);
        Assert.Contains("TextWrapping = TextWrapping.WrapWholeWords", contentCode);
    }

    [Fact]
    public void WorkspaceBatchExtractionLayout_ShouldExposeTitleIconAndEmptyStateDetail()
    {
        var repositoryRoot = FindRepositoryRoot();
        var windowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml"));
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchExtractionDialogContent.xaml"));
        var contentCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchExtractionDialogContent.xaml.cs"));
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

        Assert.Contains("AccentFillColorDefaultBrush", windowXaml);
        Assert.Contains("TextOnAccentFillColorPrimaryBrush", windowXaml);
        Assert.Contains("Glyph=\"&#xE6E9;\"", windowXaml);
        Assert.Contains("BatchExtractionCardTitleIconStyle", contentXaml);
        Assert.Contains("BundleEmptyStateTitleText", contentXaml);
        Assert.Contains("BundleEmptyStateDetailText", contentXaml);
        Assert.Contains("Glyph=\"&#xE8EB;\"", contentXaml);
        Assert.Contains("WorkspaceBatchExtractionDialogBundleEmptyDetail", contentCode);
        Assert.Contains("WorkspaceBatchExtractionDialogBundleEmptyDetail", resourceKeys);
        Assert.Contains("WorkspaceBatchExtractionDialog.Bundle.EmptyDetail", zhResources);
        Assert.Contains("WorkspaceBatchExtractionDialog.Bundle.EmptyDetail", enResources);
    }

    [Fact]
    public void WorkspaceBatchExtractionPreview_ShouldBeDeferredAndRunAsSilentTransientRead()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));

        var showCall = coordinator.IndexOf(
            "var submissionTask = window.ShowForSubmissionAsync();",
            StringComparison.Ordinal);
        var deferredPreviewCall = coordinator.IndexOf(
            "SchedulePreviewRefreshAfterWindowShown",
            showCall,
            StringComparison.Ordinal);
        var awaitSubmission = coordinator.IndexOf(
            "var submission = await submissionTask;",
            showCall,
            StringComparison.Ordinal);

        Assert.True(showCall >= 0);
        Assert.True(deferredPreviewCall > showCall);
        Assert.True(awaitSubmission > deferredPreviewCall);
        Assert.Contains("DispatcherQueuePriority.Low", coordinator);
        Assert.Contains("new BackgroundWorkRequest<Result<WorkspaceBatchExtractionPreviewDto>>", coordinator);
        Assert.Contains("BackgroundWorkProfile.TransientRead", coordinator);
        Assert.Contains("SemanticKind = BackgroundWorkSemanticKind.TransientRead", coordinator);
        Assert.Contains("ShowInStatusBar = false", coordinator);
        Assert.Contains("CancelPendingPreviewRefresh();", coordinator);
    }

    [Fact]
    public void WorkspaceBatchExtractionCancellation_ShouldUseSilentNonFailureFeedback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));
        var workspaceCancellationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "design",
            "workspace-batch-extraction-dialog.md"));

        var executionStart = coordinator.IndexOf(
            "private async Task ExecuteSubmittedExtractionAsync",
            StringComparison.Ordinal);
        var cancellationCatch = coordinator.IndexOf(
            "catch (OperationCanceledException)",
            executionStart,
            StringComparison.Ordinal);
        var unexpectedCatch = coordinator.IndexOf(
            "CommonUnexpectedFailure",
            executionStart,
            StringComparison.Ordinal);
        var resultCancellation = coordinator.IndexOf(
            "if (result.IsCancelled)",
            executionStart,
            StringComparison.Ordinal);
        var successMessage = coordinator.IndexOf(
            "WorkspaceBatchExtractionDialogCompletedMessageFormat",
            executionStart,
            StringComparison.Ordinal);

        Assert.True(cancellationCatch > executionStart);
        Assert.True(unexpectedCatch > cancellationCatch);
        Assert.True(resultCancellation > unexpectedCatch);
        Assert.True(successMessage > resultCancellation);
        Assert.DoesNotContain("HandleExtractionCancelled", coordinator);
        Assert.DoesNotContain("WorkspaceBatchExtractionDialogCancelledMessage", coordinator);
        Assert.Contains("不额外弹出成功 / 失败 / 信息 toast", workspaceCancellationDoc);
    }

    [Fact]
    public void WindowContent_ShouldExposeBundleCategoryAndDeferredOutputSelections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchExtractionDialogContent.xaml"));
        var windowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml"));

        Assert.Contains("ItemsSource=\"{Binding VisibleBundles}\"", contentXaml);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding CategoryOptions}\"", contentXaml);
        Assert.Contains("Text=\"{Binding CandidateCountText}\"", contentXaml);
        Assert.DoesNotContain("OutputDirectoryText", contentXaml);
        Assert.DoesNotContain("ItemsWrapGrid", contentXaml);
        Assert.Contains("UseWorkspaceExtractionDirectoryCheckBox", windowXaml);
        Assert.Contains("UseWorkspaceExtractionDirectory", windowXaml);
        Assert.Contains("Text=\"{Binding PlanSummaryText}\"", windowXaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", contentXaml);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", windowXaml);
        Assert.Contains("SetTitleBar(AppTitleBar)", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml.cs")));
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
