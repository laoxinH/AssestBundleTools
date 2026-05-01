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
    private readonly WorkspaceImportProcessingCoordinator _workspaceImportProcessingCoordinator;

    public OpenWorkspaceHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceImportSourceDiscoveryService workspaceImportSourceDiscoveryService,
        IWorkspaceImportFileSystemService workspaceImportFileSystemService,
        IWorkspaceImportExecutionGate workspaceImportExecutionGate,
        IWorkspaceImportExecutionOptionsResolver workspaceImportExecutionOptionsResolver,
        IAssetBundleSourceInspector assetBundleSourceInspector,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IAssetBundleReader assetBundleReader)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory
                                         ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceImportProcessingCoordinator = new WorkspaceImportProcessingCoordinator(
            _workspaceMetadataStoreFactory,
            workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory)),
            workspaceImportSourceDiscoveryService ?? throw new ArgumentNullException(nameof(workspaceImportSourceDiscoveryService)),
            workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService)),
            workspaceImportExecutionGate ?? throw new ArgumentNullException(nameof(workspaceImportExecutionGate)),
            workspaceImportExecutionOptionsResolver ?? throw new ArgumentNullException(nameof(workspaceImportExecutionOptionsResolver)),
            assetBundleSourceInspector ?? throw new ArgumentNullException(nameof(assetBundleSourceInspector)),
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)),
            assetBundleReader ?? throw new ArgumentNullException(nameof(assetBundleReader)));
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
        if (metadata is not null)
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
