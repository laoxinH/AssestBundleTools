using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 将工作区已登记的导入来源真正处理为资源包索引缓存。
/// </summary>
public sealed class ProcessWorkspaceImportsHandler
    : ICommandHandler<ProcessWorkspaceImportsCommand, Result<WorkspaceImportProcessingResultDto>>
{
    private readonly WorkspaceImportProcessingCoordinator _coordinator;

    public ProcessWorkspaceImportsHandler(
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
        _coordinator = new WorkspaceImportProcessingCoordinator(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory,
            workspaceImportSourceDiscoveryService,
            workspaceImportFileSystemService,
            workspaceImportExecutionGate,
            workspaceImportExecutionOptionsResolver,
            assetBundleSourceInspector,
            assetBundleDecryptorRegistry,
            assetBundleReader);
    }

    public Task<Result<WorkspaceImportProcessingResultDto>> HandleAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default) =>
        _coordinator.ProcessAsync(command, cancellationToken);
}
