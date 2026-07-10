namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SettingsDiagnosticsPageContractTests
{
    [Fact]
    public void SettingsDiagnostics_ShouldUseGroupedSubPages()
    {
        var repositoryRoot = FindRepositoryRoot();
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

        Assert.Contains("x:Name=\"SettingsSubPageNavigationView\"", pageXaml);
        Assert.Contains("x:Name=\"SettingsWorkspaceNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"SettingsPreferencesNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"SettingsPerformanceCacheNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"SettingsLogsNavItem\"", pageXaml);
        Assert.DoesNotContain("x:Name=\"SettingsDiagnosticsNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"WorkspaceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PreferenceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PerformanceCacheSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"LogCenterPanel\"", pageXaml);
        Assert.DoesNotContain("x:Name=\"DiagnosticsSettingsPanel\"", pageXaml);
        Assert.Contains("SettingsDiagnosticsResources.xaml", pageXaml);
        Assert.Contains("SettingsDiagnosticsResources.xaml", preferencesXaml);
        Assert.Contains("new SettingsPreferencesView(ViewModel)", pageCode);
        Assert.Contains("x:Key=\"SettingsDiagnosticsContentColumnStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSurfaceStyle\"", pageResources);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", pageResources);
        Assert.DoesNotContain("SettingsDiagnosticsPanelShadow", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=WorkspaceSettingsPanel}\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PreferenceSettingsScrollViewer}\"", preferencesXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PerformanceCacheSettingsPanel}\"", pageXaml);
        Assert.DoesNotContain("MaxWidth=\"900\"", string.Concat(pageXaml, preferencesXaml));
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingRowStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingIconStyle\"", pageResources);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingDescriptionTextStyle\"", pageResources);
        Assert.Contains("x:Name=\"RecentWorkspaceOpenBehaviorComboBox\"", preferencesXaml);
        Assert.Contains("x:Name=\"AssetEditorCloseBehaviorComboBox\"", preferencesXaml);
        Assert.Contains("x:Name=\"RestoreLastWorkspaceOnStartupToggleSwitch\"", preferencesXaml);
        Assert.Contains("x:Name=\"BundleArtifactCacheAutoPruneToggleSwitch\"", pageXaml);
        Assert.DoesNotContain("FlowDirection=\"RightToLeft\"", string.Concat(pageXaml, preferencesXaml));
        Assert.Contains("x:Name=\"BundleArtifactCacheMaximumSizeDescriptionText\"", pageXaml);
        Assert.Contains("OperationRecordLimitSliderValue", preferencesXaml);
        Assert.Contains("WorkspaceImportWorkerCountSliderValue", pageXaml);
        Assert.Contains("PreviewWorkerCountSliderValue", pageXaml);
        Assert.Contains("MaximumSizeSliderValue", pageXaml);
        Assert.DoesNotContain("<RadioButtons", pageXaml);
        Assert.Contains("Visibility=\"Collapsed\"", pageXaml);
        Assert.Contains("<shared:LogPanel", pageXaml);
        Assert.Contains("ShowSettingsSubPage", pageCode);
        Assert.Contains("OnSettingsSubPageNavigationSelectionChanged", pageCode);
        Assert.Contains("SettingsDiagnosticsRestoreLastWorkspaceToggleHeader", preferencesCode);
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
