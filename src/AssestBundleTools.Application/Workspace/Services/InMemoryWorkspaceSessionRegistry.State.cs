using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Services;

public sealed partial class InMemoryWorkspaceSessionRegistry
{
    private static IReadOnlyList<SampleBundleState> CreateSampleBundles() =>
        new[]
        {
            new SampleBundleState(
                BundleId: new BundleId("bundle.characters.player"),
                DisplayName: "player.ab",
                LogicalPath: "characters/player.ab",
                Assets: new[]
                {
                    new SampleAssetState(
                        AssetId: new AssetId("asset.characters.hero.diffuse"),
                        Name: "Hero_Diffuse",
                        AssetPath: "Assets/Characters/Hero/Hero_Diffuse.png",
                        ContainerPath: "Assets/Characters/Hero/Hero.prefab",
                        AssetType: AssetType.Texture2D,
                        SizeInBytes: 524_288,
                        HasPreview: true,
                        PathId: 1_001,
                        Dependencies: new[]
                        {
                            new AssetDependencyDto(
                                AssetId: new AssetId("asset.characters.hero.material"),
                                BundleId: new BundleId("bundle.characters.player"),
                                Name: "Hero_Material",
                                AssetType: AssetType.Material)
                        }),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.characters.hero.material"),
                        Name: "Hero_Material",
                        AssetPath: "Assets/Characters/Hero/Hero_Material.mat",
                        ContainerPath: "Assets/Characters/Hero/Hero.prefab",
                        AssetType: AssetType.Material,
                        SizeInBytes: 24_576,
                        HasPreview: false,
                        PathId: 1_002,
                        Dependencies: new[]
                        {
                            new AssetDependencyDto(
                                AssetId: new AssetId("asset.characters.hero.diffuse"),
                                BundleId: new BundleId("bundle.characters.player"),
                                Name: "Hero_Diffuse",
                                AssetType: AssetType.Texture2D)
                        }),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.characters.hero.mesh"),
                        Name: "Hero_Mesh",
                        AssetPath: "Assets/Characters/Hero/Hero_Mesh.fbx",
                        ContainerPath: "Assets/Characters/Hero/Hero.prefab",
                        AssetType: AssetType.Mesh,
                        SizeInBytes: 312_944,
                        HasPreview: false,
                        PathId: 1_003,
                        Dependencies: Array.Empty<AssetDependencyDto>())
                }),
            new SampleBundleState(
                BundleId: new BundleId("bundle.ui.common"),
                DisplayName: "common.ab",
                LogicalPath: "ui/common.ab",
                Assets: new[]
                {
                    new SampleAssetState(
                        AssetId: new AssetId("asset.ui.button.sprite"),
                        Name: "Button_Primary",
                        AssetPath: "Assets/UI/Atlas/Button_Primary.sprite",
                        ContainerPath: "Assets/UI/Atlas/CommonAtlas.spriteatlas",
                        AssetType: AssetType.Sprite,
                        SizeInBytes: 18_432,
                        HasPreview: true,
                        PathId: 2_001,
                        Dependencies: new[]
                        {
                            new AssetDependencyDto(
                                AssetId: new AssetId("asset.ui.common.texture"),
                                BundleId: new BundleId("bundle.ui.common"),
                                Name: "CommonAtlas",
                                AssetType: AssetType.Texture2D)
                        }),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.ui.common.texture"),
                        Name: "CommonAtlas",
                        AssetPath: "Assets/UI/Atlas/CommonAtlas.png",
                        ContainerPath: "Assets/UI/Atlas/CommonAtlas.spriteatlas",
                        AssetType: AssetType.Texture2D,
                        SizeInBytes: 262_144,
                        HasPreview: true,
                        PathId: 2_002,
                        Dependencies: Array.Empty<AssetDependencyDto>()),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.ui.localization.table"),
                        Name: "LocalizationTable",
                        AssetPath: "Assets/UI/Config/LocalizationTable.json",
                        ContainerPath: "Assets/UI/Config",
                        AssetType: AssetType.TextAsset,
                        SizeInBytes: 6_144,
                        HasPreview: false,
                        PathId: 2_003,
                        Dependencies: Array.Empty<AssetDependencyDto>())
                }),
            new SampleBundleState(
                BundleId: new BundleId("bundle.audio.bgm"),
                DisplayName: "bgm.ab",
                LogicalPath: "audio/bgm.ab",
                Assets: new[]
                {
                    new SampleAssetState(
                        AssetId: new AssetId("asset.audio.battle.theme"),
                        Name: "BattleTheme",
                        AssetPath: "Assets/Audio/Bgm/BattleTheme.wav",
                        ContainerPath: "Assets/Audio/Bgm",
                        AssetType: AssetType.AudioClip,
                        SizeInBytes: 1_572_864,
                        HasPreview: false,
                        PathId: 3_001,
                        Dependencies: Array.Empty<AssetDependencyDto>()),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.audio.menu.theme"),
                        Name: "MenuTheme",
                        AssetPath: "Assets/Audio/Bgm/MenuTheme.wav",
                        ContainerPath: "Assets/Audio/Bgm",
                        AssetType: AssetType.AudioClip,
                        SizeInBytes: 1_114_112,
                        HasPreview: false,
                        PathId: 3_002,
                        Dependencies: Array.Empty<AssetDependencyDto>())
                }),
            new SampleBundleState(
                BundleId: new BundleId("bundle.environment.city"),
                DisplayName: "city.ab",
                LogicalPath: "environment/city.ab",
                Assets: new[]
                {
                    new SampleAssetState(
                        AssetId: new AssetId("asset.environment.city.mesh"),
                        Name: "City_Block_A",
                        AssetPath: "Assets/Environment/City/City_Block_A.fbx",
                        ContainerPath: "Assets/Environment/City",
                        AssetType: AssetType.Mesh,
                        SizeInBytes: 842_752,
                        HasPreview: false,
                        PathId: 4_001,
                        Dependencies: new[]
                        {
                            new AssetDependencyDto(
                                AssetId: new AssetId("asset.environment.city.material"),
                                BundleId: new BundleId("bundle.environment.city"),
                                Name: "City_Lit",
                                AssetType: AssetType.Material)
                        }),
                    new SampleAssetState(
                        AssetId: new AssetId("asset.environment.city.material"),
                        Name: "City_Lit",
                        AssetPath: "Assets/Environment/City/City_Lit.mat",
                        ContainerPath: "Assets/Environment/City",
                        AssetType: AssetType.Material,
                        SizeInBytes: 32_768,
                        HasPreview: false,
                        PathId: 4_002,
                        Dependencies: Array.Empty<AssetDependencyDto>())
                })
        };

    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(IReadOnlyList<SampleBundleState> bundleStates)
    {
        return bundleStates
            .GroupBy(bundle => bundle.LogicalPath.Split('/')[0], StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var childBundles = group
                    .OrderBy(bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(bundle => new BundleTreeNodeDto(
                        BundleId: bundle.BundleId,
                        DisplayName: bundle.DisplayName,
                        LogicalPath: bundle.LogicalPath,
                        SourcePath: string.Empty,
                        IsVirtualFolder: false,
                        AssetCount: bundle.Assets.Count,
                        Children: Array.Empty<BundleTreeNodeDto>()))
                    .ToArray();

                return new BundleTreeNodeDto(
                    BundleId: new BundleId($"folder.{group.Key}"),
                    DisplayName: group.Key,
                    LogicalPath: group.Key,
                    SourcePath: string.Empty,
                    IsVirtualFolder: true,
                    AssetCount: childBundles.Sum(static node => node.AssetCount),
                    Children: childBundles);
            })
            .ToArray();
    }

    private sealed class WorkspaceSessionState
    {
        public WorkspaceSessionState(
            WorkspaceSummaryDto summary,
            IReadOnlyList<BundleTreeNodeDto> bundleTree,
            IReadOnlyDictionary<string, SampleBundleState> bundlesById)
        {
            Summary = summary;
            BundleTree = bundleTree;
            BundlesById = bundlesById;
        }

        public WorkspaceSummaryDto Summary { get; }

        public IReadOnlyList<BundleTreeNodeDto> BundleTree { get; }

        public IReadOnlyDictionary<string, SampleBundleState> BundlesById { get; }
    }

    private sealed record SampleBundleState(
        BundleId BundleId,
        string DisplayName,
        string LogicalPath,
        IReadOnlyList<SampleAssetState> Assets);

    private readonly record struct AssetTypeFacetKey(
        AssetType AssetType,
        string AssetTypeDisplayName);

    private sealed class AssetTypeFacetKeyComparer : IEqualityComparer<AssetTypeFacetKey>
    {
        public static AssetTypeFacetKeyComparer Instance { get; } = new();

        public bool Equals(AssetTypeFacetKey x, AssetTypeFacetKey y)
        {
            return x.AssetType == y.AssetType &&
                   string.Equals(
                       x.AssetTypeDisplayName,
                       y.AssetTypeDisplayName,
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(AssetTypeFacetKey obj)
        {
            return HashCode.Combine(
                obj.AssetType,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AssetTypeDisplayName));
        }
    }

    private sealed class SampleAssetState
    {
        public SampleAssetState(
            AssetId AssetId,
            string Name,
            string AssetPath,
            string ContainerPath,
            AssetType AssetType,
            long SizeInBytes,
            long PathId,
            bool HasPreview,
            IReadOnlyList<AssetDependencyDto> Dependencies,
            string? AssetTypeDisplayName = null,
            long? PreferredDisplaySizeInBytes = null)
        {
            this.AssetId = AssetId;
            this.Name = Name;
            this.AssetPath = AssetPath;
            this.ContainerPath = ContainerPath;
            this.AssetType = AssetType;
            this.SizeInBytes = SizeInBytes;
            this.PreferredDisplaySizeInBytes = PreferredDisplaySizeInBytes;
            this.PathId = PathId;
            this.HasPreview = HasPreview;
            this.Dependencies = Dependencies;
            this.AssetTypeDisplayName = string.IsNullOrWhiteSpace(AssetTypeDisplayName)
                ? AssetType.ToString()
                : AssetTypeDisplayName.Trim();
        }

        public AssetId AssetId { get; }

        public string Name { get; }

        public string AssetPath { get; }

        public string ContainerPath { get; }

        public AssetType AssetType { get; }

        public string AssetTypeDisplayName { get; }

        public long SizeInBytes { get; }

        public long? PreferredDisplaySizeInBytes { get; }

        public long PathId { get; }

        public bool HasPreview { get; }

        public IReadOnlyList<AssetDependencyDto> Dependencies { get; }

        public bool IsModified { get; set; }

        public DateTimeOffset? LastExtractedAtUtc { get; set; }

        public AppliedWorkspaceReplacementState? AppliedWorkspaceReplacement { get; set; }

        public AppliedFileReplacementState? AppliedFileReplacement { get; set; }

        public AppliedSpriteEditState? AppliedSpriteEdit { get; set; }

        public List<AppliedReplacementSnapshot?> ReplacementUndoStack { get; } = [];

        public List<AppliedReplacementSnapshot?> ReplacementRedoStack { get; } = [];
    }

    private sealed record AppliedWorkspaceReplacementState(
        BundleId SourceBundleId,
        AssetId SourceAssetId,
        string SourceBundleDisplayName,
        string SourceAssetName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedFileReplacementState(
        string SourceFilePath,
        string SourceFileName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedSpriteEditState(
        string DraftId,
        string DraftMode,
        WorkspaceSpriteEditGeometryDto Geometry,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedReplacementSnapshot(
        AppliedWorkspaceReplacementState? WorkspaceReplacement,
        AppliedFileReplacementState? FileReplacement);

    private static void ApplyReplacementSnapshot(
        SampleAssetState assetState,
        AppliedReplacementSnapshot replacementSnapshot)
    {
        assetState.ReplacementUndoStack.Add(CaptureReplacementSnapshot(assetState));
        assetState.ReplacementRedoStack.Clear();
        RestoreReplacementSnapshot(assetState, replacementSnapshot);
    }

    private static void UndoReplacementSnapshot(SampleAssetState assetState)
    {
        var currentReplacement = CaptureReplacementSnapshot(assetState);
        if (currentReplacement is null)
        {
            return;
        }

        assetState.ReplacementRedoStack.Add(currentReplacement);
        RestoreReplacementSnapshot(assetState, PopReplacementSnapshot(assetState.ReplacementUndoStack));
    }

    private static void RedoReplacementSnapshot(SampleAssetState assetState)
    {
        var redoReplacement = PopReplacementSnapshot(assetState.ReplacementRedoStack);
        if (redoReplacement is null)
        {
            return;
        }

        assetState.ReplacementUndoStack.Add(CaptureReplacementSnapshot(assetState));
        RestoreReplacementSnapshot(assetState, redoReplacement);
    }

    private static AppliedReplacementSnapshot? CaptureReplacementSnapshot(SampleAssetState assetState)
    {
        if (assetState.AppliedWorkspaceReplacement is null &&
            assetState.AppliedFileReplacement is null)
        {
            return null;
        }

        return new AppliedReplacementSnapshot(
            assetState.AppliedWorkspaceReplacement,
            assetState.AppliedFileReplacement);
    }

    private static void RestoreReplacementSnapshot(
        SampleAssetState assetState,
        AppliedReplacementSnapshot? replacementSnapshot)
    {
        assetState.AppliedWorkspaceReplacement = replacementSnapshot?.WorkspaceReplacement;
        assetState.AppliedFileReplacement = replacementSnapshot?.FileReplacement;
        RefreshModifiedState(assetState);
    }

    private static void ApplySpriteEditState(
        SampleAssetState assetState,
        AppliedSpriteEditState spriteEdit)
    {
        assetState.AppliedSpriteEdit = spriteEdit;
        RefreshModifiedState(assetState);
    }

    private static void RefreshModifiedState(SampleAssetState assetState)
    {
        assetState.IsModified =
            assetState.AppliedWorkspaceReplacement is not null ||
            assetState.AppliedFileReplacement is not null ||
            assetState.AppliedSpriteEdit is not null;
    }

    private static AppliedReplacementSnapshot? PopReplacementSnapshot(
        IList<AppliedReplacementSnapshot?> stack)
    {
        if (stack.Count == 0)
        {
            return null;
        }

        var snapshot = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return snapshot;
    }
}
