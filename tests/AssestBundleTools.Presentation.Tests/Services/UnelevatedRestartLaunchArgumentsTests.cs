using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class UnelevatedRestartLaunchArgumentsTests
{
    [Fact]
    public void IsRestartLaunch_ShouldRequireExactMarker()
    {
        Assert.True(UnelevatedRestartLaunchArguments.IsRestartLaunch(
            ["--abt-unelevated-restart"]));
        Assert.False(UnelevatedRestartLaunchArguments.IsRestartLaunch(
            ["--abt-unelevated-restart=1"]));
        Assert.False(UnelevatedRestartLaunchArguments.IsRestartLaunch(
            ["--ABT-UNELEVATED-RESTART"]));
    }

    [Fact]
    public void BuildCommandLine_ShouldQuoteExecutableAndEscapeArgumentsForCreateProcess()
    {
        var commandLine = UnelevatedRestartLaunchArguments.BuildCommandLine(
            @"C:\Program Files\AssestBundleTools\AssestBundleTools.exe",
            ["--abt-unelevated-restart", "", "value with spaces", "quoted\"value"]);

        Assert.Equal(
            "\"C:\\Program Files\\AssestBundleTools\\AssestBundleTools.exe\" " +
            "--abt-unelevated-restart \"\" \"value with spaces\" \"quoted\\\"value\"",
            commandLine);
    }

    [Fact]
    public void BuildArgumentList_ShouldQuoteArgumentsForExplorerShellExecute()
    {
        var argumentList = UnelevatedRestartLaunchArguments.BuildArgumentList(
            ["--abt-unelevated-restart", "", "value with spaces"]);

        Assert.Equal("--abt-unelevated-restart \"\" \"value with spaces\"", argumentList);
    }
}
