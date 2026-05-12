using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;
using System.Globalization;
using System.Text.Json;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static partial class AssetsToolsRealSampleDependencyTests
{
    private const string SampleBundlePath = @"D:\project\测试ab\char_2014_nian_cfa#1_20260503_231150.ab";
    private const string SampleWorkspaceKey = "real-sample-cone002";
    private const string SampleBundleId = "char_2014_nian_cfa#1_20260503_231150.ab";
    private const string TargetMeshName = "Cone002";
    private const string TargetMaterialName = "paopao_01";
    private const string GameObjectReferenceSampleBundlePath = @"D:\project\测试ab\char_450_necras_summer#20.ab";
    private const string GameObjectReferenceSampleBundleId = "char_450_necras_summer#20.ab";
    private const long GameObjectReferenceMaterialPathId = -1708556679116319340L;

    public static async Task Cone002Mesh_ShouldExposeRendererMaterialPath_ButNoTextureAssociation_FromRealDependencyGraph()
    {
        if (!File.Exists(SampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {SampleBundlePath}");
            return;
        }

        var reader = new AssetsToolsAssetBundleReader();
        var readResult = await reader.ReadAsync(new AssetBundleReadRequestDto(
            BundlePath: SampleBundlePath,
            LogicalPath: Path.GetFileName(SampleBundlePath),
            WorkspaceKey: SampleWorkspaceKey,
            WorkspaceDataRootPath: Path.GetTempPath()));

        TestAssert.True(readResult.IsSuccess, "Sample bundle should be readable by AssetsTools reader.");

        var bundleReadResult = TestAssert.NotNull<AssetBundleReadResultDto>(
            readResult.Value,
            "Reader should return a bundle read result.");
        Console.WriteLine($"[INFO] Bundle external dependencies ({bundleReadResult.Dependencies.Count}): {string.Join("; ", bundleReadResult.Dependencies)}");
        Console.WriteLine($"[INFO] Material assets: {CountAssets(bundleReadResult, AssetType.Material)}; Texture2D assets: {CountAssets(bundleReadResult, AssetType.Texture2D)}; Sprite assets: {CountAssets(bundleReadResult, AssetType.Sprite)}.");
        var materialTexEnvPointers = ProbeMaterialTexEnvPointers(TargetMaterialName);
        Console.WriteLine($"[INFO] {TargetMaterialName} TexEnv PPtrs ({materialTexEnvPointers.Count}): {FormatTexEnvPointers(materialTexEnvPointers)}");
        var coneMesh = bundleReadResult.Assets
            .SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Mesh.ToString() &&
                string.Equals(asset.AssetName, TargetMeshName, StringComparison.Ordinal));

        TestAssert.NotNull(coneMesh, $"Sample bundle should contain one Mesh asset named {TargetMeshName}.");

        var cacheEntry = CreateCacheEntry(SampleBundlePath, bundleReadResult);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([cacheEntry])));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: SampleWorkspaceKey,
            WorkspacePath: Path.GetDirectoryName(SampleBundlePath) ?? string.Empty,
            WorkspaceDataRootPath: Path.GetTempPath(),
            WorkspaceDisplayName: "Real Sample",
            InitialMode: WorkspaceMode.SinglePane));

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(SampleBundleId),
            new AssetId(coneMesh!.AssetId),
            out var details);

        TestAssert.True(hasDetails, $"{TargetMeshName} mesh details should be available from cached workspace state.");
        Console.WriteLine($"[INFO] {TargetMeshName} PathId={details!.PathId}, AssetId={details.AssetId.Value}.");
        Console.WriteLine($"[INFO] Forward dependencies ({details.Dependencies.Count}): {FormatDependencies(details.Dependencies)}");
        Console.WriteLine($"[INFO] Reverse dependencies ({details.ReferencedBy?.Count ?? 0}): {FormatDependencies(details.ReferencedBy)}");
        Console.WriteLine($"[INFO] Dependency closure ({details.DependencyClosure?.Count ?? 0}): {FormatDependencies(details.DependencyClosure)}");
        Console.WriteLine($"[INFO] Texture associations ({details.TextureAssociations?.Count ?? 0}): {FormatDependencies(details.TextureAssociations)}");
        var loggedMaterialAssetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var referrer in details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
        {
            var referrerAsset = FindReadAsset(bundleReadResult, referrer.AssetId.Value);
            Console.WriteLine(
                $"[INFO] Referrer {referrer.Name} dependencies ({referrerAsset?.Dependencies?.Count ?? 0}): {FormatReadDependencies(referrerAsset?.Dependencies)}");

            foreach (var materialDependency in referrerAsset?.Dependencies?
                         .Where(static dependency => dependency.AssetType == AssetType.Material.ToString()) ??
                     Array.Empty<AssetBundleReadAssetDependencyDto>())
            {
                var materialAsset = FindReadAsset(bundleReadResult, materialDependency.AssetId);
                if (!loggedMaterialAssetIds.Add(materialDependency.AssetId))
                {
                    continue;
                }

                Console.WriteLine(
                    $"[INFO] Material {materialDependency.AssetName} dependencies ({materialAsset?.Dependencies?.Count ?? 0}): {FormatReadDependencies(materialAsset?.Dependencies)}");
            }
        }
        TestAssert.True(
            details.ReferencedBy is { Count: > 0 },
            $"{TargetMeshName} should have reverse dependencies in the dependency tree.");
        TestAssert.True(
            details.ReferencedBy!
                .SelectMany(referrer => FindReadAsset(bundleReadResult, referrer.AssetId.Value)?.Dependencies ?? Array.Empty<AssetBundleReadAssetDependencyDto>())
                .Any(static dependency =>
                    dependency.AssetType == AssetType.Material.ToString() &&
                    string.Equals(dependency.AssetName, TargetMaterialName, StringComparison.Ordinal)),
            $"{TargetMeshName} reverse renderer path should reach material {TargetMaterialName}.");
        var paopaoMaterial = TestAssert.NotNull(
            bundleReadResult.Assets.SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Material.ToString() &&
                string.Equals(asset.AssetName, TargetMaterialName, StringComparison.Ordinal)),
            $"Sample bundle should contain Material asset named {TargetMaterialName}.");
        var materialDependencies = paopaoMaterial.Dependencies ?? Array.Empty<AssetBundleReadAssetDependencyDto>();
        var materialExternalDependencies = materialDependencies
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency.ExternalReferencePath))
            .ToArray();
        TestAssert.True(
            materialTexEnvPointers.Count > 0,
            $"{TargetMaterialName} should expose texture PPtrs under m_SavedProperties.m_TexEnvs.");
        TestAssert.True(
            materialTexEnvPointers.All(static pointer => pointer.IsExternal),
            $"{TargetMaterialName} TexEnv PPtrs should target external CAB serialized files in this sample.");
        TestAssert.True(
            materialExternalDependencies.Length > 0,
            $"{TargetMaterialName} TexEnv PPtrs should be preserved as external asset dependency placeholders.");
        var externalDependencyPathIds = materialExternalDependencies
            .Select(static dependency => dependency.PathId)
            .ToHashSet();
        foreach (var texEnvPointer in materialTexEnvPointers)
        {
            TestAssert.True(
                externalDependencyPathIds.Contains(texEnvPointer.PathId),
                $"{TargetMaterialName} TexEnv PPtr {texEnvPointer.TexEnvName} PathId={texEnvPointer.PathId} should be present in Reader dependencies.");
        }

        TestAssert.False(
            materialDependencies.Any(static dependency =>
                dependency.AssetType == AssetType.Texture2D.ToString() ||
                dependency.AssetType == AssetType.Sprite.ToString()),
            $"{TargetMaterialName} should not resolve a concrete Texture2D/Sprite from this single bundle.");
        TestAssert.True(
            details.TextureAssociations is null or { Count: 0 },
            $"{TargetMeshName} should not currently resolve a concrete Texture2D/Sprite association from this single bundle.");
    }

    public static async Task Cone002Mesh_ShouldResolveTextureAssociation_WhenReadableSampleWorkspaceContainsMatchingTextureBundles()
    {
        if (!File.Exists(SampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {SampleBundlePath}");
            return;
        }

        var cacheEntries = await LoadReadableSampleWorkspaceCacheEntriesAsync();
        var workspaceTextureAssetIds = cacheEntries
            .SelectMany(static entry => entry.Assets)
            .Where(static asset =>
                string.Equals(asset.AssetType, AssetType.Texture2D.ToString(), StringComparison.Ordinal) ||
                string.Equals(asset.AssetType, AssetType.Sprite.ToString(), StringComparison.Ordinal))
            .Select(static asset => asset.AssetId)
            .ToHashSet(StringComparer.Ordinal);
        var materialTexEnvPointers = ProbeMaterialTexEnvPointers(TargetMaterialName);
        var expectedTextureAssociationAssetIds = materialTexEnvPointers
            .Select(static pointer => AssetsToolsAssetIdFactory.CreateAssetId(pointer.SerializedFileName, pointer.PathId))
            .Where(workspaceTextureAssetIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine($"[INFO] Readable sample bundles ({cacheEntries.Count}): {string.Join("; ", cacheEntries.Select(static entry => entry.DisplayName))}");
        Console.WriteLine($"[INFO] Readable texture assets ({workspaceTextureAssetIds.Count}).");
        Console.WriteLine($"[INFO] Workspace-matched TexEnv targets for {TargetMaterialName} ({expectedTextureAssociationAssetIds.Length}): {string.Join("; ", expectedTextureAssociationAssetIds)}");

        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore(cacheEntries)));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: SampleWorkspaceKey,
            WorkspacePath: Path.GetDirectoryName(SampleBundlePath) ?? string.Empty,
            WorkspaceDataRootPath: Path.GetTempPath(),
            WorkspaceDisplayName: "Real Sample",
            InitialMode: WorkspaceMode.SinglePane));
        var sampleBundleEntry = TestAssert.NotNull(
            cacheEntries.FirstOrDefault(static entry => string.Equals(entry.BundleId, SampleBundleId, StringComparison.Ordinal)),
            $"Sample bundle cache entry should be present for {SampleBundleId}.");
        var coneMesh = TestAssert.NotNull(
            sampleBundleEntry.Assets.SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Mesh.ToString() &&
                string.Equals(asset.AssetName, TargetMeshName, StringComparison.Ordinal)),
            $"Sample bundle should contain one Mesh asset named {TargetMeshName}.");

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(SampleBundleId),
            new AssetId(coneMesh.AssetId),
            out var details);

        TestAssert.True(hasDetails, $"{TargetMeshName} mesh details should be available from the readable sample workspace.");
        if (expectedTextureAssociationAssetIds.Length > 0)
        {
            TestAssert.True(
                details!.TextureAssociations is { Count: > 0 },
                $"{TargetMeshName} should resolve texture associations when the readable sample workspace contains matching texture bundles.");

            var actualTextureAssociationIds = details.TextureAssociations!
                .Select(static association => association.AssetId.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var expectedAssetId in expectedTextureAssociationAssetIds)
            {
                TestAssert.True(
                    actualTextureAssociationIds.Contains(expectedAssetId),
                    $"{TargetMeshName} should include workspace texture association {expectedAssetId}.");
            }
        }
        else
        {
            TestAssert.True(
                details!.TextureAssociations is null or { Count: 0 },
                $"{TargetMeshName} should still have no concrete texture association because the readable sample workspace does not contain matching texture bundles.");
        }
    }

    public static async Task GameObject_ShouldResolveComponentMaterialPath_FromRealDependencyGraph()
    {
        if (!File.Exists(GameObjectReferenceSampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {GameObjectReferenceSampleBundlePath}");
            return;
        }

        var reader = new AssetsToolsAssetBundleReader();
        var readResult = await reader.ReadAsync(new AssetBundleReadRequestDto(
            BundlePath: GameObjectReferenceSampleBundlePath,
            LogicalPath: Path.GetFileName(GameObjectReferenceSampleBundlePath),
            WorkspaceKey: SampleWorkspaceKey,
            WorkspaceDataRootPath: Path.GetTempPath()));

        TestAssert.True(readResult.IsSuccess, "GameObject reference sample bundle should be readable by AssetsTools reader.");

        var bundleReadResult = TestAssert.NotNull<AssetBundleReadResultDto>(
            readResult.Value,
            "Reader should return a bundle read result.");
        var targetMaterial = TestAssert.NotNull(
            bundleReadResult.Assets.SingleOrDefault(static asset =>
                string.Equals(asset.AssetType, AssetType.Material.ToString(), StringComparison.Ordinal) &&
                asset.PathId == GameObjectReferenceMaterialPathId),
            $"Sample bundle should contain Material PathId={GameObjectReferenceMaterialPathId}.");
        var gameObjectWithMaterialDependency = bundleReadResult.Assets
            .Where(static asset => string.Equals(asset.AssetTypeDisplayName, "GameObject", StringComparison.Ordinal))
            .FirstOrDefault(asset =>
                asset.Dependencies?.Any(dependency =>
                    string.Equals(dependency.AssetId, targetMaterial.AssetId, StringComparison.Ordinal) &&
                    dependency.PathId == GameObjectReferenceMaterialPathId) == true);

        TestAssert.NotNull(
            gameObjectWithMaterialDependency,
            $"At least one GameObject should expose component Material PathId={GameObjectReferenceMaterialPathId} as a reader dependency.");
        Console.WriteLine(
            $"[INFO] GameObject {gameObjectWithMaterialDependency!.AssetName} resolved Material dependency {targetMaterial.AssetName} PathId={targetMaterial.PathId}.");

        var cacheEntry = CreateCacheEntry(GameObjectReferenceSampleBundlePath, bundleReadResult);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([cacheEntry])));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: SampleWorkspaceKey,
            WorkspacePath: Path.GetDirectoryName(GameObjectReferenceSampleBundlePath) ?? string.Empty,
            WorkspaceDataRootPath: Path.GetTempPath(),
            WorkspaceDisplayName: "GameObject Reference Sample",
            InitialMode: WorkspaceMode.SinglePane));

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(GameObjectReferenceSampleBundleId),
            new AssetId(gameObjectWithMaterialDependency.AssetId),
            out var details);

        TestAssert.True(hasDetails, "GameObject details should be available from cached workspace state.");
        var materialDependency = TestAssert.NotNull(
            details!.Dependencies.SingleOrDefault(dependency =>
                dependency.PathId == GameObjectReferenceMaterialPathId),
            $"GameObject details should resolve Material PathId={GameObjectReferenceMaterialPathId}.");
        TestAssert.Equal(targetMaterial.AssetId, materialDependency.AssetId.Value, "Resolved Material dependency should keep the concrete asset id.");
        TestAssert.Equal(GameObjectReferenceSampleBundleId, materialDependency.BundleId.Value, "Resolved Material dependency should point to the sample bundle.");
        TestAssert.Equal(AssetType.Material, materialDependency.AssetType, "Resolved Material dependency should keep the concrete Material type.");
    }

    public static Task MonoBehaviourPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples()
    {
        if (!Directory.Exists(Path.GetDirectoryName(SampleBundlePath)))
        {
            Console.WriteLine($"[SKIP] Sample bundle directory not found: {Path.GetDirectoryName(SampleBundlePath)}");
            return Task.CompletedTask;
        }

        var summaries = new List<MonoBehaviourPreviewProbeSummary>();
        foreach (var bundlePath in EnumerateSampleBundlePaths())
        {
            var bundleSummaries = ProbeMonoBehaviourPreviewSummaries(bundlePath);
            if (bundleSummaries.Count > 0)
            {
                Console.WriteLine($"[INFO] MonoBehaviour candidates {Path.GetFileName(bundlePath)}: {bundleSummaries.Count}.");
                summaries.AddRange(bundleSummaries);
            }
        }

        if (summaries.Count == 0)
        {
            Console.WriteLine("[SKIP] No readable MonoBehaviour assets found in real sample bundles.");
            return Task.CompletedTask;
        }

        var rankedSummaries = summaries
            .OrderByDescending(static summary => summary.Preview.FieldCount)
            .ThenByDescending(static summary => summary.Preview.ArrayFieldCount)
            .ThenByDescending(static summary => summary.Preview.ReferenceCount)
            .ThenBy(static summary => summary.BundleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine(
            $"[INFO] MonoBehaviour probe total={rankedSummaries.Length}, " +
            $"withScript={rankedSummaries.Count(static summary => summary.Preview.Script?.ClassName is { Length: > 0 } || summary.Preview.Script?.Name is { Length: > 0 })}, " +
            $"withContainers={rankedSummaries.Count(static summary => summary.Preview.ArrayFieldCount > 0)}, " +
            $"withReferences={rankedSummaries.Count(static summary => summary.Preview.ReferenceCount > 0)}.");

        foreach (var summary in rankedSummaries.Take(5))
        {
            Console.WriteLine(
                $"[INFO] MonoBehaviour heavy #{Array.IndexOf(rankedSummaries, summary) + 1}: " +
                $"{summary.BundleName}::{summary.AssetName} PathId={summary.PathId}, " +
                $"script={FormatMonoBehaviourProbeScript(summary.Preview.Script)}, " +
                $"fields={summary.Preview.FieldCount}, scalars={summary.Preview.ScalarFieldCount}, " +
                $"arrays={summary.Preview.ArrayFieldCount}, refs={summary.Preview.ReferenceCount}, " +
                $"typetreeNodes={summary.Preview.TypeTreeNodeCount}, " +
                $"truncated(fields={summary.Preview.IsFieldListTruncated}, refs={summary.Preview.IsReferenceListTruncated}).");
            Console.WriteLine($"[INFO]   groups: {FormatMonoBehaviourProbeGroups(summary.Distribution.RootGroups)}");
            Console.WriteLine($"[INFO]   containers: {FormatMonoBehaviourProbeContainers(summary.Distribution.Containers)}");
            Console.WriteLine($"[INFO]   types: {FormatMonoBehaviourProbeTypeCounts(summary.Distribution.TypeCounts)}");
            Console.WriteLine($"[INFO]   fields: {FormatMonoBehaviourProbeFields(summary.Preview.Fields)}");
            Console.WriteLine($"[INFO]   references: {FormatMonoBehaviourProbeReferences(summary.Preview.References)}");
        }

        var heavySummary = rankedSummaries[0];
        TestAssert.Equal(1, heavySummary.Preview.SchemaVersion, "Real MonoBehaviour preview schema should be v1.");
        TestAssert.True(heavySummary.Preview.FieldCount > 0, "Real MonoBehaviour preview should visit at least the root field tree.");
        TestAssert.True(heavySummary.Preview.Fields.Count <= 32, "Real MonoBehaviour preview should keep field rows bounded.");
        TestAssert.True(heavySummary.Preview.References.Count <= 32, "Real MonoBehaviour preview should keep reference rows bounded.");
        TestAssert.True(
            heavySummary.Preview.Script is not null ||
            heavySummary.Preview.ReferenceCount > 0 ||
            heavySummary.Preview.ScalarFieldCount > 0,
            "Real MonoBehaviour preview should expose script, reference, or scalar field data.");
        TestAssert.True(
            rankedSummaries.Any(static summary => summary.Preview.ArrayFieldCount > 0),
            "Real MonoBehaviour samples should include at least one serialized container for preview decisions.");
        TestAssert.True(
            heavySummary.Preview.FieldGroups?.Any(static group => !group.IsUnityBuiltin) == true,
            "Heavy real MonoBehaviour preview should expose user field groups.");
        TestAssert.True(
            rankedSummaries.Any(static summary => summary.Preview.Containers is { Count: > 0 }),
            "Real MonoBehaviour preview should expose serialized container summaries.");
        TestAssert.True(
            !string.IsNullOrWhiteSpace(heavySummary.Preview.TypeTreeJsonText) &&
            heavySummary.Preview.TypeTreeJsonText.Contains("\"m_GameObject\"", StringComparison.Ordinal),
            "Real MonoBehaviour preview should expose a TypeTree JSON field tree.");
        TestAssert.True(
            heavySummary.Preview.TypeTreeNodeCount > 0,
            "Real MonoBehaviour TypeTree JSON should report visited node count.");
        return Task.CompletedTask;
    }

    public static Task AnimatorControllerPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples()
    {
        if (!Directory.Exists(Path.GetDirectoryName(SampleBundlePath)))
        {
            Console.WriteLine($"[SKIP] Sample bundle directory not found: {Path.GetDirectoryName(SampleBundlePath)}");
            return Task.CompletedTask;
        }

        var summaries = new List<AnimatorControllerPreviewProbeSummary>();
        foreach (var bundlePath in EnumerateSampleBundlePaths())
        {
            var bundleSummaries = ProbeAnimatorControllerPreviewSummaries(bundlePath);
            if (bundleSummaries.Count > 0)
            {
                Console.WriteLine($"[INFO] AnimatorController candidates {Path.GetFileName(bundlePath)}: {bundleSummaries.Count}.");
                summaries.AddRange(bundleSummaries);
            }
        }

        if (summaries.Count == 0)
        {
            Console.WriteLine("[SKIP] No readable AnimatorController assets found in real sample bundles.");
            return Task.CompletedTask;
        }

        var rankedSummaries = summaries
            .OrderByDescending(static summary => summary.Preview.StateCount)
            .ThenByDescending(static summary => summary.Preview.TransitionCount)
            .ThenByDescending(static summary => summary.Preview.ReferenceCount)
            .ThenByDescending(static summary => summary.Preview.LayerCount)
            .ThenBy(static summary => summary.BundleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine(
            $"[INFO] AnimatorController probe total={rankedSummaries.Length}, " +
            $"layers={rankedSummaries.Sum(static summary => summary.Preview.LayerCount)}, " +
            $"parameters={rankedSummaries.Sum(static summary => summary.Preview.ParameterCount)}, " +
            $"states={rankedSummaries.Sum(static summary => summary.Preview.StateCount)}, " +
            $"transitions={rankedSummaries.Sum(static summary => summary.Preview.TransitionCount)}, " +
            $"references={rankedSummaries.Sum(static summary => summary.Preview.ReferenceCount)}.");

        foreach (var summary in rankedSummaries.Take(5))
        {
            Console.WriteLine(
                $"[INFO] AnimatorController heavy #{Array.IndexOf(rankedSummaries, summary) + 1}: " +
                $"{summary.BundleName}::{summary.AssetName} PathId={summary.PathId}, " +
                $"layers={summary.Preview.LayerCount}, parameters={summary.Preview.ParameterCount}, " +
                $"stateMachines={summary.Preview.StateMachineCount}, states={summary.Preview.StateCount}, " +
                $"transitions={summary.Preview.TransitionCount}, refs={summary.Preview.ReferenceCount}, " +
                $"truncated(layers={summary.Preview.IsLayerListTruncated}, parameters={summary.Preview.IsParameterListTruncated}, " +
                $"refs={summary.Preview.IsReferenceListTruncated}, containers={summary.Preview.IsContainerListTruncated}).");
            Console.WriteLine($"[INFO]   layers: {FormatAnimatorControllerProbeLayers(summary.Preview.Layers)}");
            Console.WriteLine($"[INFO]   parameters: {FormatAnimatorControllerProbeParameters(summary.Preview.Parameters)}");
            Console.WriteLine($"[INFO]   containers: {FormatAnimatorControllerProbeContainers(summary.Preview.Containers)}");
            Console.WriteLine($"[INFO]   references: {FormatAnimatorControllerProbeReferences(summary.Preview.References)}");
        }

        var heavySummary = rankedSummaries[0];
        TestAssert.Equal(1, heavySummary.Preview.SchemaVersion, "Real AnimatorController preview schema should be v1.");
        TestAssert.True(heavySummary.Preview.Layers.Count <= 24, "Real AnimatorController preview should keep layer rows bounded.");
        TestAssert.True(heavySummary.Preview.Parameters.Count <= 48, "Real AnimatorController preview should keep parameter rows bounded.");
        TestAssert.True(heavySummary.Preview.References.Count <= 48, "Real AnimatorController preview should keep reference rows bounded.");
        TestAssert.True(
            rankedSummaries.Any(static summary =>
                summary.Preview.LayerCount > 0 ||
                summary.Preview.ParameterCount > 0 ||
                summary.Preview.StateCount > 0 ||
                summary.Preview.ReferenceCount > 0),
            "Real AnimatorController preview should expose layer, parameter, state, or reference data.");
        return Task.CompletedTask;
    }

    private static string FormatMonoBehaviourProbeScript(AssetPreviewMonoBehaviourScriptDto? script)
    {
        if (script is null)
        {
            return "<none>";
        }

        if (!string.IsNullOrWhiteSpace(script.ClassName))
        {
            return script.ClassName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(script.Name))
        {
            return script.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(script.SerializedFileName))
        {
            return string.Concat("<", script.SerializedFileName.Trim(), ">");
        }

        return "<unresolved>";
    }

    private static string FormatMonoBehaviourProbeGroups(IReadOnlyList<MonoBehaviourFieldGroupProbe> groups)
    {
        return groups.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                groups.Take(6).Select(static group =>
                    $"{group.Name}: fields={group.TotalFieldCount}, arrays={group.ArrayFieldCount}, refs={group.ReferenceFieldCount}, scalars={group.ScalarFieldCount}"));
    }

    private static string FormatMonoBehaviourProbeContainers(IReadOnlyList<MonoBehaviourContainerProbe> containers)
    {
        return containers.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                containers.Take(6).Select(static container =>
                    $"{container.Path} ({container.TypeName ?? "?"}, count={container.ChildCount.ToString(CultureInfo.InvariantCulture)})"));
    }

    private static string FormatMonoBehaviourProbeTypeCounts(IReadOnlyList<MonoBehaviourTypeCountProbe> typeCounts)
    {
        return typeCounts.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                typeCounts.Take(6).Select(static typeCount =>
                    $"{typeCount.TypeName}={typeCount.Count.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static string FormatMonoBehaviourProbeFields(IReadOnlyList<AssetPreviewMonoBehaviourFieldDto> fields)
    {
        return fields.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                fields.Take(6).Select(static field =>
                    field.ChildCount.HasValue
                        ? $"{field.Path} [{field.ChildCount.Value.ToString(CultureInfo.InvariantCulture)}]"
                        : $"{field.Path}={FormatMonoBehaviourProbeFieldValue(field)}"));
    }

    private static string FormatMonoBehaviourProbeReferences(IReadOnlyList<AssetPreviewMonoBehaviourReferenceDto> references)
    {
        return references.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                references.Take(6).Select(static reference =>
                    $"{reference.Path}->{reference.TypeName ?? reference.TargetName ?? reference.SerializedFileName ?? "?"}#{reference.PathId?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));
    }

    private static string FormatMonoBehaviourProbeFieldValue(AssetPreviewMonoBehaviourFieldDto field)
    {
        return field.ValueKind switch
        {
            AssetPreviewInfoValueKind.Boolean => field.BooleanValue?.ToString(CultureInfo.InvariantCulture) ?? "?",
            AssetPreviewInfoValueKind.Decimal => field.DoubleValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "?",
            AssetPreviewInfoValueKind.Integer => field.Int64Value?.ToString(CultureInfo.InvariantCulture) ?? "?",
            AssetPreviewInfoValueKind.Text => string.IsNullOrWhiteSpace(field.StringValue) ? "?" : field.StringValue.Trim(),
            _ => "?"
        };
    }

    private static string FormatAnimatorControllerProbeLayers(
        IReadOnlyList<AssetPreviewAnimatorControllerLayerDto> layers)
    {
        return layers.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                layers.Take(6).Select(static layer =>
                    $"{layer.Index.ToString(CultureInfo.InvariantCulture)}:{layer.Name ?? "?"} states={layer.StateCount?.ToString(CultureInfo.InvariantCulture) ?? "?"}, transitions={layer.TransitionCount?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));
    }

    private static string FormatAnimatorControllerProbeParameters(
        IReadOnlyList<AssetPreviewAnimatorControllerParameterDto> parameters)
    {
        return parameters.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                parameters.Take(8).Select(static parameter =>
                    $"{parameter.Name ?? "?"}:{parameter.TypeName ?? parameter.Type?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));
    }

    private static string FormatAnimatorControllerProbeContainers(
        IReadOnlyList<AssetPreviewAnimatorControllerContainerDto>? containers)
    {
        return containers is not { Count: > 0 }
            ? "<none>"
            : string.Join(
                "; ",
                containers.Take(8).Select(static container =>
                    $"{container.Kind}:{container.Path} count={container.ChildCount.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static string FormatAnimatorControllerProbeReferences(
        IReadOnlyList<AssetPreviewAnimatorControllerReferenceDto> references)
    {
        return references.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                references.Take(8).Select(static reference =>
                    $"{reference.Path}->{reference.TypeName ?? reference.TargetName ?? reference.SerializedFileName ?? "?"}#{reference.PathId?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));
    }
}
