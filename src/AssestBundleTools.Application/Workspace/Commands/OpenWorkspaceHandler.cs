using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 处理打开工作区请求，并创建临时工作区会话。
/// </summary>
public sealed class OpenWorkspaceHandler : ICommandHandler<OpenWorkspaceCommand, Result<WorkspaceSummaryDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly WorkspaceImportProcessingCoordinator? _workspaceImportProcessingCoordinator;

    public OpenWorkspaceHandler(
        IWorkspaceSessionRegistry? workspaceSessionRegistry = null,
        IWorkspaceMetadataStoreFactory? workspaceMetadataStoreFactory = null,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory = null,
        IWorkspaceImportSourceDiscoveryService? workspaceImportSourceDiscoveryService = null,
        IAssetBundleSourceInspector? assetBundleSourceInspector = null,
        IAssetBundleDecryptorRegistry? assetBundleDecryptorRegistry = null,
        IAssetBundleReader? assetBundleReader = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory
                                         ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        if (workspaceDataStoreFactory is not null &&
            workspaceImportSourceDiscoveryService is not null &&
            assetBundleSourceInspector is not null &&
            assetBundleDecryptorRegistry is not null &&
            assetBundleReader is not null)
        {
            _workspaceImportProcessingCoordinator = new WorkspaceImportProcessingCoordinator(
                _workspaceMetadataStoreFactory,
                workspaceDataStoreFactory,
                workspaceImportSourceDiscoveryService,
                assetBundleSourceInspector,
                assetBundleDecryptorRegistry,
                assetBundleReader);
        }
    }

    public async Task<Result<WorkspaceSummaryDto>> HandleAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceSummaryDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspacePath) &&
            string.IsNullOrWhiteSpace(command.WorkspaceDisplayName))
        {
            return Result<WorkspaceSummaryDto>.Failure(ApplicationErrors.WorkspacePathRequired());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Result<WorkspaceSummaryDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            command.WorkspacePath,
            command.WorkspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Result<WorkspaceSummaryDto>.Failure(ApplicationErrors.WorkspacePathRequired());
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var metadata = metadataStore.Load(workspaceKey);
        if (metadata is not null && _workspaceImportProcessingCoordinator is not null)
        {
            var processingResult = await _workspaceImportProcessingCoordinator.ProcessAsync(
                new ProcessWorkspaceImportsCommand(
                    WorkspaceKey: workspaceKey,
                    WorkspaceDataRootPath: command.WorkspaceDataRootPath),
                cancellationToken);

            if (processingResult.IsCancelled)
            {
                return Result<WorkspaceSummaryDto>.Cancelled();
            }

            if (!processingResult.IsSuccess)
            {
                return Result<WorkspaceSummaryDto>.Failure(processingResult.Errors);
            }
        }

        var workspaceSummary = _workspaceSessionRegistry.OpenWorkspace(
            new WorkspaceSessionOpenRequestDto(
                WorkspaceKey: workspaceKey,
                WorkspaceDataRootPath: command.WorkspaceDataRootPath,
                WorkspacePath: string.IsNullOrWhiteSpace(command.WorkspacePath)
                    ? metadata?.SourcePath ?? string.Empty
                    : command.WorkspacePath.Trim(),
                WorkspaceDisplayName: string.IsNullOrWhiteSpace(command.WorkspaceDisplayName)
                    ? metadata?.DisplayName ?? string.Empty
                    : command.WorkspaceDisplayName.Trim(),
                InitialMode: command.InitialMode));
        return Result<WorkspaceSummaryDto>.Success(workspaceSummary);
    }
}
