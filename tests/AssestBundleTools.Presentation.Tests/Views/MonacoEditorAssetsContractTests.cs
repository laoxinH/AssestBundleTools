namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class MonacoEditorAssetsContractTests
{
    private static readonly string[] RequiredAssetRelativePaths =
    [
        "abt-editor-host.html",
        "abt-editor-host.js",
        "LICENSE.monaco-editor.txt",
        "ThirdPartyNotices.monaco-editor.txt",
        "vs/loader.js",
        "vs/editor.api-CalNCsUg.js",
        "vs/workers-DcJshg-q.js",
        "vs/lspLanguageFeatures-kM9O9rjY.js",
        "vs/monaco.contribution-DO3azKX8.js",
        "vs/monaco.contribution-qLAYrEOP.js",
        "vs/monaco.contribution-EcChJV6a.js",
        "vs/monaco.contribution-D2OdxNBt.js",
        "vs/jsonMode-DULH5oaX.js",
        "vs/cpp-CkKPQIni.js",
        "vs/ini-CsNwO04R.js",
        "vs/nls.messages-loader.js",
        "vs/nls.messages.js.js",
        "vs/nls.messages.zh-cn.js.js",
        "vs/editor/editor.main.js",
        "vs/editor/editor.main.css",
        "vs/assets/editor.worker-Be8ye1pW.js",
        "vs/assets/json.worker-DKiEKt88.js",
        "vs/basic-languages/monaco.contribution.js"
    ];

    [Fact]
    public void MonacoAssets_ShouldKeepOnlyCurrentEditorSurfaceDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var monacoRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "Monaco");

        var assetFiles = Directory
            .EnumerateFiles(monacoRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(monacoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var requiredPath in RequiredAssetRelativePaths)
        {
            Assert.Contains(requiredPath, assetFiles);
        }

        Assert.DoesNotContain(assetFiles, path => path.Contains("ts.worker", StringComparison.Ordinal));
        Assert.DoesNotContain(assetFiles, path => path.Contains("css.worker", StringComparison.Ordinal));
        Assert.DoesNotContain(assetFiles, path => path.Contains("html.worker", StringComparison.Ordinal));
        Assert.DoesNotContain(assetFiles, path => path.StartsWith("vs/language/", StringComparison.Ordinal));
        Assert.DoesNotContain(assetFiles, path => path.StartsWith("vs/python-", StringComparison.Ordinal));
        Assert.DoesNotContain(assetFiles, path => path.Equals("vs/nls.messages.ru.js.js", StringComparison.Ordinal));
        Assert.InRange(assetFiles.Length, RequiredAssetRelativePaths.Length, 30);
    }

    [Fact]
    public void Project_ShouldCopyMonacoAssetsByAllowList()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFile = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "AssestBundleTools.Presentation.WinUI.csproj"));

        Assert.Contains("<MonacoEditorAsset Include=\"Assets\\Monaco\\vs\\editor\\editor.main.js\" />", projectFile);
        Assert.Contains("SourceFiles=\"@(MonacoEditorAsset)\"", projectFile);
        Assert.DoesNotContain("<_MonacoEditorAsset Include=\"Assets\\Monaco\\**\\*.*\" />", projectFile);
    }

    [Fact]
    public void MonacoHost_ShouldExposeRangePatchBridgeWithoutSaveTextPayload()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hostScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "Monaco",
            "abt-editor-host.js"));

        Assert.Contains("case 'getPendingRangeEdits':", hostScript);
        Assert.Contains("type: 'documentRangeEdits'", hostScript);
        Assert.Contains("case 'markSaved':", hostScript);
        Assert.Contains("baselineText = getEditorText();", hostScript);

        var saveCommandIndex = hostScript.IndexOf("type: 'saveRequested'", StringComparison.Ordinal);
        var saveCommandEndIndex = hostScript.IndexOf("});", saveCommandIndex, StringComparison.Ordinal);
        var saveCommand = hostScript[saveCommandIndex..saveCommandEndIndex];
        Assert.DoesNotContain("text:", saveCommand);
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
