using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Saving.Commands;

/// <summary>
/// 执行“另存为新包”。
/// </summary>
/// <remarks>
/// 当组合根尚未提供真实写出端口时仍返回计划摘要；
/// 提供写出端口后，按保存计划并发写出 AB 文件并持久化导出清单。
/// </remarks>
public sealed class SaveWorkspaceAsNewBundleHandler : ICommandHandler<SaveWorkspaceAsNewBundleCommand, Result<WorkspaceSaveExecutionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceSaveArtifactInputResolver? _artifactInputResolver;
    private readonly IWorkspaceSaveOutputProbe? _outputProbe;
    private readonly IWorkspaceBundleExporter? _bundleExporter;
    private readonly IWorkspaceBundleExportManifestStore? _manifestStore;
    private readonly WorkspaceSaveResourceProcessorRunner _resourceProcessorRunner;
    private readonly IWorkspaceDataStoreFactory? _workspaceDataStoreFactory;
    private readonly IPostExportPluginCatalogStore? _postExportPluginCatalogStore;
    private readonly IWorkspacePostExportPluginLauncher? _postExportPluginLauncher;

    public SaveWorkspaceAsNewBundleHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory = null,
        IWorkspaceSaveOutputProbe? outputProbe = null,
        IWorkspaceBundleExporter? bundleExporter = null,
        IWorkspaceBundleExportManifestStore? manifestStore = null,
        IResourceProcessorCatalogStore? resourceProcessorCatalogStore = null,
        IWorkspaceResourceProcessorExecutor? resourceProcessorExecutor = null,
        IPostExportPluginCatalogStore? postExportPluginCatalogStore = null,
        IWorkspacePostExportPluginLauncher? postExportPluginLauncher = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory;
        _artifactInputResolver = workspaceDataStoreFactory is null
            ? null
            : new WorkspaceSaveArtifactInputResolver(workspaceDataStoreFactory);
        _outputProbe = outputProbe;
        _bundleExporter = bundleExporter;
        _manifestStore = manifestStore;
        _resourceProcessorRunner = new WorkspaceSaveResourceProcessorRunner(
            resourceProcessorCatalogStore,
            resourceProcessorExecutor);
        _postExportPluginCatalogStore = postExportPluginCatalogStore;
        _postExportPluginLauncher = postExportPluginLauncher;
    }

    public async Task<Result<WorkspaceSaveExecutionResultDto>> HandleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        var plan = WorkspaceSavePlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.OutputDirectory,
            _artifactInputResolver,
            _outputProbe,
            command.WorkspaceKey,
            command.WorkspaceDataRootPath,
            command.OutputConflictPolicy);
        if (plan.BundlePlans.Count == 0)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.NoSaveCandidates(command.WorkspaceId));
        }

        if (!TrySelectBundlePlans(plan, command.SelectedBundleIds, out var selectedPlans, out var selectionErrors))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(selectionErrors);
        }

        if (_bundleExporter is null)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Success(
                CreateSummaryResult(plan, selectedPlans, DateTimeOffset.UtcNow));
        }

        var validationErrors = ValidateSelectedPlansForWrite(selectedPlans);
        if (validationErrors.Count > 0)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(validationErrors);
        }

        command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
            WorkspaceSaveExecutionStageDto.Planning,
            CompletedBundleCount: 0,
            TotalBundleCount: selectedPlans.Count));

        var operationId = $"save-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var processorSelections = NormalizeProcessorSelections(command.ProcessorSelections);
        if (!TryResolvePostExportPlugin(command.PostExportPluginId, out var postExportPlugin, out var postExportPluginError))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(postExportPluginError!);
        }

        var resourceProcessorPlanResult = _resourceProcessorRunner.BuildPlan(processorSelections);
        if (resourceProcessorPlanResult.IsFailure)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(resourceProcessorPlanResult.Errors);
        }

        var resourceProcessorPlan = resourceProcessorPlanResult.Value ?? WorkspaceResourceProcessorPlan.Empty;
        IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> bundleResults;
        try
        {
            var processorResult = await _resourceProcessorRunner.ExecuteAsync(
                plan,
                selectedPlans,
                resourceProcessorPlan,
                new WorkspaceResourceProcessorExecutionContext(
                    OperationId: operationId,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDataRootPath: command.WorkspaceDataRootPath,
                    Progress: command.Progress),
                cancellationToken);
            if (processorResult.IsCancelled)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
            }

            if (processorResult.IsFailure)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Failure(processorResult.Errors);
            }

            var bundleExportRunner = new WorkspaceSaveBundleExportRunner(
                _workspaceSessionRegistry,
                _workspaceDataStoreFactory,
                _bundleExporter);
            bundleResults = await bundleExportRunner.ExportAsync(
                plan,
                selectedPlans,
                processorSelections,
                processorResult.Value ?? WorkspaceResourceProcessorBatchResult.Empty,
                operationId,
                command,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
        }
        var successfulResults = bundleResults
            .Where(static result => result.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded)
            .ToArray();

        string? manifestPath = null;
        if (successfulResults.Length > 0 && _manifestStore is not null)
        {
            command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                WorkspaceSaveExecutionStageDto.ManifestWriting,
                CompletedBundleCount: bundleResults.Count,
                TotalBundleCount: selectedPlans.Count,
                SucceededBundleCount: successfulResults.Length,
                FailedBundleCount: bundleResults.Count - successfulResults.Length));

            var manifestResult = await _manifestStore.SaveAsync(
                new WorkspaceBundleExportManifestSaveRequestDto(
                    WorkspaceId: plan.WorkspaceId,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDisplayName: plan.WorkspaceDisplayName,
                    OutputDirectory: plan.OutputDirectory,
                    ExportedAtUtc: DateTimeOffset.UtcNow,
                    BundleResults: bundleResults,
                    ProcessorSelections: processorSelections),
                cancellationToken);
            if (manifestResult.IsCancelled)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
            }

            if (manifestResult.IsFailure)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Failure(manifestResult.Errors);
            }

            manifestPath = manifestResult.Value?.ManifestPath;
        }

        WorkspacePostExportPluginLaunchResultDto? postExportPluginLaunchResult = null;
        if (postExportPlugin is not null && !string.IsNullOrWhiteSpace(manifestPath))
        {
            command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                WorkspaceSaveExecutionStageDto.PostExportPluginLaunching,
                CompletedBundleCount: bundleResults.Count,
                TotalBundleCount: selectedPlans.Count,
                SucceededBundleCount: successfulResults.Length,
                FailedBundleCount: bundleResults.Count - successfulResults.Length));

            var launchResult = await _postExportPluginLauncher!.LaunchAsync(
                new WorkspacePostExportPluginLaunchRequestDto(
                    postExportPlugin,
                    manifestPath,
                    plan.OutputDirectory,
                    plan.WorkspaceId,
                    command.WorkspaceKey),
                cancellationToken);
            if (launchResult.IsCancelled)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
            }

            postExportPluginLaunchResult = launchResult.IsSuccess && launchResult.Value is not null
                ? launchResult.Value
                : CreatePostExportPluginLaunchFailure(postExportPlugin, launchResult.Errors.FirstOrDefault());
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
            WorkspaceSaveExecutionStageDto.Completed,
            CompletedBundleCount: bundleResults.Count,
            TotalBundleCount: selectedPlans.Count,
            SucceededBundleCount: successfulResults.Length,
            FailedBundleCount: bundleResults.Count - successfulResults.Length));

        return Result<WorkspaceSaveExecutionResultDto>.Success(
            CreateExecutionResult(
                plan,
                selectedPlans,
                bundleResults,
                manifestPath,
                postExportPluginLaunchResult,
                completedAtUtc));
    }

    private static WorkspaceSaveExecutionResultDto CreateSummaryResult(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        DateTimeOffset completedAtUtc)
    {
        var selectedPlan = CreatePlanSnapshot(plan, selectedPlans);
        return new WorkspaceSaveExecutionResultDto(
            WorkspaceId: plan.WorkspaceId,
            WorkspaceDisplayName: plan.WorkspaceDisplayName,
            OutputDirectory: plan.OutputDirectory,
            PlannedBundleCount: selectedPlans.Count,
            TotalChangeCount: selectedPlans.Sum(static bundle => bundle.ChangeCount),
            CompletedAtUtc: completedAtUtc,
            ResolvedArtifactInputCount: selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount: selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact),
            OutputReadyBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked),
            OutputUnprobedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed),
            IsReadyForWrite: selectedPlan.IsReadyForWrite,
            BundleResults: Array.Empty<WorkspaceSaveBundleExecutionResultDto>());
    }

    private static WorkspaceSaveExecutionResultDto CreateExecutionResult(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> bundleResults,
        string? manifestPath,
        WorkspacePostExportPluginLaunchResultDto? postExportPluginLaunchResult,
        DateTimeOffset completedAtUtc)
    {
        var succeededCount = bundleResults.Count(static result => result.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded);
        var failedCount = bundleResults.Count - succeededCount;
        var selectedPlan = CreatePlanSnapshot(plan, selectedPlans);
        var status = succeededCount == selectedPlans.Count
            ? WorkspaceSaveExecutionStatusDto.Succeeded
            : succeededCount == 0
                ? WorkspaceSaveExecutionStatusDto.Failed
                : WorkspaceSaveExecutionStatusDto.PartiallySucceeded;

        return new WorkspaceSaveExecutionResultDto(
            WorkspaceId: plan.WorkspaceId,
            WorkspaceDisplayName: plan.WorkspaceDisplayName,
            OutputDirectory: plan.OutputDirectory,
            PlannedBundleCount: selectedPlans.Count,
            TotalChangeCount: selectedPlans.Sum(static bundle => bundle.ChangeCount),
            CompletedAtUtc: completedAtUtc,
            ResolvedArtifactInputCount: selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount: selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact),
            OutputReadyBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked),
            OutputUnprobedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed),
            IsReadyForWrite: selectedPlan.IsReadyForWrite,
            Status: status,
            SucceededBundleCount: succeededCount,
            FailedBundleCount: failedCount,
            WrittenBundleCount: succeededCount,
            ExportManifestPath: manifestPath,
            BundleResults: bundleResults,
            PostExportPluginLaunchResult: postExportPluginLaunchResult);
    }

    private bool TryResolvePostExportPlugin(
        string? pluginId,
        out PostExportPluginCatalogEntryDto? plugin,
        out AssestBundleTools.Domain.Common.Errors.AppError? error)
    {
        plugin = null;
        error = null;
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return true;
        }

        var normalizedId = pluginId.Trim();
        if (_postExportPluginCatalogStore is null || _postExportPluginLauncher is null)
        {
            error = ApplicationErrors.PostExportPluginCatalogNotFound(normalizedId);
            return false;
        }

        plugin = _postExportPluginCatalogStore.Load().FirstOrDefault(entry =>
            string.Equals(entry.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null || plugin.Status != ResourceProcessorStatus.Available)
        {
            error = ApplicationErrors.PostExportPluginCatalogNotFound(normalizedId);
            plugin = null;
            return false;
        }

        return true;
    }

    private static WorkspacePostExportPluginLaunchResultDto CreatePostExportPluginLaunchFailure(
        PostExportPluginCatalogEntryDto plugin,
        AssestBundleTools.Domain.Common.Errors.AppError? error) =>
        new(
            plugin.Id,
            plugin.DisplayName,
            WorkspacePostExportPluginLaunchStatusDto.LaunchFailed,
            error?.Code,
            error is null
                ? null
                : string.Join(
                    "; ",
                    error.Parameters
                        .Where(static parameter => parameter.Value is not null)
                        .Select(static parameter => $"{parameter.Key}={parameter.Value}")));

    private static WorkspaceSavePlan CreatePlanSnapshot(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans)
    {
        var unresolvedArtifactInputCount = selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact);
        var outputBlockedBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked);
        var outputUnprobedBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed);
        return plan with
        {
            ResolvedArtifactInputCount = selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount = unresolvedArtifactInputCount,
            OutputReadyBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount = outputBlockedBundleCount,
            OutputUnprobedBundleCount = outputUnprobedBundleCount,
            IsReadyForWrite = unresolvedArtifactInputCount == 0 &&
                              outputBlockedBundleCount == 0 &&
                              outputUnprobedBundleCount == 0
        };
    }

    private static bool TrySelectBundlePlans(
        WorkspaceSavePlan plan,
        IReadOnlyList<Domain.Bundles.BundleId>? selectedBundleIds,
        out IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        out IReadOnlyList<Domain.Common.Errors.AppError> errors)
    {
        if (selectedBundleIds is null)
        {
            selectedPlans = plan.BundlePlans;
            errors = Array.Empty<Domain.Common.Errors.AppError>();
            return true;
        }

        var selectedIdValues = selectedBundleIds
            .Where(static bundleId => !string.IsNullOrWhiteSpace(bundleId.Value))
            .Select(static bundleId => bundleId.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedIdValues.Length == 0)
        {
            selectedPlans = Array.Empty<PlannedWorkspaceBundleSave>();
            errors = [ApplicationErrors.NoSaveCandidates(plan.WorkspaceId)];
            return false;
        }

        var selectedIdSet = selectedIdValues.ToHashSet(StringComparer.Ordinal);
        selectedPlans = plan.BundlePlans
            .Where(plan => selectedIdSet.Contains(plan.BundleId.Value))
            .ToArray();
        var foundIdSet = selectedPlans
            .Select(static selectedPlan => selectedPlan.BundleId.Value)
            .ToHashSet(StringComparer.Ordinal);
        errors = selectedIdValues
            .Where(selectedId => !foundIdSet.Contains(selectedId))
            .Select(selectedId => ApplicationErrors.BundleNotFound(new Domain.Bundles.BundleId(selectedId)))
            .ToArray();
        return errors.Count == 0;
    }

    private static IReadOnlyList<Domain.Common.Errors.AppError> ValidateSelectedPlansForWrite(
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans)
    {
        var errors = new List<Domain.Common.Errors.AppError>();
        foreach (var selectedPlan in selectedPlans)
        {
            if (!selectedPlan.ArtifactInput.HasStandardizedArtifact ||
                string.IsNullOrWhiteSpace(selectedPlan.ArtifactInput.ArtifactBundlePath))
            {
                errors.Add(ApplicationErrors.BundleExportArtifactMissing(
                    selectedPlan.BundleDisplayName,
                    selectedPlan.ArtifactInput.ArtifactDiagnosticCode));
            }

            if (selectedPlan.OutputPreflight.IsWriteBlocked)
            {
                errors.Add(ApplicationErrors.BundleExportOutputBlocked(
                    selectedPlan.OutputFilePath,
                    selectedPlan.OutputPreflight.DiagnosticCode));
            }
        }

        return errors;
    }

    private static IReadOnlyList<WorkspaceExportProcessorSelectionDto> NormalizeProcessorSelections(
        IReadOnlyList<WorkspaceExportProcessorSelectionDto>? processorSelections)
    {
        if (processorSelections is null || processorSelections.Count == 0)
        {
            return Array.Empty<WorkspaceExportProcessorSelectionDto>();
        }

        return processorSelections
            .Where(static selection => !string.IsNullOrWhiteSpace(selection.ResourceKind))
            .GroupBy(
                static selection => selection.ResourceKind.Trim().ToLowerInvariant(),
                StringComparer.Ordinal)
            .Select(static group =>
            {
                var selection = group.Last();
                return new WorkspaceExportProcessorSelectionDto(
                    group.Key,
                    string.IsNullOrWhiteSpace(selection.ProcessorId) ? null : selection.ProcessorId.Trim());
            })
            .ToArray();
    }

}
