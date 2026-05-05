using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

internal static class BundleExplorerPreviewTestSupport
{
    public static WorkspaceSummaryDto CreateWorkspaceSummary(
        WorkspaceId workspaceId,
        WorkspaceNavigationRequest workspaceRequest)
    {
        return new WorkspaceSummaryDto(
            workspaceId,
            workspaceRequest.WorkspaceDisplayName,
            workspaceRequest.SourcePath,
            WorkspaceMode.SinglePane,
            PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow);
    }

    public static AssetDocumentTabViewModel CreateAssetTab(
        BundleId bundleId,
        AssetId assetId,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            "Button",
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    public static Result<AssetPreviewDto> CreateSummaryPreviewResult(GetAssetPreviewQuery query)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: "text/plain",
            TextContent: "summary",
            BinaryContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None)));
    }

    public static Result<AssetPreviewDto> CreateMeshPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\mesh.json")
    {
        var meshGeometry = new AssetPreviewMeshGeometryDto(
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

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Mesh,
            ContentType: "application/vnd.assestbundletools.mesh-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.MeshVertexCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: meshGeometry.SourceVertexCount)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            MeshGeometry: meshGeometry));
    }

    public static Result<AssetPreviewDto> CreateMaterialPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\material.json")
    {
        var material = new AssetPreviewMaterialDto(
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
                    FileId: 5,
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
                new AssetPreviewMaterialFloatPropertyDto("_Glossiness", 1)
            ],
            IntegerProperties:
            [
                new AssetPreviewMaterialIntegerPropertyDto("_Cull", 2)
            ],
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_Color", 1, 0, 0, 1)
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Material,
            ContentType: "application/vnd.assestbundletools.material-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.MaterialTexturePropertyCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: material.TextureProperties.Count)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            Material: material));
    }

    public static Result<AssetPreviewDto> CreateAnimationClipPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\animation-clip.json")
    {
        var animationClip = new AssetPreviewAnimationClipDto(
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

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.AnimationClip,
            ContentType: "application/vnd.assestbundletools.animation-clip-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AnimationClipEventCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: animationClip.Events.Count)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            AnimationClip: animationClip));
    }

    public static Result<AssetPreviewDto> CreateGameObjectPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\game-object.json")
    {
        var gameObject = new AssetPreviewGameObjectDto(
            SchemaVersion: 1,
            Name: "HeroRoot",
            Layer: 5,
            Tag: "Player",
            IsActive: true,
            StaticEditorFlags: 0,
            ComponentCount: 4,
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
                    TypeId: 33,
                    TypeName: "MeshFilter",
                    Name: "HeroMeshFilter",
                    SerializedFileName: "scene.assets",
                    ExternalReferencePath: null,
                    IsExternal: false,
                    IsEnabled: null,
                    ScriptPathId: null,
                    ScriptName: null,
                    References:
                    [
                        new AssetPreviewGameObjectReferenceDto(
                            Role: "mesh",
                            FileId: 0,
                            PathId: 300,
                            TypeName: "Mesh",
                            Name: "HeroMesh",
                            SerializedFileName: "scene.assets",
                            ExternalReferencePath: null,
                            IsExternal: false)
                    ],
                    Summaries:
                    [
                        new AssetPreviewGameObjectComponentSummaryDto(
                            AssetPreviewGameObjectComponentSummaryKey.MeshReference,
                            AssetPreviewInfoValueKind.Text,
                            StringValue: "HeroMesh")
                    ]),
                new AssetPreviewGameObjectComponentDto(
                    Index: 2,
                    FileId: 0,
                    PathId: 102,
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
                    Index: 3,
                    FileId: 0,
                    PathId: 103,
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

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.GameObject,
            ContentType: "application/vnd.assestbundletools.game-object-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.GameObjectComponentCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: gameObject.ComponentCount)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            GameObject: gameObject));
    }

    public static Result<AssetPreviewDto> CreateMonoBehaviourPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\mono-behaviour.json")
    {
        var monoBehaviour = new AssetPreviewMonoBehaviourDto(
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
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.MonoBehaviour,
            ContentType: "application/vnd.assestbundletools.mono-behaviour-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.MonoBehaviourEnabled,
                    AssetPreviewInfoValueKind.Boolean,
                    BooleanValue: monoBehaviour.IsEnabled)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            MonoBehaviour: monoBehaviour));
    }

    public static Result<AssetPreviewDto> CreateAnimatorControllerPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\animator-controller.json")
    {
        var animatorController = new AssetPreviewAnimatorControllerDto(
            SchemaVersion: 1,
            Name: "HeroAnimator",
            LayerCount: 0,
            ParameterCount: 2,
            StateMachineCount: 1,
            StateCount: 5,
            TransitionCount: 8,
            AnyStateTransitionCount: 2,
            ReferenceCount: 3,
            Layers: Array.Empty<AssetPreviewAnimatorControllerLayerDto>(),
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
                    Name: "Awake",
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
                    Path: "m_AnimationClips.Array.data[1]",
                    Name: "data[1]",
                    FileId: 0,
                    PathId: 701,
                    TypeName: "AnimationClip",
                    TargetName: "HeroAttack",
                    SerializedFileName: "animations.assets",
                    ExternalReferencePath: null,
                    IsExternal: false),
                new AssetPreviewAnimatorControllerReferenceDto(
                    Path: "m_AvatarMask",
                    Name: "m_AvatarMask",
                    FileId: 4,
                    PathId: 900,
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
                    Depth: 6),
                new AssetPreviewAnimatorControllerContainerDto(
                    Path: "m_Controller.m_StateMachineArray.Array.data[0].data.m_TransitionConstantArray",
                    Name: "m_TransitionConstantArray",
                    Kind: AssetPreviewAnimatorControllerContainerKind.Transition,
                    TypeName: "vector",
                    ChildCount: 4,
                    Depth: 6)
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.AnimatorController,
            ContentType: "application/vnd.assestbundletools.animator-controller-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AnimatorControllerStateCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: animatorController.StateCount)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            AnimatorController: animatorController));
    }

    public static Result<AssetPreviewDto> CreateTexturePreviewResult(
        GetAssetPreviewQuery query,
        byte[]? binaryContent = null)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Image,
            ContentType: "image/png",
            TextContent: null,
            BinaryContent: binaryContent ?? [0x89, 0x50, 0x4E, 0x47],
            PreviewPayloadFilePath: @"C:\PreviewCache\texture.png",
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None)));
    }

    public static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException();
            }

            await Task.Delay(10);
        }
    }

    public static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: "Button",
            AssetPath: "Assets/UI/Button.png",
            ContainerPath: "Assets/UI",
            AssetType: AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: null,
            PathId: 1001,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: "Texture2D");
    }
}
