using System.Xml.Linq;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceSetupDialogContractTests
{
    [Fact]
    public void Content_ShouldShrinkWithinContentDialogViewport()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "Controls",
            "WorkspaceSetupDialogContent.xaml"));
        var root = Assert.IsType<XElement>(document.Root);

        Assert.Equal("Stretch", (string?)root.Attribute("HorizontalAlignment"));
        Assert.Equal("600", (string?)root.Attribute("MaxWidth"));
        Assert.Null(root.Attribute("Width"));
        Assert.Null(root.Attribute("MinWidth"));
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
