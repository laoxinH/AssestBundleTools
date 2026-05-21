using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPanelViewModelTests
{
    [Fact]
    public void CanModify_ShouldFollowImmediateFileReplacementCapabilityOnly()
    {
        var viewModel = CreateViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.False(viewModel.CanModify);

        viewModel.CanSelectFileReplacementSource = true;

        Assert.True(viewModel.CanModify);
        Assert.Contains(nameof(InspectorPanelViewModel.CanModify), changedProperties);

        changedProperties.Clear();
        viewModel.CanSelectFileReplacementSource = false;

        Assert.False(viewModel.CanModify);
        Assert.Contains(nameof(InspectorPanelViewModel.CanModify), changedProperties);

        changedProperties.Clear();
        viewModel.CanSelectReplacementSource = true;

        Assert.False(viewModel.CanModify);
        Assert.DoesNotContain(nameof(InspectorPanelViewModel.CanModify), changedProperties);

        changedProperties.Clear();
        viewModel.CanSelectReplacementSource = false;
        viewModel.CanApplyReplacement = true;

        Assert.False(viewModel.CanModify);
        Assert.DoesNotContain(nameof(InspectorPanelViewModel.CanModify), changedProperties);
    }

    [Fact]
    public void Apply_ShouldExposeSeparateChangeRecords()
    {
        var viewModel = CreateViewModel();
        var changeRecord = new InspectorChangeRecordViewModel(
            OperationId: "op-1",
            TitleText: "Modified",
            BadgeText: "Effective",
            SourceText: "Source: hero.png",
            TimeText: "Time: now",
            SecondaryText: @"C:\Imports\hero.png",
            ShowsUndoReplacement: true,
            ShowsRedoReplacement: false,
            ShowsDeleteRecord: true,
            IsActionEnabled: true,
            UndoButtonText: "Undo",
            RedoButtonText: "Redo",
            DeleteButtonText: "Delete",
            Glyph: "\uE8B7");

        ApplyBasicDetails(
            viewModel,
            containerPath: string.Empty,
            serializedFileName: "CAB-test",
            changeRecords: [changeRecord]);

        Assert.Single(viewModel.ChangeRecords);
        Assert.Equal(changeRecord, viewModel.ChangeRecords[0]);
        Assert.Equal(Visibility.Visible, viewModel.ChangeRecordsListVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.ChangeRecordsEmptyStateVisibility);
    }

    [Fact]
    public void ChangeRecordPresenter_ShouldKeepOnlyLatestExtractionRecord()
    {
        var presenter = new BundleExplorerAssetChangeRecordPresenter(new TestLocalizationService());
        var oldExtractionAtUtc = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);
        var latestExtractionAtUtc = new DateTimeOffset(2026, 5, 21, 8, 0, 0, TimeSpan.Zero);
        var details = BundleExplorerPreviewTestSupport.CreateAssetDetails(
            new WorkspaceId("workspace-ui"),
            new BundleId("bundle-ui"),
            new AssetId("asset-1")) with
        {
            ChangeRecords =
            [
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.Extraction,
                    CreatedAtUtc: oldExtractionAtUtc,
                    AppliedAtUtc: oldExtractionAtUtc),
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.FileReplacement,
                    CreatedAtUtc: latestExtractionAtUtc.AddMinutes(-10),
                    AppliedAtUtc: latestExtractionAtUtc.AddMinutes(-10),
                    SourceFileName: "hero.png",
                    SourceFilePath: @"D:\Imports\hero.png"),
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.Extraction,
                    CreatedAtUtc: latestExtractionAtUtc,
                    AppliedAtUtc: latestExtractionAtUtc)
            ]
        };

        var records = presenter.CreateChangeRecords(
            details,
            canUndoReplacement: false,
            canRedoReplacement: false,
            canManageRecords: true);

        Assert.Equal(2, records.Count);
        Assert.Equal(
            1,
            records.Count(record => record.TitleText == ResourceKeys.InspectorChangeRecordExtractionTitle));
        Assert.Contains(
            latestExtractionAtUtc.ToLocalTime().ToString("G"),
            records.Single(record => record.TitleText == ResourceKeys.InspectorChangeRecordExtractionTitle).TimeText);
    }

    [Fact]
    public void Apply_ShouldShowAvailablePathsAsSeparateRows()
    {
        var viewModel = CreateViewModel();

        ApplyBasicDetails(
            viewModel,
            containerPath: "assets/ui/button.png",
            serializedFileName: "CAB-test");

        var rows = viewModel.InformationCards.Single(static card => card.Key == "basic").Rows;

        Assert.DoesNotContain(rows, static row => row.Label == "Path");
        Assert.Contains(rows, static row => row.Label == "Container" && row.Value == "assets/ui/button.png");
        Assert.Contains(rows, static row => row.Label == "Serialized File" && row.Value == "CAB-test");
        Assert.Contains(rows, static row => row.Label == "PathId" && row.Value == "1");
    }

    [Fact]
    public void Apply_ShouldHideUnavailablePathRows()
    {
        var viewModel = CreateViewModel();

        ApplyBasicDetails(
            viewModel,
            containerPath: string.Empty,
            serializedFileName: string.Empty);

        var rows = viewModel.InformationCards.Single(static card => card.Key == "basic").Rows;

        Assert.DoesNotContain(rows, static row => row.Label == "Path");
        Assert.DoesNotContain(rows, static row => row.Label == "Container");
        Assert.DoesNotContain(rows, static row => row.Label == "Serialized File");
        Assert.Contains(rows, static row => row.Label == "PathId" && row.Value == "1");
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldMakeOverlayVisible_WhenViewportArrivesAfterPreviewState()
    {
        var viewModel = CreateViewModel();
        ApplyBasicDetails(viewModel, string.Empty, "CAB-test");
        viewModel.ApplyImagePreview(
            CreateBitmapImageStub(),
            imagePixelWidth: 1000,
            imagePixelHeight: 500,
            spritePreviewOverlayDisplayState: new SpritePreviewOverlayDisplayState(
                CanvasWidth: 1000,
                CanvasHeight: 500,
                RegionX: 100,
                RegionY: 50,
                RegionWidth: 200,
                RegionHeight: 100,
                PivotX: null,
                PivotY: null,
                BorderLeft: null,
                BorderTop: null,
                BorderRight: null,
                BorderBottom: null,
                BadgeText: "crop"));

        Assert.Equal(Visibility.Collapsed, viewModel.Preview.Image.OverlayVisibility);

        viewModel.UpdatePreviewImageViewport(300d, 200d);

        Assert.Equal(Visibility.Visible, viewModel.Preview.Image.OverlayVisibility);
        Assert.Equal(30d, viewModel.Preview.Image.OverlayX, precision: 3);
        Assert.Equal(40d, viewModel.Preview.Image.OverlayY, precision: 3);
        Assert.Equal(60d, viewModel.Preview.Image.OverlayWidth, precision: 3);
        Assert.Equal(30d, viewModel.Preview.Image.OverlayHeight, precision: 3);
    }

    [Fact]
    public void PreviewSurfaceHeight_ShouldBeFixedForLoadingImagesAndText_ButAutoForSummaryCardsAndSpine()
    {
        var viewModel = CreateViewModel();

        viewModel.SetPreviewLoading("Loading");

        AssertFixedPreviewSurfaceHeight(viewModel);

        viewModel.ApplyImagePreview(CreateBitmapImageStub(), imagePixelWidth: 100, imagePixelHeight: 100);

        AssertFixedPreviewSurfaceHeight(viewModel);

        viewModel.ApplyTextPreview("plain text");

        AssertFixedPreviewSurfaceHeight(viewModel);

        viewModel.ApplySpineAnimationPreview(
            CreateSpinePreview(),
            new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal));

        AssertAutoPreviewSurfaceHeight(viewModel);

        viewModel.ApplyMaterialPreview(
            CreateEmptyMaterial(),
            materialPreviewDisplayState: CreateMaterialDisplayState());

        AssertAutoPreviewSurfaceHeight(viewModel);

        viewModel.ApplyGameObjectPreview(
            CreateEmptyGameObject(),
            gameObjectPreviewDisplayState: CreateGameObjectDisplayState());

        AssertAutoPreviewSurfaceHeight(viewModel);

        viewModel.ApplyMonoBehaviourPreview(
            CreateEmptyMonoBehaviour(),
            monoBehaviourPreviewDisplayState: CreateMonoBehaviourDisplayState());

        AssertAutoPreviewSurfaceHeight(viewModel);

        viewModel.ApplyAnimatorControllerPreview(
            CreateEmptyAnimatorController(),
            animatorControllerPreviewDisplayState: CreateAnimatorControllerDisplayState());

        AssertAutoPreviewSurfaceHeight(viewModel);
    }

    private static void AssertFixedPreviewSurfaceHeight(InspectorPanelViewModel viewModel)
    {
        Assert.True(viewModel.Preview.UsesFixedPreviewSurfaceHeight);
        Assert.Equal(320d, viewModel.Preview.PreviewSurfaceHeight);
        Assert.Equal(320d, viewModel.Preview.PreviewSurfaceMinHeight);
    }

    private static void AssertAutoPreviewSurfaceHeight(InspectorPanelViewModel viewModel)
    {
        Assert.False(viewModel.Preview.UsesFixedPreviewSurfaceHeight);
        Assert.True(double.IsNaN(viewModel.Preview.PreviewSurfaceHeight));
        Assert.Equal(0d, viewModel.Preview.PreviewSurfaceMinHeight);
    }

    [Fact]
    public void SectionScrollPosition_ShouldBeStoredIndependentlyPerInspector()
    {
        var firstInspector = CreateViewModel();
        var secondInspector = CreateViewModel();

        firstInspector.CaptureSectionSelectorScrollPosition(horizontalOffset: 16d, verticalOffset: 0d);
        firstInspector.CaptureSectionScrollPosition(sectionIndex: 0, horizontalOffset: 12d, verticalOffset: 120d);
        firstInspector.CaptureSectionScrollPosition(sectionIndex: 1, horizontalOffset: 4d, verticalOffset: 40d);
        secondInspector.CaptureSectionScrollPosition(sectionIndex: 0, horizontalOffset: 2d, verticalOffset: 20d);

        Assert.Equal(new InspectorPanelScrollPosition(16d, 0d), firstInspector.GetSectionSelectorScrollPosition());
        Assert.Equal(default(InspectorPanelScrollPosition), secondInspector.GetSectionSelectorScrollPosition());
        Assert.Equal(new InspectorPanelScrollPosition(12d, 120d), firstInspector.GetSectionScrollPosition(0));
        Assert.Equal(new InspectorPanelScrollPosition(4d, 40d), firstInspector.GetSectionScrollPosition(1));
        Assert.Equal(new InspectorPanelScrollPosition(2d, 20d), secondInspector.GetSectionScrollPosition(0));
        Assert.Equal(default(InspectorPanelScrollPosition), secondInspector.GetSectionScrollPosition(1));

        var capturedState = firstInspector.CaptureInspectorScrollState();
        Assert.Equal(16d, capturedState?.SectionSelector?.HorizontalOffset);
        Assert.Equal(0d, capturedState?.SectionSelector?.VerticalOffset);
        Assert.Equal(12d, capturedState?.DetailsSection?.HorizontalOffset);
        Assert.Equal(120d, capturedState?.DetailsSection?.VerticalOffset);
        Assert.Equal(4d, capturedState?.DependenciesSection?.HorizontalOffset);
        Assert.Equal(40d, capturedState?.DependenciesSection?.VerticalOffset);
    }

    [Fact]
    public void Clear_ShouldResetCapturedSectionScrollPositions()
    {
        var viewModel = CreateViewModel();

        viewModel.CaptureSectionSelectorScrollPosition(horizontalOffset: 20d, verticalOffset: 0d);
        viewModel.CaptureSectionScrollPosition(sectionIndex: 0, horizontalOffset: 8d, verticalOffset: 80d);
        viewModel.CaptureSectionScrollPosition(sectionIndex: 3, horizontalOffset: 3d, verticalOffset: 30d);

        viewModel.Clear("Empty");

        Assert.Equal(default(InspectorPanelScrollPosition), viewModel.GetSectionSelectorScrollPosition());
        Assert.Equal(default(InspectorPanelScrollPosition), viewModel.GetSectionScrollPosition(0));
        Assert.Equal(default(InspectorPanelScrollPosition), viewModel.GetSectionScrollPosition(3));
    }

    [Fact]
    public void RestoreInspectorScrollState_ShouldRestoreCapturedSectionOffsets()
    {
        var viewModel = CreateViewModel();

        viewModel.RestoreInspectorScrollState(new WorkspaceInspectorScrollStateDto(
            DetailsSection: new WorkspaceInspectorSectionScrollPositionDto(6d, 160d),
            ChangesSection: new WorkspaceInspectorSectionScrollPositionDto(0d, 44d),
            SectionSelector: new WorkspaceInspectorSectionScrollPositionDto(24d, 0d)));

        Assert.Equal(new InspectorPanelScrollPosition(24d, 0d), viewModel.GetSectionSelectorScrollPosition());
        Assert.Equal(new InspectorPanelScrollPosition(6d, 160d), viewModel.GetSectionScrollPosition(0));
        Assert.Equal(new InspectorPanelScrollPosition(0d, 44d), viewModel.GetSectionScrollPosition(3));
        Assert.Equal(default(InspectorPanelScrollPosition), viewModel.GetSectionScrollPosition(1));
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldProjectOverlayIntoViewportWithoutUpscalingStrokeSpace()
    {
        var projected = TryCalculateLayout(
            canvasWidth: 1000d,
            canvasHeight: 500d,
            regionX: 100d,
            regionY: 50d,
            regionWidth: 200d,
            regionHeight: 100d,
            viewportWidth: 300d,
            viewportHeight: 200d);

        Assert.NotNull(projected);
        Assert.Equal(30d, projected.Value.OverlayX, precision: 3);
        Assert.Equal(40d, projected.Value.OverlayY, precision: 3);
        Assert.Equal(60d, projected.Value.OverlayWidth, precision: 3);
        Assert.Equal(30d, projected.Value.OverlayHeight, precision: 3);
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldKeepSmallImageOverlayAtNativeScale_WhenViewportIsLarger()
    {
        var projected = TryCalculateLayout(
            canvasWidth: 100d,
            canvasHeight: 50d,
            regionX: 10d,
            regionY: 8d,
            regionWidth: 20d,
            regionHeight: 10d,
            viewportWidth: 300d,
            viewportHeight: 200d);

        Assert.NotNull(projected);
        Assert.Equal(110d, projected.Value.OverlayX, precision: 3);
        Assert.Equal(83d, projected.Value.OverlayY, precision: 3);
        Assert.Equal(20d, projected.Value.OverlayWidth, precision: 3);
        Assert.Equal(10d, projected.Value.OverlayHeight, precision: 3);
    }

    private static PreviewImageOverlayLayout? TryCalculateLayout(
        double canvasWidth,
        double canvasHeight,
        double regionX,
        double regionY,
        double regionWidth,
        double regionHeight,
        double viewportWidth,
        double viewportHeight)
    {
        return PreviewImageOverlayLayoutCalculator.TryCalculate(
            canvasWidth,
            canvasHeight,
            regionX,
            regionY,
            regionWidth,
            regionHeight,
            viewportWidth,
            viewportHeight,
            out var layout)
            ? layout
            : null;
    }

    private static void ApplyBasicDetails(
        InspectorPanelViewModel viewModel,
        string containerPath,
        string serializedFileName,
        IEnumerable<InspectorChangeRecordViewModel>? changeRecords = null)
    {
        viewModel.Apply(
            assetName: "Sprite",
            bundleDisplayName: "bundle",
            assetTypeText: "Sprite",
            containerPath: containerPath,
            serializedFileName: serializedFileName,
            sizeText: "1 KB",
            pathIdText: "1",
            extractCapabilityText: string.Empty,
            fileReplacementCapabilityText: string.Empty,
            workspaceReplacementCapabilityText: string.Empty,
            replacementSourceText: string.Empty,
            replacementStatusText: string.Empty,
            changeSummaryText: string.Empty,
            canExtract: false,
            canSelectFileReplacementSource: false,
            canSelectReplacementSource: false,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: Array.Empty<AssetDependencyViewModel>(),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>(),
            changeRecords: changeRecords);
    }

    private static BitmapImage CreateBitmapImageStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }

    private static SpineAnimationPreviewDto CreateSpinePreview()
    {
        var skeletonAsset = new SpineAssetGroupPartDto(
            SpineAssetGroupPartRole.Skeleton,
            new BundleId("bundle"),
            new AssetId("skeleton"),
            "spine.ab",
            "hero.skel",
            "Assets/hero.skel",
            AssetType.TextAsset,
            nameof(AssetType.TextAsset));

        return new SpineAnimationPreviewDto(
            skeletonAsset,
            SpineVersion: "3.8.99",
            Bones: [new SpineAnimationBoneDto("root", null, 0d, 0d, 0d, 1d, 1d)],
            Slots: [new SpineAnimationSlotDto("slot", "root", "body", 0)],
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: Array.Empty<SpineAnimationClipDto>());
    }

    private static AssetPreviewMaterialDto CreateEmptyMaterial()
    {
        return new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties: Array.Empty<AssetPreviewMaterialTexturePropertyDto>(),
            FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties: Array.Empty<AssetPreviewMaterialColorPropertyDto>());
    }

    private static MaterialPreviewDisplayState CreateMaterialDisplayState()
    {
        return new MaterialPreviewDisplayState(
            AssetNameText: "mat",
            ShaderNameText: "shader",
            ShaderReferenceText: string.Empty,
            ShaderResolutionText: string.Empty,
            ResolvedShaderAssetId: null,
            PropertyCountText: "0",
            MainTexturePreviewTitleText: string.Empty,
            MainTexturePreviewLimitText: string.Empty,
            MainTextureTint: null,
            SemanticSectionTitle: string.Empty,
            SemanticBadges: Array.Empty<MaterialPreviewSemanticBadgeDisplayState>(),
            TextureSectionTitle: string.Empty,
            ColorSectionTitle: string.Empty,
            NumberSectionTitle: string.Empty,
            EmptySectionText: string.Empty,
            TruncatedText: string.Empty,
            TextureRows: Array.Empty<MaterialPreviewTextureRowDisplayState>(),
            ColorRows: Array.Empty<MaterialPreviewColorRowDisplayState>(),
            NumberRows: Array.Empty<MaterialPreviewNumberRowDisplayState>(),
            SummaryCards: Array.Empty<MaterialPreviewSummaryCardDisplayState>(),
            DetailCards: Array.Empty<InspectorInfoCardViewModel>());
    }

    private static AssetPreviewGameObjectDto CreateEmptyGameObject()
    {
        return new AssetPreviewGameObjectDto(
            SchemaVersion: 1,
            Name: "go",
            Layer: 0,
            Tag: "Untagged",
            IsActive: true,
            StaticEditorFlags: null,
            ComponentCount: 0,
            Components: Array.Empty<AssetPreviewGameObjectComponentDto>());
    }

    private static GameObjectPreviewDisplayState CreateGameObjectDisplayState()
    {
        return new GameObjectPreviewDisplayState(
            AssetNameText: "go",
            StateText: "active",
            ComponentSummaryText: "0 components",
            TransformSummaryText: string.Empty,
            HierarchySummaryText: string.Empty,
            SummaryCards: Array.Empty<GameObjectPreviewSummaryCardDisplayState>(),
            DetailCards: Array.Empty<InspectorInfoCardViewModel>());
    }

    private static AssetPreviewMonoBehaviourDto CreateEmptyMonoBehaviour()
    {
        return new AssetPreviewMonoBehaviourDto(
            SchemaVersion: 1,
            Name: "behaviour",
            IsEnabled: true,
            Script: null,
            FieldCount: 0,
            ScalarFieldCount: 0,
            ReferenceCount: 0,
            ArrayFieldCount: 0,
            Fields: Array.Empty<AssetPreviewMonoBehaviourFieldDto>(),
            References: Array.Empty<AssetPreviewMonoBehaviourReferenceDto>());
    }

    private static MonoBehaviourPreviewDisplayState CreateMonoBehaviourDisplayState()
    {
        return new MonoBehaviourPreviewDisplayState(
            AssetNameText: "behaviour",
            ScriptText: string.Empty,
            StateText: "enabled",
            FieldSummaryText: "0 fields",
            ReferenceSummaryText: "0 refs",
            SummaryCards: Array.Empty<MonoBehaviourPreviewSummaryCardDisplayState>(),
            DetailCards: Array.Empty<InspectorInfoCardViewModel>());
    }

    private static AssetPreviewAnimatorControllerDto CreateEmptyAnimatorController()
    {
        return new AssetPreviewAnimatorControllerDto(
            SchemaVersion: 1,
            Name: "controller",
            LayerCount: 0,
            ParameterCount: 0,
            StateMachineCount: 0,
            StateCount: 0,
            TransitionCount: 0,
            AnyStateTransitionCount: 0,
            ReferenceCount: 0,
            Layers: Array.Empty<AssetPreviewAnimatorControllerLayerDto>(),
            Parameters: Array.Empty<AssetPreviewAnimatorControllerParameterDto>(),
            References: Array.Empty<AssetPreviewAnimatorControllerReferenceDto>());
    }

    private static AnimatorControllerPreviewDisplayState CreateAnimatorControllerDisplayState()
    {
        return new AnimatorControllerPreviewDisplayState(
            AssetNameText: "controller",
            StructureText: string.Empty,
            StateSummaryText: string.Empty,
            ReferenceSummaryText: string.Empty,
            SummaryCards: Array.Empty<AnimatorControllerPreviewSummaryCardDisplayState>(),
            DetailCards: Array.Empty<InspectorInfoCardViewModel>());
    }

    private static InspectorPanelViewModel CreateViewModel()
    {
        return new InspectorPanelViewModel(
            new InspectorInformationTextSet(
                BasicInfoTitle: "Basic",
                PreviewInfoTitle: "Preview",
                PreviewDiagnosticTitle: "Diagnostic",
                AssetNameLabel: "Name",
                AssetTypeLabel: "Type",
                BundleLabel: "Bundle",
                ContainerPathLabel: "Container",
                SerializedFileLabel: "Serialized File",
                AssetSizeLabel: "Size",
                PathIdLabel: "PathId",
                PreviewCacheStatusLabel: "Cache",
                PreviewFallbackReasonLabel: "Fallback",
                PreviewDiagnosticDetailLabel: "Detail",
                CopyToolTipText: "Copy",
                ItemCountFormat: "{0}"));
    }
}
