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

    [Fact]
    public void MonacoHost_ShouldTrackStreamingRangeEditsWithoutFullDocumentReplacement()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hostScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "Monaco",
            "abt-editor-host.js"));

        Assert.Contains("let pendingStreamingRangeEdit = null;", hostScript);
        Assert.Contains("trackStreamingContentChanges(event && event.changes);", hostScript);
        Assert.Contains("mergeStreamingRangeEdit", hostScript);
        Assert.Contains("getCurrentTextByOffsetRange(", hostScript);

        var baselineBranchStart = hostScript.IndexOf("if (baselineInfo) {", StringComparison.Ordinal);
        var baselineBranchEnd = hostScript.IndexOf("const currentText = getEditorText();", baselineBranchStart, StringComparison.Ordinal);
        var baselineBranch = hostScript[baselineBranchStart..baselineBranchEnd];
        Assert.Contains("pendingStreamingRangeEdit.baselineStart", baselineBranch);
        Assert.Contains("pendingStreamingRangeEdit.currentEnd", baselineBranch);
        Assert.DoesNotContain("text: currentText", baselineBranch);
    }

    [Fact]
    public void TextEditorSurface_ShouldInvalidateSentDocumentCacheAfterWebViewEdits()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bridgeCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextDocumentSurfaceView.WebBridge.cs"));
        var draftViewCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextAssetEditDraftView.xaml.cs"));
        var windowTextEditCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "AssetEditorWindow.TextEdit.cs"));

        var contentChangedBlockStart = bridgeCode.IndexOf("case \"contentChanged\":", StringComparison.Ordinal);
        var contentChangedBlockEnd = bridgeCode.IndexOf("break;", contentChangedBlockStart, StringComparison.Ordinal);
        var contentChangedBlock = bridgeCode[contentChangedBlockStart..contentChangedBlockEnd];
        Assert.Contains("_hasUnsynchronizedWebViewChanges = true;", contentChangedBlock);
        Assert.Contains("_isSentDocumentCacheValid = false;", contentChangedBlock);

        Assert.Contains("private bool _isSentDocumentCacheValid;", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextDocumentSurfaceView.xaml.cs")));
        Assert.Contains("ResetPendingDocumentChanges(bool forceRefreshDocument = false)", bridgeCode);
        Assert.Contains("_isSentDocumentCacheValid &&", bridgeCode);
        Assert.Contains("SendDocumentToWebView(force: true);", bridgeCode);
        Assert.Contains("view.Bindings.Update();", draftViewCode);
        Assert.Contains("ResetPendingDocumentChanges(forceRefreshDocument: true);", draftViewCode);
        Assert.Contains("var hadUnsavedChanges = draft?.HasUnsavedChanges == true;", windowTextEditCode);
        Assert.Contains("if (applied && hadUnsavedChanges)", windowTextEditCode);
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
