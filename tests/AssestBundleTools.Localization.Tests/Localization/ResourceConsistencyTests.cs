using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AssestBundleTools.Localization.Tests.Localization;

public sealed class ResourceConsistencyTests
{
    [Fact]
    public void ZhCnAndEnUsResourceKeys_ShouldMatch()
    {
        var zhCnKeys = LoadResourceKeys("zh-CN");
        var enUsKeys = LoadResourceKeys("en-US");

        var missingFromZhCn = enUsKeys.Except(zhCnKeys).OrderBy(static key => key).ToArray();
        var missingFromEnUs = zhCnKeys.Except(enUsKeys).OrderBy(static key => key).ToArray();

        Assert.True(
            missingFromZhCn.Length == 0 && missingFromEnUs.Length == 0,
            CreateDifferenceMessage(missingFromZhCn, missingFromEnUs));
    }

    [Fact]
    public void ResourceKeysConstants_ShouldExistInLocalizedResources()
    {
        var declaredResourceKeys = LoadDeclaredResourceKeys();
        var zhCnKeys = LoadResourceKeys("zh-CN");
        var enUsKeys = LoadResourceKeys("en-US");

        var missingFromZhCn = declaredResourceKeys.Except(zhCnKeys).OrderBy(static key => key).ToArray();
        var missingFromEnUs = declaredResourceKeys.Except(enUsKeys).OrderBy(static key => key).ToArray();

        Assert.True(
            missingFromZhCn.Length == 0 && missingFromEnUs.Length == 0,
            CreateDifferenceMessage(missingFromZhCn, missingFromEnUs));
    }

    private static HashSet<string> LoadResourceKeys(string locale)
    {
        var resourceFilePath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Strings",
            locale,
            "Resources.resw");
        var document = XDocument.Load(resourceFilePath);

        return document
            .Root?
            .Elements("data")
            .Select(static element => (string?)element.Attribute("name"))
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key!)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
    }

    private static HashSet<string> LoadDeclaredResourceKeys()
    {
        var resourceKeysPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Localization",
            "ResourceKeys.cs");
        var sourceText = File.ReadAllText(resourceKeysPath);

        return Regex.Matches(sourceText, "public const string \\w+ = \"([^\"]+)\";")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AssestBundleTools.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test output directory.");
    }

    private static string CreateDifferenceMessage(
        IReadOnlyList<string> missingFromZhCn,
        IReadOnlyList<string> missingFromEnUs)
    {
        return $"""
                Missing from zh-CN:
                {FormatEntries(missingFromZhCn)}

                Missing from en-US:
                {FormatEntries(missingFromEnUs)}
                """;
    }

    private static string FormatEntries(IReadOnlyList<string> entries)
    {
        return entries.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, entries);
    }
}
