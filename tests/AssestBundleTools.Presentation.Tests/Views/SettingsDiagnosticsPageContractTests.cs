namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SettingsDiagnosticsPageContractTests
{
    [Fact]
    public void SettingsDiagnostics_ShouldUseShellHostedSubmenuSections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var shellXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml"));
        var shellCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml.cs"));
        var shellNavigationPaneCoordinatorCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "Shell",
            "Coordinators",
            "ShellNavigationPaneCoordinator.cs"));
        var shellNavigationKeys = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "Shell",
            "Contracts",
            "ShellNavigationKeys.cs"));
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "SettingsDiagnosticsPage.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "SettingsDiagnosticsPage.xaml.cs"));
        var preferencesXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "Controls",
            "SettingsPreferencesView.xaml"));
        var preferencesCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "Controls",
            "SettingsPreferencesView.xaml.cs"));
        var performanceCacheXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "Controls",
            "SettingsPerformanceCacheView.xaml"));
        var performanceCacheCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "Controls",
            "SettingsPerformanceCacheView.xaml.cs"));
        var pageResources = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "SettingsDiagnosticsResources.xaml"));
        var resourceKeys = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Localization",
            "ResourceKeys.cs"));

        Assert.Contains("ItemInvoked=\"OnNavigationItemInvoked\"", shellXaml);
        Assert.Contains("PaneClosing=\"OnNavigationPaneClosing\"", shellXaml);
        Assert.Contains("PaneOpened=\"OnNavigationPaneOpened\"", shellXaml);
        Assert.DoesNotContain("PaneOpening=", shellXaml);
        Assert.Contains("x:Name=\"SettingsDiagnosticsNavItem\"", shellXaml);
        Assert.Contains("IsExpanded=\"True\"", shellXaml);
        Assert.Contains("SelectsOnInvoked=\"False\"", shellXaml);
        Assert.Contains("<NavigationViewItem.MenuItems>", shellXaml);
        Assert.Contains("x:Name=\"SettingsWorkspaceNavItem\"", shellXaml);
        Assert.Contains("x:Name=\"SettingsPreferencesNavItem\"", shellXaml);
        Assert.Contains("x:Name=\"SettingsPerformanceCacheNavItem\"", shellXaml);
        Assert.Contains("x:Name=\"SettingsLogsNavItem\"", shellXaml);
        Assert.DoesNotContain("x:Name=\"SettingsSubPageNavigationView\"", pageXaml);
        Assert.DoesNotContain("<NavigationView", pageXaml);
        Assert.DoesNotContain("x:Name=\"SettingsDiagnosticsNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"WorkspaceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PreferenceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PerformanceCacheSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"LogCenterPanel\"", pageXaml);
        Assert.DoesNotContain("x:Name=\"DiagnosticsSettingsPanel\"", pageXaml);
        Assert.Contains("SettingsDiagnosticsResources.xaml", pageXaml);
        Assert.Contains("SettingsDiagnosticsResources.xaml", preferencesXaml);
        Assert.Contains("SettingsDiagnosticsResources.xaml", performanceCacheXaml);
        Assert.Contains("new SettingsPreferencesView(ViewModel)", pageCode);
        Assert.Contains("new SettingsPerformanceCacheView(ViewModel)", pageCode);
        Assert.Contains("ActivateSection(SettingsDiagnosticsSection.Preferences)", pageCode);
        Assert.Contains("ContentFrame.Content is SettingsDiagnosticsPage", shellCode);
        Assert.Contains("if (!sender.IsPaneOpen)", shellCode);
        Assert.Contains("new ShellNavigationPaneCoordinator(", shellCode);
        Assert.Contains("_navigationPaneCoordinator.ExpandSettingsIfPaneOpen()", shellCode);
        Assert.Contains("_settingsNavigationItem.IsExpanded = false", shellNavigationPaneCoordinatorCode);
        Assert.Contains("ApplySettingsPageLayout(_navigationView.IsPaneOpen)", shellNavigationPaneCoordinatorCode);
        Assert.Contains("_navigationView.OpenPaneLength - _navigationView.CompactPaneLength", shellNavigationPaneCoordinatorCode);
        Assert.Contains("_lastSettingsSection = SettingsDiagnosticsSection.Preferences", shellCode);
        Assert.Contains("SettingsDiagnosticsNavWorkspace", shellCode);
        Assert.Contains("SettingsDiagnosticsNavPreferences", shellCode);
        Assert.Contains("SettingsDiagnosticsNavPerformanceCache", shellCode);
        Assert.Contains("SettingsDiagnosticsNavLogs", shellCode);
        Assert.Contains("SettingsDiagnosticsWorkspace", shellNavigationKeys);
        Assert.Contains("SettingsDiagnosticsPreferences", shellNavigationKeys);
        Assert.Contains("SettingsDiagnosticsPerformanceCache", shellNavigationKeys);
        Assert.Contains("SettingsDiagnosticsLogs", shellNavigationKeys);
        Assert.Contains("x:Key=\"SettingsDiagnosticsContentColumnStyle\"", pageResources);
        Assert.Contains("x:Name=\"WorkspaceSettingsContentColumn\"", pageXaml);
        Assert.Contains("x:Name=\"LogCenterContentColumn\"", pageXaml);
        Assert.Contains("x:Name=\"PreferenceSettingsContentColumn\"", preferencesXaml);
        Assert.Contains("x:Name=\"PerformanceCacheSettingsContentColumn\"", performanceCacheXaml);
        Assert.Contains("DefaultContentColumnMaxWidth + reclaimedWidth", pageCode);
        Assert.Contains("SetContentColumnMaxWidth(contentColumnMaxWidth)", pageCode);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSurfaceStyle\"", pageResources);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", pageResources);
        Assert.DoesNotContain("SettingsDiagnosticsPanelShadow", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=WorkspaceSettingsPanel}\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PreferenceSettingsScrollViewer}\"", preferencesXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PerformanceCacheSettingsScrollViewer}\"", performanceCacheXaml);
        Assert.DoesNotContain("MaxWidth=\"900\"", string.Concat(pageXaml, preferencesXaml, performanceCacheXaml));
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingRowStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingIconStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingDescriptionTextStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsTrailingToggleSwitchStyle\"", pageResources);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"0\" />", pageResources);
        Assert.Contains("x:Name=\"RecentWorkspaceOpenBehaviorComboBox\"", preferencesXaml);
        Assert.Contains("x:Name=\"AssetEditorCloseBehaviorComboBox\"", preferencesXaml);
        Assert.Contains("x:Name=\"DisplayLanguageComboBox\"", preferencesXaml);
        Assert.Contains("DisplayLanguageSelectedIndex", preferencesXaml);
        Assert.Contains("x:Name=\"RestoreLastWorkspaceOnStartupToggleSwitch\"", preferencesXaml);
        Assert.Contains("x:Name=\"ElevatedDragDropCompatibilityToggleSwitch\"", preferencesXaml);
        Assert.Contains("ElevatedDragDropCompatibilityEnabled", preferencesXaml);
        Assert.Contains("Style=\"{StaticResource SettingsDiagnosticsTrailingToggleSwitchStyle}\"", preferencesXaml);
        Assert.Contains("x:Name=\"BundleArtifactCacheAutoPruneToggleSwitch\"", performanceCacheXaml);
        Assert.Contains("Style=\"{StaticResource SettingsDiagnosticsTrailingToggleSwitchStyle}\"", performanceCacheXaml);
        Assert.DoesNotContain("FlowDirection=\"RightToLeft\"", string.Concat(pageXaml, preferencesXaml, performanceCacheXaml));
        Assert.Contains("x:Name=\"BundleArtifactCacheMaximumSizeDescriptionText\"", performanceCacheXaml);
        Assert.Contains("OperationRecordLimitSliderValue", preferencesXaml);
        Assert.Contains("WorkspaceImportWorkerCountSliderValue", performanceCacheXaml);
        Assert.Contains("PreviewWorkerCountSliderValue", performanceCacheXaml);
        Assert.Contains("MaximumSizeSliderValue", performanceCacheXaml);
        Assert.DoesNotContain("<RadioButtons", string.Concat(pageXaml, performanceCacheXaml));
        Assert.Contains("Visibility=\"Collapsed\"", pageXaml);
        Assert.Contains("<shared:LogPanel", pageXaml);
        Assert.Contains("ActivateSection", pageCode);
        Assert.DoesNotContain("OnSettingsSubPageNavigationSelectionChanged", pageCode);
        Assert.Contains("SettingsDiagnosticsRestoreLastWorkspaceToggleHeader", preferencesCode);
        Assert.Contains("SettingsDiagnosticsElevatedDragDropCompatibilityLabel", preferencesCode);
        Assert.Contains("SettingsDiagnosticsElevatedDragDropCompatibilityDescription", preferencesCode);
        Assert.Contains("SettingsDiagnosticsDisplayLanguageOptionSystemDefault", preferencesCode);
        Assert.Contains("SettingsDiagnosticsDisplayLanguageSaveFailedMessage", resourceKeys);
        Assert.Contains("SettingsDiagnosticsBundleArtifactCacheMaximumSizeRowDescription", performanceCacheCode);
        Assert.Contains("SettingsDiagnosticsNavLogs", resourceKeys);
        Assert.Contains("SettingsDiagnosticsRestoreLastWorkspaceRowDescription", resourceKeys);
        Assert.Contains("SettingsDiagnosticsBundleArtifactCacheMaximumSizeRowDescription", resourceKeys);
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
