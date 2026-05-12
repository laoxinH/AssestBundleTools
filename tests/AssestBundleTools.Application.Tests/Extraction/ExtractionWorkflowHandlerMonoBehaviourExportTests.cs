using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Extraction;

internal static partial class ExtractionWorkflowHandlerTests
{
    public static async Task ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsTypeTreeJson_ByDefault()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.behaviours");
        var assetId = new AssetId("asset.behaviour");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Unknown,
            "HeroController",
            "Assets/Scripts/HeroController",
            "MonoBehaviour"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMonoBehaviourPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "MonoBehaviour TypeTree JSON extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("HeroController.typetree.json", StringComparison.Ordinal), "MonoBehaviour default extraction should write TypeTree JSON output.");
        var json = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(json.Contains("\"m_GameObject\"", StringComparison.Ordinal), "TypeTree JSON output should include Unity base fields.");
        TestAssert.True(json.Contains("\"_propertyName\"", StringComparison.Ordinal), "TypeTree JSON output should include script fields.");
        TestAssert.True(json.Contains("\"angle1\": 90", StringComparison.Ordinal), "TypeTree JSON output should preserve scalar script values.");
        TestAssert.False(json.Contains("\"schemaVersion\"", StringComparison.Ordinal), "TypeTree JSON output should not be the summary sidecar projection.");
        TestAssert.Equal(AssetPreviewKind.MonoBehaviour, result.Value?.OutputKind, "MonoBehaviour extraction should report MonoBehaviour output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsYaml_WhenSelectedOutputPathUsesYaml()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.behaviours");
        var assetId = new AssetId("asset.behaviour");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Unknown,
            "HeroController",
            "Assets/Scripts/HeroController",
            "MonoBehaviour"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMonoBehaviourPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());
        const string selectedOutputPath = @"D:\Selected\HeroController.yaml";

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            selectedOutputPath,
            PreserveSuggestedFileName: true));

        TestAssert.True(result.IsSuccess, "MonoBehaviour YAML extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.Equal(selectedOutputPath, writeRequest.SuggestedOutputPath, "YAML extraction should preserve the selected output path.");
        var yaml = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(yaml.Contains("%YAML 1.1", StringComparison.Ordinal), "YAML output should include a YAML header.");
        TestAssert.True(yaml.Contains("script:", StringComparison.Ordinal), "YAML output should include script metadata.");
        TestAssert.True(yaml.Contains("fields:", StringComparison.Ordinal), "YAML output should include field projections.");
        TestAssert.True(yaml.Contains("m_Level", StringComparison.Ordinal), "YAML output should include MonoBehaviour field names.");
        TestAssert.Equal(AssetPreviewKind.MonoBehaviour, result.Value?.OutputKind, "MonoBehaviour extraction should report MonoBehaviour output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsFieldText_WhenSelectedOutputPathUsesTxt()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.behaviours");
        var assetId = new AssetId("asset.behaviour");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Unknown,
            "HeroController",
            "Assets/Scripts/HeroController",
            "MonoBehaviour"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMonoBehaviourPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());
        const string selectedOutputPath = @"D:\Selected\HeroController.txt";

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            selectedOutputPath,
            PreserveSuggestedFileName: true));

        TestAssert.True(result.IsSuccess, "MonoBehaviour field text extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.Equal(selectedOutputPath, writeRequest.SuggestedOutputPath, "Field text extraction should preserve the selected output path.");
        var fields = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(fields.Contains("# AssestBundleTools MonoBehaviour field export", StringComparison.Ordinal), "Field output should identify the export kind.");
        TestAssert.True(fields.Contains("m_Level (int) = 7", StringComparison.Ordinal), "Field output should include scalar fields.");
        TestAssert.True(fields.Contains("m_Target -> Texture2D HeroTexture", StringComparison.Ordinal), "Field output should include object references.");
        TestAssert.Equal(AssetPreviewKind.MonoBehaviour, result.Value?.OutputKind, "MonoBehaviour extraction should report MonoBehaviour output kind.");
    }

    private static AssetPreviewDto CreateMonoBehaviourPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Kind: AssetPreviewKind.MonoBehaviour,
            ContentType: "application/vnd.assestbundletools.mono-behaviour-preview+json",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: CreateDiagnostic(),
            MonoBehaviour: new AssetPreviewMonoBehaviourDto(
                SchemaVersion: 1,
                Name: "HeroController",
                IsEnabled: true,
                Script: new AssetPreviewMonoBehaviourScriptDto(
                    FileId: 0,
                    PathId: 300,
                    Name: "HeroController",
                    ClassName: "HeroController",
                    Namespace: "Game",
                    AssemblyName: "Assembly-CSharp",
                    SerializedFileName: "bundle.assets",
                    ExternalReferencePath: null,
                    IsExternal: false),
                FieldCount: 4,
                ScalarFieldCount: 2,
                ReferenceCount: 1,
                ArrayFieldCount: 1,
                Fields:
                [
                    new AssetPreviewMonoBehaviourFieldDto(
                        Path: "m_Level",
                        Name: "m_Level",
                        TypeName: "int",
                        ValueKind: AssetPreviewInfoValueKind.Integer,
                        Int64Value: 7),
                    new AssetPreviewMonoBehaviourFieldDto(
                        Path: "m_Title",
                        Name: "m_Title",
                        TypeName: "string",
                        ValueKind: AssetPreviewInfoValueKind.Text,
                        StringValue: "Amiya"),
                    new AssetPreviewMonoBehaviourFieldDto(
                        Path: "m_Items",
                        Name: "m_Items",
                        TypeName: "Array",
                        ValueKind: AssetPreviewInfoValueKind.Integer,
                        ChildCount: 2)
                ],
                References:
                [
                    new AssetPreviewMonoBehaviourReferenceDto(
                        Path: "m_Target",
                        Name: "m_Target",
                        FileId: 0,
                        PathId: 42,
                        TypeName: "Texture2D",
                        TargetName: "HeroTexture",
                        SerializedFileName: "bundle.assets",
                        ExternalReferencePath: null,
                        IsExternal: false)
                ],
                FieldGroups:
                [
                    new AssetPreviewMonoBehaviourFieldGroupDto(
                        Name: "m_Level",
                        FieldCount: 1,
                        ScalarFieldCount: 1,
                        ReferenceCount: 0,
                        ArrayFieldCount: 0)
                ],
                Containers:
                [
                    new AssetPreviewMonoBehaviourContainerDto(
                        Path: "m_Items",
                        Name: "m_Items",
                        TypeName: "Array",
                        ChildCount: 2,
                        Depth: 1)
                ],
                TypeTreeJsonText: """
                {
                  "m_GameObject": {
                    "m_FileID": 0,
                    "m_PathID": -1531058549578097814
                  },
                  "m_Enabled": 1,
                  "m_Script": {
                    "m_FileID": 1,
                    "m_PathID": 7163214010000414217
                  },
                  "m_Name": "",
                  "_propertyName": "_Rotation",
                  "_rotateTex1": 1,
                  "angle1": 90.0
                }
                """,
                TypeTreeNodeCount: 8,
                IsTypeTreeJsonTruncated: false));
}
