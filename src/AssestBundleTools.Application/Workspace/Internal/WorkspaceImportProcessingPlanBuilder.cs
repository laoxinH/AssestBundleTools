using System.Diagnostics;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportProcessingPlanBuilder
{
    private const string ToolSchemaVersion = "workspace-cache/preview-1";

    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceImportSourceDiscoveryService _workspaceImportSourceDiscoveryService;
    private readonly WorkspaceImportCandidateScreeningCoordinator _workspaceImportCandidateScreeningCoordinator;
    private readonly IWorkspaceImportExecutionOptionsResolver _workspaceImportExecutionOptionsResolver;

    public WorkspaceImportProcessingPlanBuilder(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceImportSourceDiscoveryService workspaceImportSourceDiscoveryService,
        WorkspaceImportCandidateScreeningCoordinator workspaceImportCandidateScreeningCoordinator,
        IWorkspaceImportExecutionOptionsResolver workspaceImportExecutionOptionsResolver)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _workspaceImportSourceDiscoveryService = workspaceImportSourceDiscoveryService ?? throw new ArgumentNullException(nameof(workspaceImportSourceDiscoveryService));
        _workspaceImportCandidateScreeningCoordinator = workspaceImportCandidateScreeningCoordinator ?? throw new ArgumentNullException(nameof(workspaceImportCandidateScreeningCoordinator));
        _workspaceImportExecutionOptionsResolver = workspaceImportExecutionOptionsResolver ?? throw new ArgumentNullException(nameof(workspaceImportExecutionOptionsResolver));
    }

    public async Task<Result<WorkspaceImportProcessingPlan>> BuildAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken)
    {
        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var metadata = metadataStore.Load(command.WorkspaceKey);
        if (metadata is null)
        {
            return Result<WorkspaceImportProcessingPlan>.Failure(
                ApplicationErrors.WorkspaceImportMetadataNotFound(command.WorkspaceKey));
        }

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(command.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
        var importedSources = metadata.ImportedSources ?? Array.Empty<WorkspaceImportedSourceDto>();
        var discoveredFiles = _workspaceImportSourceDiscoveryService.Discover(
            importedSources,
            metadata.ExcludedBundleSourcePaths);
        var screeningStopwatch = Stopwatch.StartNew();
        var screeningResult = await _workspaceImportCandidateScreeningCoordinator.ScreenAsync(
            metadata.AssetBundleDecryptorId,
            discoveredFiles,
            cancellationToken);
        screeningStopwatch.Stop();
        if (!screeningResult.IsSuccess || screeningResult.Value is null)
        {
            return Result<WorkspaceImportProcessingPlan>.Failure(screeningResult.Errors);
        }

        return Result<WorkspaceImportProcessingPlan>.Success(
            new WorkspaceImportProcessingPlan(
                Command: command,
                MetadataStore: metadataStore,
                Metadata: metadata,
                BundleIndexCacheStore: bundleIndexCacheStore,
                PreviewCacheStore: previewCacheStore,
                ProcessedAtUtc: DateTimeOffset.UtcNow,
                ImportedSources: importedSources,
                DiscoveredFiles: discoveredFiles,
                DecryptorResolution: screeningResult.Value,
                ScreeningDurationMs: screeningStopwatch.ElapsedMilliseconds,
                CurrentToolVersion: ResolveToolVersion(),
                ExistingCacheEntriesByBundleCacheKey: bundleIndexCacheStore.LoadAll(command.WorkspaceKey)
                    .ToDictionary(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase),
                PreviewCacheKeysByBundleCacheKey: BuildPreviewCacheKeysByBundle(
                    previewCacheStore.LoadDescriptors(command.WorkspaceKey)),
                ExecutionOptions: _workspaceImportExecutionOptionsResolver.ResolveOptions()));
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildPreviewCacheKeysByBundle(
        IReadOnlyList<PreviewCacheDescriptorDto> previewDescriptors)
    {
        var previewCacheKeysByBundle = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var previewDescriptor in previewDescriptors)
        {
            if (string.IsNullOrWhiteSpace(previewDescriptor.BundleCacheKey) ||
                string.IsNullOrWhiteSpace(previewDescriptor.PreviewCacheKey))
            {
                continue;
            }

            if (!previewCacheKeysByBundle.TryGetValue(previewDescriptor.BundleCacheKey, out var previewCacheKeys))
            {
                previewCacheKeys = new List<string>();
                previewCacheKeysByBundle[previewDescriptor.BundleCacheKey] = previewCacheKeys;
            }

            previewCacheKeys.Add(previewDescriptor.PreviewCacheKey);
        }

        return previewCacheKeysByBundle.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveToolVersion()
    {
        var assemblyVersion = typeof(WorkspaceImportProcessingPlanBuilder).Assembly.GetName().Version?.ToString()
                              ?? "1.0.0";
        return $"{assemblyVersion}+{ToolSchemaVersion}";
    }
}
