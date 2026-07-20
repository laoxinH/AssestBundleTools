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
        Assert.Contains("Name=\"CopyMonacoEditorAssetsToPublish\" AfterTargets=\"Publish\"", projectFile);
        Assert.Contains("DestinationFiles=\"@(MonacoEditorAsset->'$(PublishDir)\\%(Identity)')\"", projectFile);
        Assert.Contains("Published Monaco editor assets are missing.", projectFile);
        Assert.DoesNotContain("<_MonacoEditorAsset Include=\"Assets\\Monaco\\**\\*.*\" />", projectFile);
    }

    [Fact]
    public void ReleasePipeline_ShouldRejectPackagesWithoutMonacoAssets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var releaseScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "eng",
            "release",
            "Publish-Architecture.ps1"));

        Assert.Contains("function Assert-MonacoAssetDirectory", releaseScript);
        Assert.Contains("function Assert-MonacoAssetsInArchive", releaseScript);
        Assert.Contains("-AssetRoot (Join-Path $publishDirectory 'Assets\\Monaco')", releaseScript);
        Assert.Contains("-ArchivePath $portable.FullName", releaseScript);
        Assert.Contains("-ArchivePath $fullPackage.FullName", releaseScript);
    }

    [Fact]
    public void MonacoHost_ShouldExposeRangePatchBridgeWithoutSaveTextPayload()
    {
        var hostScript = ReadMonacoHostScript();

        Assert.Contains("case 'getPendingRangeEdits':", hostScript);
        Assert.Contains("type: 'documentRangeEdits'", hostScript);
        Assert.Contains("case 'markSaved':", hostScript);
        Assert.Contains("baselineText = getEditorText();", hostScript);
        Assert.Contains("createRangeEditFromTexts(baselineText, currentText)", hostScript);

        var saveCommandIndex = hostScript.IndexOf("type: 'saveRequested'", StringComparison.Ordinal);
        var saveCommandEndIndex = hostScript.IndexOf("});", saveCommandIndex, StringComparison.Ordinal);
        var saveCommand = hostScript[saveCommandIndex..saveCommandEndIndex];
        Assert.DoesNotContain("text:", saveCommand);
    }

    [Fact]
    public void MonacoHost_ShouldCreateStreamingRangeEditsFromFinalLfText()
    {
        var hostScript = ReadMonacoHostScript();

        Assert.Contains("let streamingBaselineChunks = [];", hostScript);
        Assert.Contains("streamingBaselineChunks.push(text);", hostScript);
        Assert.Contains("baselineText = streamingBaselineChunks.join('');", hostScript);
        Assert.Contains("function createRangeEditFromTexts(savedText, currentText)", hostScript);

        var pendingRangeEditsFunction = ExtractFunction(hostScript, "function getPendingRangeEdits");
        Assert.Contains("const currentText = getEditorText();", pendingRangeEditsFunction);
        Assert.Contains("createRangeEditFromTexts(baselineText, currentText)", pendingRangeEditsFunction);
        Assert.DoesNotContain("rangeOffset", pendingRangeEditsFunction);
        Assert.DoesNotContain("pendingStreamingRangeEdit", pendingRangeEditsFunction);
    }

    [Fact]
    public void MonacoHost_ShouldUseLfTextForStreamingRangePatches()
    {
        var hostScript = ReadMonacoHostScript();

        Assert.Contains("function ensureModelUsesLf(model)", hostScript);
        Assert.Contains("model.setEOL(monaco.editor.EndOfLineSequence.LF);", hostScript);

        var setDocumentFunction = ExtractFunction(hostScript, "function setDocument");
        Assert.Contains("ensureModelUsesLf(model);", setDocumentFunction);
        Assert.Contains("ensureModelUsesLf(nextModel);", setDocumentFunction);

        var beginStreamingFunction = ExtractFunction(hostScript, "function beginStreamingDocument");
        Assert.Contains("ensureModelUsesLf(model);", beginStreamingFunction);
        Assert.Contains("ensureModelUsesLf(nextModel);", beginStreamingFunction);

        var appendChunkFunction = ExtractFunction(hostScript, "function appendDocumentChunk");
        Assert.Contains("ensureModelUsesLf(model);", appendChunkFunction);
        Assert.Contains("model.applyEdits(", appendChunkFunction);
        Assert.Contains("streamingBaselineChunks.push(text);", appendChunkFunction);

        Assert.DoesNotContain("trackStreamingContentChanges", hostScript);
        Assert.DoesNotContain("pendingStreamingRangeEdit", hostScript);
        Assert.DoesNotContain("Number(change && change.rangeOffset)", hostScript);
    }

    [Fact]
    public void MonacoHostPage_ShouldVersionHostScriptForWebViewCacheInvalidation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hostPage = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "Monaco",
            "abt-editor-host.html"));

        Assert.Contains("abt-editor-host.js?v=20260707-editor-visual-refresh", hostPage);
    }

    [Fact]
    public void TextAssetEditDraftView_ShouldHideLegacyHeaderInEditableMode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var draftViewXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextAssetEditDraftView.xaml"));

        Assert.Contains("Visibility=\"{x:Bind ViewModel.HeaderVisibility, Mode=OneWay}\"", draftViewXaml);
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

    [Fact]
    public void TextAssetEditDraftView_ShouldCancelStaleStreamingLoadWhenDraftChanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var draftViewCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextAssetEditDraftView.xaml.cs"));
        var bridgeCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "TextDocumentSurfaceView.WebBridge.cs"));

        Assert.Contains("private CancellationTokenSource? _fullEditLoadCancellation;", draftViewCode);
        Assert.Contains("view.CancelFullEditLoad();", draftViewCode);
        Assert.Contains("CancelFullEditLoad();", draftViewCode);
        Assert.Contains("var viewModel = ViewModel;", draftViewCode);
        Assert.Contains("ReferenceEquals(ViewModel, viewModel)", draftViewCode);
        Assert.Contains("catch (OperationCanceledException) when (cancellation.IsCancellationRequested)", draftViewCode);
        Assert.Contains("_ = TextEditorSurface.CancelStreamingDocumentAsync();", draftViewCode);
        Assert.DoesNotContain("if (ViewModel.CanLoadFullEdit)", draftViewCode);
        Assert.DoesNotContain("CoreWebView2!", bridgeCode);
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

    private static string ReadMonacoHostScript()
    {
        var repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "Monaco",
            "abt-editor-host.js"));
    }

    private static string ExtractFunction(string source, string declaration)
    {
        var startIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"{declaration} was not found.");

        var nextFunctionIndex = source.IndexOf("\n  function ", startIndex + declaration.Length, StringComparison.Ordinal);
        return nextFunctionIndex >= 0
            ? source[startIndex..nextFunctionIndex]
            : source[startIndex..];
    }
}
