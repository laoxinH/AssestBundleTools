using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    private static readonly JsonSerializerOptions TestMeshPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestMaterialPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestAnimationClipPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestFontPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestGameObjectPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestMonoBehaviourPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestAnimatorControllerPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static GetAssetPreviewQuery CreateQuery()
    {
        return new GetAssetPreviewQuery(
            WorkspaceId: new WorkspaceId("workspace-session-1"),
            WorkspaceKey: "workspace-key-1",
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.001"),
            AssetId: new AssetId("asset.001"));
    }

    private static BundleIndexCacheEntryDto CreateBundleCacheEntry(
        string workspaceKey,
        string bundleId,
        string assetId)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-001",
            BundleId: bundleId,
            DisplayName: "characters",
            LogicalPath: "characters.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: @"C:\Bundles\characters.ab",
                FileSizeInBytes: 1024,
                LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero),
                ContentHash: "hash-characters"),
            CachedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 5, 0, TimeSpan.Zero),
            ParserVersion: "parser-1",
            ToolVersion: "tool-1",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                new BundleIndexAssetCacheEntryDto(
                    AssetId: assetId,
                    AssetName: "HeroPortrait",
                    AssetType: AssetType.Texture2D.ToString(),
                    AssetPath: "Assets/HeroPortrait.png",
                    ContainerPath: null,
                    SizeInBytes: 256,
                    PreferredDisplaySizeInBytes: 256,
                    HasPreview: true,
                    CanExtract: true,
                    CanReplace: true,
                    SerializedFileName: "characters.assets",
                    PathId: 12345,
                    AssetTypeDisplayName: "Texture2D")
            ],
            DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            DecryptorVersion: "1.0.0");
    }

    private static AssetDetailsDto CreateAssetDetails(
        GetAssetPreviewQuery query,
        string? name = null,
        AssetType assetType = AssetType.Texture2D,
        string? fileReplacementSourceFileName = null,
        string? fileReplacementSourcePath = null,
        BundleId? workspaceReplacementSourceBundleId = null,
        AssetId? workspaceReplacementSourceAssetId = null,
        string? workspaceReplacementSourceBundleDisplayName = null,
        string? workspaceReplacementSourceAssetName = null,
        IReadOnlyList<AssetDependencyDto>? dependencies = null,
        IReadOnlyList<AssetDependencyDto>? textureAssociations = null,
        string? textEditDraftId = null,
        string? textEditDraftMode = null,
        DateTimeOffset? textEditAppliedAtUtc = null,
        int? textEditDraftRevision = null)
    {
        var hasFileReplacement = !string.IsNullOrWhiteSpace(fileReplacementSourcePath);
        var hasWorkspaceReplacement = workspaceReplacementSourceBundleId.HasValue &&
                                      workspaceReplacementSourceAssetId.HasValue;
        return new AssetDetailsDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Name: name ?? "HeroPortrait",
            AssetPath: "Assets/HeroPortrait.png",
            ContainerPath: "Assets",
            AssetType: assetType,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: 256,
            PathId: 12345,
            IsModified: hasFileReplacement || hasWorkspaceReplacement || textEditAppliedAtUtc.HasValue,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: workspaceReplacementSourceBundleDisplayName,
            WorkspaceReplacementSourceAssetName: workspaceReplacementSourceAssetName,
            WorkspaceReplacementSourceAssetType: hasWorkspaceReplacement ? assetType : null,
            FileReplacementSourceFileName: fileReplacementSourceFileName,
            FileReplacementSourcePath: fileReplacementSourcePath,
            FileReplacementSourceAssetType: hasFileReplacement ? assetType : null,
            LastExtractedAtUtc: null,
            Dependencies: dependencies ?? Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetType.ToString(),
            TextureAssociations: textureAssociations,
            WorkspaceReplacementSourceBundleId: workspaceReplacementSourceBundleId,
            WorkspaceReplacementSourceAssetId: workspaceReplacementSourceAssetId,
            TextEditDraftId: textEditDraftId,
            TextEditDraftMode: textEditDraftMode,
            TextEditAppliedAtUtc: textEditAppliedAtUtc,
            TextEditDraftRevision: textEditDraftRevision);
    }

    private static BundleIndexAssetCacheEntryDto CreateAssetCacheEntry(string assetId)
    {
        return new BundleIndexAssetCacheEntryDto(
            AssetId: assetId,
            AssetName: "HeroPortrait",
            AssetType: AssetType.Texture2D.ToString(),
            AssetPath: "Assets/HeroPortrait.png",
            ContainerPath: null,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: 256,
            HasPreview: true,
            CanExtract: true,
            CanReplace: true,
            SerializedFileName: "characters.assets",
            PathId: 12345,
            AssetTypeDisplayName: AssetType.Texture2D.ToString());
    }

    private static AssetBundleDecryptorDescriptorDto CreatePassThroughDecryptorDescriptor()
    {
        return new AssetBundleDecryptorDescriptorDto(
            AssetBundleDecryptorIds.BuiltInPassThrough,
            "PassThrough",
            "1.0.0",
            AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: false,
            IsFallback: true,
            Description: "fallback");
    }

    private static PreviewCachePayloadDto CreateTextPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string textContent,
        string generatorVersion)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Text.ToString(),
                ContentType: "text/plain; charset=utf-8",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.txt"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion),
            Encoding.UTF8.GetBytes(textContent));
    }

    private static PreviewCachePayloadDto CreateImagePreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewImageOverlayDto? imageOverlay,
        AssetPreviewImageFocusDto? imageFocus = null,
        byte[]? imageFocusContent = null)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Image.ToString(),
                ContentType: "image/png",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.png"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                ImageOverlay: imageOverlay,
                ImageFocus: imageFocus),
            [0x89, 0x50, 0x4E, 0x47],
            imageFocusContent);
    }

    private static PreviewCachePayloadDto CreateMeshPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewMeshGeometryDto meshGeometry)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Mesh.ToString(),
                ContentType: "application/vnd.assestbundletools.mesh-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.mesh.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MeshVertexCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: meshGeometry.SourceVertexCount)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                meshGeometry,
                TestMeshPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateMaterialPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewMaterialDto material)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Material.ToString(),
                ContentType: "application/vnd.assestbundletools.material-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.material.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MaterialTexturePropertyCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: material.TextureProperties.Count)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                material,
                TestMaterialPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateAnimationClipPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewAnimationClipDto animationClip)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.AnimationClip.ToString(),
                ContentType: "application/vnd.assestbundletools.animation-clip-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.animation-clip.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.AnimationClipEventCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: animationClip.Events.Count)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                animationClip,
                TestAnimationClipPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateFontPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewFontDto font)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Font.ToString(),
                ContentType: "application/vnd.assestbundletools.font-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.font.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.FontCharacterCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: font.CharacterCount),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.FontTexturePathId,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: font.Texture?.PathId),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.FontDataSize,
                        AssetPreviewInfoValueKind.ByteSize,
                        Int64Value: font.FontDataSizeInBytes)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                font,
                TestFontPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateGameObjectPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewGameObjectDto gameObject)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.GameObject.ToString(),
                ContentType: "application/vnd.assestbundletools.game-object-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.game-object.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.GameObjectComponentCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: gameObject.ComponentCount),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.GameObjectLayer,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: gameObject.Layer),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.GameObjectIsActive,
                        AssetPreviewInfoValueKind.Boolean,
                        BooleanValue: gameObject.IsActive)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                gameObject,
                TestGameObjectPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateMonoBehaviourPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewMonoBehaviourDto monoBehaviour)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.MonoBehaviour.ToString(),
                ContentType: "application/vnd.assestbundletools.mono-behaviour-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.mono-behaviour.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 5, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MonoBehaviourEnabled,
                        AssetPreviewInfoValueKind.Boolean,
                        BooleanValue: monoBehaviour.IsEnabled),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MonoBehaviourScriptPathId,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: monoBehaviour.Script?.PathId)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                monoBehaviour,
                TestMonoBehaviourPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateAnimatorControllerPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewAnimatorControllerDto animatorController)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.AnimatorController.ToString(),
                ContentType: "application/vnd.assestbundletools.animator-controller-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.animator-controller.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 5, 11, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.AnimatorControllerLayerCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: animatorController.LayerCount),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.AnimatorControllerParameterCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: animatorController.ParameterCount),
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.AnimatorControllerStateCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: animatorController.StateCount)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                animatorController,
                TestAnimatorControllerPreviewJsonSerializerOptions));
    }

    private static AssetPreviewMaterialDto CreateMaterialPreview()
    {
        return new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: new AssetPreviewMaterialShaderDto(
                FileId: 0,
                PathId: 100,
                Name: "Particles/Additive",
                SerializedFileName: "materials.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            TextureProperties:
            [
                new AssetPreviewMaterialTexturePropertyDto(
                    Name: "_MainTex",
                    FileId: 2,
                    PathId: 200,
                    SerializedFileName: "CAB-textures",
                    ExternalReferencePath: "archive:/CAB-textures/CAB-textures",
                    IsExternal: true,
                    ScaleX: 1,
                    ScaleY: 1,
                    OffsetX: 0,
                    OffsetY: 0)
            ],
            FloatProperties:
            [
                new AssetPreviewMaterialFloatPropertyDto("_Glossiness", 0.5d)
            ],
            IntegerProperties:
            [
                new AssetPreviewMaterialIntegerPropertyDto("_Cull", 2)
            ],
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_Color", 1, 0.25d, 0.5d, 1)
            ]);
    }

    private static AssetPreviewAnimationClipDto CreateAnimationClipPreview()
    {
        return new AssetPreviewAnimationClipDto(
            SchemaVersion: 1,
            SampleRate: 30,
            StartTime: 0,
            StopTime: 1.5,
            Duration: 1.5,
            EstimatedFrameCount: 45,
            IsLegacy: false,
            LoopTime: true,
            LoopBlend: false,
            LoopBlendOrientation: true,
            LoopBlendPositionY: false,
            LoopBlendPositionXZ: true,
            FloatCurveCount: 8,
            PositionCurveCount: 2,
            RotationCurveCount: 3,
            ScaleCurveCount: 1,
            PPtrCurveCount: 0,
            Events:
            [
                new AssetPreviewAnimationEventDto(
                    Time: 0.25,
                    FunctionName: "OnStep",
                    StringParameter: "left",
                    FloatParameter: 0.5,
                    IntParameter: 2)
            ],
            EventCount: 1,
            CurveBindingCount: 3,
            CurveBindings:
            [
                new AssetPreviewAnimationCurveBindingDto(
                    AssetPreviewAnimationCurveBindingKind.Float,
                    Path: "Root/Hips",
                    Attribute: "m_LocalPosition.x",
                    ClassId: 4,
                    ScriptFileId: null,
                    ScriptPathId: null,
                    PathHash: null,
                    AttributeId: null,
                    TypeId: null,
                    CustomType: null,
                    IsPPtrCurve: false,
                    CurveCount: 1,
                    KeyframeCount: 4),
                new AssetPreviewAnimationCurveBindingDto(
                    AssetPreviewAnimationCurveBindingKind.Rotation,
                    Path: "Root/Spine",
                    Attribute: null,
                    ClassId: 4,
                    ScriptFileId: null,
                    ScriptPathId: null,
                    PathHash: null,
                    AttributeId: null,
                    TypeId: null,
                    CustomType: null,
                    IsPPtrCurve: false,
                    CurveCount: 4,
                    KeyframeCount: 12),
                new AssetPreviewAnimationCurveBindingDto(
                    AssetPreviewAnimationCurveBindingKind.Generic,
                    Path: null,
                    Attribute: null,
                    ClassId: null,
                    ScriptFileId: null,
                    ScriptPathId: null,
                    PathHash: 1387920772,
                    AttributeId: 1895888582,
                    TypeId: 23,
                    CustomType: 22,
                    IsPPtrCurve: false,
                    CurveCount: 1,
                    KeyframeCount: null)
            ]);
    }

    private static AssetPreviewFontDto CreateFontPreview()
    {
        return new AssetPreviewFontDto(
            SchemaVersion: 1,
            FontName: "LiberationSans SDF",
            FontSize: 18,
            LineSpacing: 21.5,
            Ascent: 17,
            Descent: -4,
            DefaultSize: 16,
            AsciiStartOffset: 32,
            Tracking: 1.25,
            CharacterSpacing: 2,
            CharacterPadding: 1,
            CharacterCount: 72,
            FontDataSizeInBytes: 4096,
            Texture: new AssetPreviewFontReferenceDto(
                FileId: 2,
                PathId: 300,
                Name: "LiberationSans Atlas",
                SerializedFileName: "font-textures.assets",
                ExternalReferencePath: "archive:/font-textures/font-textures.assets",
                IsExternal: true),
            DefaultMaterial: new AssetPreviewFontReferenceDto(
                FileId: 0,
                PathId: 301,
                Name: "LiberationSans Material",
                SerializedFileName: "fonts.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            Characters:
            [
                new AssetPreviewFontCharacterDto(
                    CodePoint: 65,
                    DisplayText: "A",
                    UvX: 0.1,
                    UvY: 0.2,
                    UvWidth: 0.05,
                    UvHeight: 0.06,
                    VertexX: -1,
                    VertexY: -2,
                    VertexWidth: 8,
                    VertexHeight: 9,
                    Advance: 8.5,
                    IsFlipped: false),
                new AssetPreviewFontCharacterDto(
                    CodePoint: 66,
                    DisplayText: "B",
                    UvX: 0.2,
                    UvY: 0.3,
                    UvWidth: 0.04,
                    UvHeight: 0.06,
                    VertexX: -1,
                    VertexY: -2,
                    VertexWidth: 7,
                    VertexHeight: 9,
                    Advance: 7.75,
                    IsFlipped: true)
            ],
            IsCharacterListTruncated: true);
    }

    private static AssetPreviewGameObjectDto CreateGameObjectPreview()
    {
        return new AssetPreviewGameObjectDto(
            SchemaVersion: 1,
            Name: "HeroRoot",
            Layer: 5,
            Tag: "Player",
            IsActive: true,
            StaticEditorFlags: 0,
            ComponentCount: 3,
            Components:
            [
                new AssetPreviewGameObjectComponentDto(
                    Index: 0,
                    FileId: 0,
                    PathId: 100,
                    TypeId: 4,
                    TypeName: "Transform",
                    Name: null,
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false,
                    IsEnabled: null,
                    ScriptPathId: null,
                    ScriptName: null),
                new AssetPreviewGameObjectComponentDto(
                    Index: 1,
                    FileId: 0,
                    PathId: 101,
                    TypeId: 23,
                    TypeName: "MeshRenderer",
                    Name: "HeroRenderer",
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false,
                    IsEnabled: true,
                    ScriptPathId: null,
                    ScriptName: null,
                    References:
                    [
                        new AssetPreviewGameObjectReferenceDto(
                            Role: "material",
                            FileId: 0,
                            PathId: 200,
                            TypeName: "Material",
                            Name: "HeroMaterial",
                            SerializedFileName: "scene.assets",
                            ExternalReferencePath: null,
                            IsExternal: false)
                    ],
                    Summaries:
                    [
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.MaterialCount,
                            AssetPreviewInfoValueKind.Integer,
                            Int64Value: 1),
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.Enabled,
                            AssetPreviewInfoValueKind.Boolean,
                            BooleanValue: true),
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.ReceiveShadows,
                            AssetPreviewInfoValueKind.Boolean,
                            BooleanValue: true)
                    ]),
                new AssetPreviewGameObjectComponentDto(
                    Index: 2,
                    FileId: 0,
                    PathId: 102,
                    TypeId: 114,
                    TypeName: "MonoBehaviour",
                    Name: "HeroLogic",
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false,
                    IsEnabled: true,
                    ScriptPathId: 400,
                    ScriptName: "HeroController",
                    References:
                    [
                        new AssetPreviewGameObjectReferenceDto(
                            Role: "script",
                            FileId: 0,
                            PathId: 400,
                            TypeName: "MonoScript",
                            Name: "HeroController",
                            SerializedFileName: "scene.assets",
                            ExternalReferencePath: null,
                            IsExternal: false)
                    ],
                    Summaries:
                    [
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.Script,
                            AssetPreviewInfoValueKind.Text,
                            StringValue: "HeroController"),
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.Enabled,
                            AssetPreviewInfoValueKind.Boolean,
                            BooleanValue: true)
                    ])
            ],
            IsComponentListTruncated: false,
            Transform: new AssetPreviewGameObjectTransformDto(
                PathId: 100,
                TypeName: "Transform",
                ParentPathId: null,
                ChildCount: 2,
                LocalPositionX: 1,
                LocalPositionY: 2,
                LocalPositionZ: 3,
                LocalRotationX: 0,
                LocalRotationY: 0,
                LocalRotationZ: 0,
                LocalRotationW: 1,
                LocalScaleX: 1,
                LocalScaleY: 1,
                LocalScaleZ: 1));
    }

    private static AssetPreviewMonoBehaviourDto CreateMonoBehaviourPreview()
    {
        return new AssetPreviewMonoBehaviourDto(
            SchemaVersion: 1,
            Name: "HeroLogic",
            IsEnabled: true,
            Script: new AssetPreviewMonoBehaviourScriptDto(
                FileId: 0,
                PathId: 400,
                Name: "HeroController",
                ClassName: "HeroController",
                Namespace: "Game.Logic",
                AssemblyName: "Assembly-CSharp.dll",
                SerializedFileName: "scene.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            FieldCount: 8,
            ScalarFieldCount: 2,
            ReferenceCount: 2,
            ArrayFieldCount: 1,
            Fields:
            [
                new AssetPreviewMonoBehaviourFieldDto(
                    Path: "m_Health",
                    Name: "m_Health",
                    TypeName: "int",
                    ValueKind: AssetPreviewInfoValueKind.Integer,
                    Int64Value: 100),
                new AssetPreviewMonoBehaviourFieldDto(
                    Path: "m_DisplayName",
                    Name: "m_DisplayName",
                    TypeName: "string",
                    ValueKind: AssetPreviewInfoValueKind.Text,
                    StringValue: "Hero"),
                new AssetPreviewMonoBehaviourFieldDto(
                    Path: "m_Waypoints",
                    Name: "m_Waypoints",
                    TypeName: "Array",
                    ValueKind: AssetPreviewInfoValueKind.Integer,
                    ChildCount: 2)
            ],
            References:
            [
                new AssetPreviewMonoBehaviourReferenceDto(
                    Path: "m_Script",
                    Name: "m_Script",
                    FileId: 0,
                    PathId: 400,
                    TypeName: "MonoScript",
                    TargetName: "HeroController",
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false),
                new AssetPreviewMonoBehaviourReferenceDto(
                    Path: "m_Material",
                    Name: "m_Material",
                    FileId: 0,
                    PathId: 200,
                    TypeName: "Material",
                    TargetName: "HeroMaterial",
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false)
            ],
            FieldGroups:
            [
                new AssetPreviewMonoBehaviourFieldGroupDto(
                    Name: "m_Health",
                    FieldCount: 1,
                    ScalarFieldCount: 1,
                    ReferenceCount: 0,
                    ArrayFieldCount: 0),
                new AssetPreviewMonoBehaviourFieldGroupDto(
                    Name: "m_Waypoints",
                    FieldCount: 3,
                    ScalarFieldCount: 0,
                    ReferenceCount: 0,
                    ArrayFieldCount: 1)
            ],
            Containers:
            [
                new AssetPreviewMonoBehaviourContainerDto(
                    Path: "m_Waypoints",
                    Name: "m_Waypoints",
                    TypeName: "Array",
                    ChildCount: 2,
                    Depth: 1)
            ],
            TypeTreeJsonText: """
            {
              "m_GameObject": {
                "m_FileID": 0,
                "m_PathID": 120
              },
              "m_Enabled": 1,
              "m_Name": "HeroLogic",
              "m_Health": 100
            }
            """,
            TypeTreeNodeCount: 6,
            IsTypeTreeJsonTruncated: false);
    }

    private static AssetPreviewAnimatorControllerDto CreateAnimatorControllerPreview()
    {
        return new AssetPreviewAnimatorControllerDto(
            SchemaVersion: 1,
            Name: "HeroAnimator",
            LayerCount: 1,
            ParameterCount: 2,
            StateMachineCount: 1,
            StateCount: 3,
            TransitionCount: 4,
            AnyStateTransitionCount: 1,
            ReferenceCount: 2,
            Layers:
            [
                new AssetPreviewAnimatorControllerLayerDto(
                    Index: 0,
                    Name: "Base Layer",
                    DefaultWeight: 1,
                    BlendingMode: 0,
                    SyncedLayerIndex: -1,
                    IkPass: false,
                    StateMachinePathId: 600,
                    AvatarMaskPathId: null,
                    StateCount: 3,
                    TransitionCount: 4)
            ],
            Parameters:
            [
                new AssetPreviewAnimatorControllerParameterDto(
                    Index: 0,
                    Name: "Speed",
                    Type: 1,
                    TypeName: "Float",
                    DefaultFloat: 1,
                    DefaultInt: 0,
                    DefaultBool: false),
                new AssetPreviewAnimatorControllerParameterDto(
                    Index: 1,
                    Name: "IsAttacking",
                    Type: 4,
                    TypeName: "Bool",
                    DefaultFloat: 0,
                    DefaultInt: 0,
                    DefaultBool: true)
            ],
            References:
            [
                new AssetPreviewAnimatorControllerReferenceDto(
                    Path: "m_AnimationClips.Array.data[0]",
                    Name: "data[0]",
                    FileId: 0,
                    PathId: 700,
                    TypeName: "AnimationClip",
                    TargetName: "HeroIdle",
                    SerializedFileName: "animations.assets",
                    ExternalReferencePath: null,
                    IsExternal: false),
                new AssetPreviewAnimatorControllerReferenceDto(
                    Path: "m_AvatarMask",
                    Name: "m_AvatarMask",
                    FileId: 2,
                    PathId: 800,
                    TypeName: "AvatarMask",
                    TargetName: null,
                    SerializedFileName: "masks.assets",
                    ExternalReferencePath: "archive:/masks.assets",
                    IsExternal: true)
            ],
            Containers:
            [
                new AssetPreviewAnimatorControllerContainerDto(
                    Path: "m_Controller.m_StateMachineArray",
                    Name: "m_StateMachineArray",
                    Kind: AssetPreviewAnimatorControllerContainerKind.StateMachine,
                    TypeName: "vector",
                    ChildCount: 1,
                    Depth: 2),
                new AssetPreviewAnimatorControllerContainerDto(
                    Path: "m_Controller.m_StateMachineArray.Array.data[0].data.m_StateConstantArray",
                    Name: "m_StateConstantArray",
                    Kind: AssetPreviewAnimatorControllerContainerKind.State,
                    TypeName: "vector",
                    ChildCount: 3,
                    Depth: 6)
            ]);
    }

    private static AssetPreviewMeshGeometryDto CreateMeshGeometry()
    {
        return new AssetPreviewMeshGeometryDto(
            SchemaVersion: 3,
            SourceVertexCount: 3,
            SourceIndexCount: 3,
            SourceTriangleCount: 1,
            IsGeometryTruncated: false,
            Vertices:
            [
                new AssetPreviewMeshVertexDto(0, 0, 0),
                new AssetPreviewMeshVertexDto(1, 0, 0),
                new AssetPreviewMeshVertexDto(0, 1, 0)
            ],
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(0, 2)
            ],
            SubMeshes:
            [
                new AssetPreviewMeshSubMeshDto(
                    Index: 0,
                    FirstIndexByte: 0,
                    IndexCount: 3,
                    Topology: 0,
                    BaseVertex: 0,
                    FirstVertex: 0,
                    VertexCount: 3)
            ],
            Bounds: new AssetPreviewMeshBoundsDto(
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0),
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0)),
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels:
            [
                new AssetPreviewMeshChannelDto(
                    Index: 0,
                    Stream: 0,
                    Offset: 0,
                    Format: 0,
                    Dimension: 3,
                    IsPreviewReadable: true),
                new AssetPreviewMeshChannelDto(
                    Index: 4,
                    Stream: 0,
                    Offset: 12,
                    Format: 0,
                    Dimension: 2,
                    IsPreviewReadable: true)
            ],
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
            ]);
    }

    private static string CreatePreviewCacheKey(string bundleCacheKey, string assetId)
    {
        var normalizedKey = $"{bundleCacheKey.ToLowerInvariant()}::{assetId.ToLowerInvariant()}";
        var bytes = Encoding.UTF8.GetBytes(normalizedKey);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

}
