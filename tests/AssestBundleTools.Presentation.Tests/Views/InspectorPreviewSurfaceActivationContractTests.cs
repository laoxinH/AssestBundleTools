namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class InspectorPreviewSurfaceActivationContractTests
{
    [Fact]
    public void InspectorPanelPreview_ShouldDisableNestedPreview_WhenDetailsPreviewIsHidden()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPanelView.xaml"));

        Assert.Contains("IsPreviewActive=\"{x:Bind ShowDetailsPreview, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void InspectorPanelActionBar_ShouldExposeOnlyLightweightExtractAndModifyActions()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPanelView.xaml"));

        Assert.Contains("x:Name=\"ExtractButton\"", xaml);
        Assert.Contains("x:Name=\"ModifyButton\"", xaml);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.CanModify, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("x:Name=\"ApplyReplacementButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"UndoReplacementButton\"", xaml);
    }

    [Fact]
    public void InspectorModifyButton_ShouldUseImmediateFileModificationWithoutWorkspaceFallback()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPanelView.xaml.cs"));
        var handler = code[
            code.IndexOf("private void OnModifyButtonClick", StringComparison.Ordinal)..
            code.IndexOf("private void OnPreviewSurfaceOpenPreviewLocationRequested", StringComparison.Ordinal)];

        var fileGuardIndex = handler.IndexOf("ViewModel?.CanSelectFileReplacementSource == true", StringComparison.Ordinal);
        var fileInvokeIndex = handler.IndexOf("ModifyFromFileReplacementSourceRequested?.Invoke", StringComparison.Ordinal);

        Assert.DoesNotContain("ApplyReplacementRequested?.Invoke", handler);
        Assert.True(fileGuardIndex < fileInvokeIndex);
        Assert.DoesNotContain("SelectFileReplacementSourceRequested?.Invoke", handler);
        Assert.DoesNotContain("SelectReplacementSourceRequested?.Invoke", handler);
    }

    [Fact]
    public void InspectorChangesSection_ShouldExposeSeparateRecordsWithUndoRedoCommands()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPanelView.xaml"));

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.ChangeRecords, Mode=OneWay}\"", xaml);
        Assert.Contains("OnChangeRecordUndoReplacementButtonClick", xaml);
        Assert.Contains("OnChangeRecordRedoReplacementButtonClick", xaml);
        Assert.Contains("OnChangeRecordDeleteButtonClick", xaml);
        Assert.Contains("ViewModel.ChangeRecordsEmptyStateVisibility", xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldFollowParentPreviewActivation()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml"));

        Assert.Contains("<controls:InspectorSpineAnimationPreviewSurfaceView", xaml);
        Assert.Contains("IsPreviewActive=\"{x:Bind IsPreviewActive, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldBindSliderToThrottledDisplayPlaybackTime()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml"));

        Assert.Contains(
            "Value=\"{x:Bind ViewModel.DisplayPlaybackTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            xaml);
        Assert.Contains(
            "Text=\"{x:Bind ViewModel.DisplayPlaybackTimeText, Mode=OneWay}\"",
            xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldSuspendPlaybackClockWhileSliderIsScrubbed()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml.cs"));
        var runtimeCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.Runtime.cs"));
        var sliderCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.PlaybackSlider.cs"));

        Assert.Contains("RegisterPlaybackSliderInputHandlers();", code);
        Assert.Contains("CancelPlaybackSliderScrub();", code);
        Assert.Contains("if (_isPlaybackSliderScrubbing)", runtimeCode);
        Assert.Contains("UIElement.PointerPressedEvent", sliderCode);
        Assert.Contains("UIElement.PointerReleasedEvent", sliderCode);
        Assert.Contains("ViewModel.DisplayPlaybackTime = PlaybackSlider.Value;", sliderCode);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldExposeImageStyleBackgroundSwitcher()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml"));

        Assert.Contains("<controls:CheckerboardBackground", xaml);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.CheckerboardBackgroundVisibility, Mode=OneWay}\"", xaml);
        Assert.Contains("Background=\"White\"", xaml);
        Assert.Contains("x:Name=\"BackgroundModeButton\"", xaml);
        Assert.Contains("Glyph=\"{x:Bind ViewModel.BackgroundModeGlyph, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldUseWidthDrivenSquareViewportByDefault()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml"));
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml.cs"));
        var viewportCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.Viewport.cs"));

        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
        Assert.Contains("x:Name=\"PreviewViewportSquareFrame\"", xaml);
        Assert.Contains("SizeChanged=\"OnPreviewViewportSquareFrameSizeChanged\"", xaml);
        Assert.Contains("UseSquareViewportProperty", code);
        Assert.Contains("PreviewViewportSquareFrame.Height = squareSide;", viewportCode);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldUseFlexibleViewportInAssetEditorWindow()
    {
        var previewSurfaceXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml"));
        var previewSurfaceCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.Height.cs"));
        var assetEditorXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "AssetEditorWindow.xaml"));

        Assert.Contains("UseSquareViewport=\"{x:Bind UseCompactSpineViewport, Mode=OneWay}\"", previewSurfaceXaml);
        Assert.Contains("UseAvailablePreviewSpaceProperty", previewSurfaceCode);
        Assert.Contains("UseCompactSpineViewport => !UseAvailablePreviewSpace", previewSurfaceCode);
        Assert.Contains("UseAvailablePreviewSpace=\"True\"", assetEditorXaml);
    }

    [Fact]
    public void SpriteEditGeometryPanel_ShouldLiveBetweenEditorPreviewAndActionBar()
    {
        var imagePreviewXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorImagePreviewSurfaceView.xaml"));
        var assetEditorXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "AssetEditorWindow.xaml"));

        Assert.DoesNotContain("SpriteEditGeometryPanelView", imagePreviewXaml);
        Assert.DoesNotContain("SpriteEditGeometryNumberBox", imagePreviewXaml);
        Assert.Contains("<controls:SpriteEditGeometryPanelView", assetEditorXaml);
        Assert.Contains(
            "Visibility=\"{x:Bind ViewModel.Inspector.Preview.Image.SpriteEditGeometryPanelVisibility, Mode=OneWay}\"",
            assetEditorXaml);
        Assert.Contains(
            "ViewModel=\"{x:Bind ViewModel.Inspector.Preview.Image, Mode=OneWay}\"",
            assetEditorXaml);

        var previewIndex = assetEditorXaml.IndexOf("<controls:InspectorPreviewSurfaceView", StringComparison.Ordinal);
        var geometryIndex = assetEditorXaml.IndexOf("<controls:SpriteEditGeometryPanelView", StringComparison.Ordinal);
        var actionBarIndex = assetEditorXaml.IndexOf("x:Name=\"ExtractButton\"", StringComparison.Ordinal);

        Assert.True(previewIndex >= 0);
        Assert.True(geometryIndex > previewIndex);
        Assert.True(actionBarIndex > geometryIndex);
    }

    [Fact]
    public void SpriteEditGeometryPanel_ShouldUseTextOnlyNumberInput()
    {
        var panelXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "SpriteEditGeometryPanelView.xaml"));

        Assert.Contains("SpinButtonPlacementMode\" Value=\"Hidden\"", panelXaml);
        Assert.DoesNotContain("SpinButtonPlacementMode\" Value=\"Compact\"", panelXaml);
    }

    [Fact]
    public void SpriteEditGeometryPanel_ShouldNotWriteBackWhilePreviewDragIsActive()
    {
        var panelCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "SpriteEditGeometryPanelView.xaml.cs"));

        Assert.Contains("ViewModel.IsSpriteEditDragActive", panelCode);
        Assert.Contains("ViewModel.UpdateSpriteEditGeometryFromInput", panelCode);
        Assert.Contains("ResolveSpriteEditGeometryInputKind(sender)", panelCode);
        Assert.Contains("SpriteEditGeometryInputKind.Pivot", panelCode);
        Assert.Contains("SpriteEditGeometryInputKind.Crop", panelCode);
    }

    [Fact]
    public void SpriteEditGeometryPanel_ShouldReleaseNumberInputFocusWhenPreviewDragStarts()
    {
        var panelXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "SpriteEditGeometryPanelView.xaml"));
        var panelCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "SpriteEditGeometryPanelView.xaml.cs"));

        Assert.Contains("x:Name=\"SpriteEditFocusSinkButton\"", panelXaml);
        Assert.Contains("AutomationProperties.AccessibilityView=\"Raw\"", panelXaml);
        Assert.Contains("ReleaseSpriteEditGeometryInputFocus();", panelCode);
        Assert.Contains("ResolveFocusedSpriteEditGeometryNumberBox();", panelCode);
        Assert.Contains("ResetSpriteEditGeometryNumberBox(focusedNumberBox);", panelCode);
        Assert.Contains("TryFocusSpriteEditFocusSink()", panelCode);
        Assert.Contains("SpriteEditFocusSinkButton.IsTabStop = true", panelCode);
    }

    [Fact]
    public void SpriteEditPreviewToolbar_ShouldExposeGeometryShortcutsOnlyWhileEditing()
    {
        var previewSurfaceXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml"));
        var previewSurfaceCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml.cs"));

        Assert.Contains("x:Name=\"SpriteEditResetOriginalButton\"", previewSurfaceXaml);
        Assert.Contains("x:Name=\"SpriteEditFullSizeButton\"", previewSurfaceXaml);
        Assert.Contains(
            "Visibility=\"{x:Bind ViewModel.Preview.Image.SpriteEditActionControlsVisibility, Mode=OneWay}\"",
            previewSurfaceXaml);
        Assert.Contains("OnSpriteEditResetOriginalButtonClick", previewSurfaceXaml);
        Assert.Contains("OnSpriteEditFullSizeButtonClick", previewSurfaceXaml);
        Assert.Contains("ResetSpriteEditGeometryToOriginal()", previewSurfaceCode);
        Assert.Contains("UseFullImageForSpriteEditGeometry()", previewSurfaceCode);
        Assert.Contains("InspectorSpriteEditResetOriginalToolTip", previewSurfaceCode);
        Assert.Contains("InspectorSpriteEditFullSizeToolTip", previewSurfaceCode);
        Assert.Contains("AutomationProperties.SetName(button, text)", previewSurfaceCode);
    }

    [Fact]
    public void ImagePreviewViewportHost_ShouldReceivePointerInputAcrossTransparentAreas()
    {
        var imagePreviewXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorImagePreviewSurfaceView.xaml"));

        Assert.Contains("x:Name=\"PreviewImageViewportHost\"", imagePreviewXaml);
        Assert.Contains("Background=\"Transparent\"", imagePreviewXaml);
        Assert.Contains("PointerPressed=\"OnPreviewImagePointerPressed\"", imagePreviewXaml);
    }

    [Fact]
    public void SpriteEditPreview_ShouldExposeMeasurementOverlay()
    {
        var imagePreviewXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorImagePreviewSurfaceView.xaml"));

        Assert.Contains("SpriteEditMeasurementVisibility", imagePreviewXaml);
        Assert.Contains("SpriteEditAxisOriginX", imagePreviewXaml);
        Assert.Contains("SpriteEditTopLeftCoordinateText", imagePreviewXaml);
        Assert.Contains("SpriteEditBottomRightCoordinateText", imagePreviewXaml);
        Assert.Contains("SpriteEditTopLengthText", imagePreviewXaml);
        Assert.Contains("SpriteEditRightLengthText", imagePreviewXaml);
    }

    [Fact]
    public void SpriteEditPreview_ShouldUseIndependentFrameLinesWhileEditing()
    {
        var imagePreviewXaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorImagePreviewSurfaceView.xaml"));

        Assert.Contains("OverlayFrameVisibility", imagePreviewXaml);
        Assert.Contains("SpriteEditFrameVisibility", imagePreviewXaml);
        Assert.Contains("X2=\"{x:Bind ViewModel.OverlayRightX, Mode=OneWay}\"", imagePreviewXaml);
        Assert.Contains("Y2=\"{x:Bind ViewModel.OverlayBottomY, Mode=OneWay}\"", imagePreviewXaml);
    }

    [Fact]
    public void SpineD3DPreviewBackend_ShouldDrawPreviewBackground()
    {
        var backendCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "D3D11SpineAnimationPreviewRenderBackend.cs"));
        var backgroundRendererCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "D3D11SpinePreviewBackgroundRenderer.cs"));

        Assert.Contains("D3D11SpinePreviewBackgroundRenderer", backendCode);
        Assert.Contains("_backgroundRenderer.Draw(", backendCode);
        Assert.Contains("CheckerboardTileLayoutBuilder.Build", backgroundRendererCode);
        Assert.Contains("SolidBackgroundColor", backgroundRendererCode);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldQueueBindingUpdatesFromObservedViewModelChanges()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml.cs"));
        var bindingCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.Bindings.cs"));

        Assert.Contains("private void QueueBindingsUpdate()", bindingCode);
        Assert.Contains("if (shouldUpdateBindings)", code);
        Assert.Contains("QueueBindingsUpdate();", code);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldReconcileSelectorSelectionsAfterBindingRefresh()
    {
        var bindingCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.Bindings.cs"));
        var selectorCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.Selectors.cs"));

        Assert.Contains("Bindings.Update();", bindingCode);
        Assert.Contains("ReconcileSelectorSelections();", bindingCode);
        Assert.Contains("SelectStringItem(AnimationComboBox", selectorCode);
        Assert.Contains("SelectStringItem(SkinComboBox", selectorCode);
        Assert.Contains("SelectPlaybackSpeedItem(PlaybackSpeedComboBox", selectorCode);
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
