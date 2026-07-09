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
        Assert.Contains("x:Name=\"SettingsDiagnosticsNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"SettingsLogsNavItem\"", pageXaml);
        Assert.Contains("x:Name=\"WorkspaceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PreferenceSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"PerformanceCacheSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"DiagnosticsSettingsPanel\"", pageXaml);
        Assert.Contains("x:Name=\"LogCenterPanel\"", pageXaml);
        Assert.Contains("x:Key=\"SettingsDiagnosticsContentColumnStyle\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=WorkspaceSettingsPanel}\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PreferenceSettingsPanel}\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=PerformanceCacheSettingsPanel}\"", pageXaml);
        Assert.Contains("Width=\"{Binding ViewportWidth, ElementName=DiagnosticsSettingsPanel}\"", pageXaml);
        Assert.DoesNotContain("MaxWidth=\"900\"", pageXaml);
        Assert.Contains("x:Key=\"SettingsDiagnosticsSettingRowStyle\"", pageXaml);
        Assert.Contains("x:Name=\"RecentWorkspaceOpenBehaviorComboBox\"", pageXaml);
        Assert.Contains("x:Name=\"AssetEditorCloseBehaviorComboBox\"", pageXaml);
        Assert.Contains("OperationRecordLimitSliderValue", pageXaml);
        Assert.Contains("WorkspaceImportWorkerCountSliderValue", pageXaml);
        Assert.Contains("PreviewWorkerCountSliderValue", pageXaml);
        Assert.Contains("MaximumSizeSliderValue", pageXaml);
        Assert.DoesNotContain("<RadioButtons", pageXaml);
        Assert.Contains("Visibility=\"Collapsed\"", pageXaml);
        Assert.Contains("<shared:LogPanel", pageXaml);
        Assert.Contains("ShowSettingsSubPage", pageCode);
        Assert.Contains("OnSettingsSubPageNavigationSelectionChanged", pageCode);
        Assert.Contains("SettingsDiagnosticsNavLogs", resourceKeys);
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
