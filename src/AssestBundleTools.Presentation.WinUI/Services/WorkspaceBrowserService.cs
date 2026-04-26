using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将工作区相关应用层用例聚合为 Presentation 可直接调用的服务。
/// </summary>
/// <remarks>
/// 当前类本身只承担 Presentation 门面职责，
/// 具体 Handler 统一由组合根注入，避免在 UI 服务内部继续拼装应用层依赖。
/// </remarks>
public sealed class WorkspaceBrowserService : IWorkspaceBrowserService
{
    private readonly OpenWorkspaceHandler _openWorkspaceHandler;
    private readonly RegisterWorkspaceImportHandler _registerWorkspaceImportHandler;
    private readonly ProcessWorkspaceImportsHandler _processWorkspaceImportsHandler;
    private readonly RemoveWorkspaceBundleHandler _removeWorkspaceBundleHandler;
    private readonly LoadBundleTreeHandler _loadBundleTreeHandler;
    private readonly LoadAssetListHandler _loadAssetListHandler;
    private readonly GetAssetDetailsHandler _getAssetDetailsHandler;
    private readonly GetAssetPreviewHandler _getAssetPreviewHandler;
    private readonly GetWorkspaceChangeSummaryHandler _getWorkspaceChangeSummaryHandler;
    private readonly GetWorkspaceSavePreviewHandler _getWorkspaceSavePreviewHandler;
    private readonly SaveWorkspaceAsNewBundleHandler _saveWorkspaceAsNewBundleHandler;
    private readonly ExtractAssetHandler _extractAssetHandler;
    private readonly GetWorkspaceBatchExtractionPreviewHandler _getWorkspaceBatchExtractionPreviewHandler;
    private readonly ExtractWorkspaceAssetsHandler _extractWorkspaceAssetsHandler;
    private readonly GetWorkspaceBatchReplacementPreviewHandler _getWorkspaceBatchReplacementPreviewHandler;
    private readonly ExecuteWorkspaceBatchReplacementHandler _executeWorkspaceBatchReplacementHandler;
    private readonly GetFileReplacementCandidateHandler _getFileReplacementCandidateHandler;
    private readonly GetWorkspaceReplacementCandidatesHandler _getWorkspaceReplacementCandidatesHandler;
    private readonly ReplaceAssetFromFileHandler _replaceAssetFromFileHandler;
    private readonly ReplaceAssetFromWorkspaceHandler _replaceAssetFromWorkspaceHandler;
    private readonly UndoAssetReplacementHandler _undoAssetReplacementHandler;
    private readonly OpenAssetInPaneHandler _openAssetInPaneHandler;

    public WorkspaceBrowserService(
        OpenWorkspaceHandler openWorkspaceHandler,
        RegisterWorkspaceImportHandler registerWorkspaceImportHandler,
        ProcessWorkspaceImportsHandler processWorkspaceImportsHandler,
        RemoveWorkspaceBundleHandler removeWorkspaceBundleHandler,
        LoadBundleTreeHandler loadBundleTreeHandler,
        LoadAssetListHandler loadAssetListHandler,
        GetAssetDetailsHandler getAssetDetailsHandler,
        GetAssetPreviewHandler getAssetPreviewHandler,
        GetWorkspaceChangeSummaryHandler getWorkspaceChangeSummaryHandler,
        GetWorkspaceSavePreviewHandler getWorkspaceSavePreviewHandler,
        SaveWorkspaceAsNewBundleHandler saveWorkspaceAsNewBundleHandler,
        ExtractAssetHandler extractAssetHandler,
        GetWorkspaceBatchExtractionPreviewHandler getWorkspaceBatchExtractionPreviewHandler,
        ExtractWorkspaceAssetsHandler extractWorkspaceAssetsHandler,
        GetWorkspaceBatchReplacementPreviewHandler getWorkspaceBatchReplacementPreviewHandler,
        ExecuteWorkspaceBatchReplacementHandler executeWorkspaceBatchReplacementHandler,
        GetFileReplacementCandidateHandler getFileReplacementCandidateHandler,
        GetWorkspaceReplacementCandidatesHandler getWorkspaceReplacementCandidatesHandler,
        ReplaceAssetFromFileHandler replaceAssetFromFileHandler,
        ReplaceAssetFromWorkspaceHandler replaceAssetFromWorkspaceHandler,
        UndoAssetReplacementHandler undoAssetReplacementHandler,
        OpenAssetInPaneHandler openAssetInPaneHandler)
    {
        _openWorkspaceHandler = openWorkspaceHandler ?? throw new ArgumentNullException(nameof(openWorkspaceHandler));
        _registerWorkspaceImportHandler = registerWorkspaceImportHandler ?? throw new ArgumentNullException(nameof(registerWorkspaceImportHandler));
        _processWorkspaceImportsHandler = processWorkspaceImportsHandler ?? throw new ArgumentNullException(nameof(processWorkspaceImportsHandler));
        _removeWorkspaceBundleHandler = removeWorkspaceBundleHandler ?? throw new ArgumentNullException(nameof(removeWorkspaceBundleHandler));
        _loadBundleTreeHandler = loadBundleTreeHandler ?? throw new ArgumentNullException(nameof(loadBundleTreeHandler));
        _loadAssetListHandler = loadAssetListHandler ?? throw new ArgumentNullException(nameof(loadAssetListHandler));
        _getAssetDetailsHandler = getAssetDetailsHandler ?? throw new ArgumentNullException(nameof(getAssetDetailsHandler));
        _getAssetPreviewHandler = getAssetPreviewHandler ?? throw new ArgumentNullException(nameof(getAssetPreviewHandler));
        _getWorkspaceChangeSummaryHandler = getWorkspaceChangeSummaryHandler ?? throw new ArgumentNullException(nameof(getWorkspaceChangeSummaryHandler));
        _getWorkspaceSavePreviewHandler = getWorkspaceSavePreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceSavePreviewHandler));
        _saveWorkspaceAsNewBundleHandler = saveWorkspaceAsNewBundleHandler ?? throw new ArgumentNullException(nameof(saveWorkspaceAsNewBundleHandler));
        _extractAssetHandler = extractAssetHandler ?? throw new ArgumentNullException(nameof(extractAssetHandler));
        _getWorkspaceBatchExtractionPreviewHandler = getWorkspaceBatchExtractionPreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceBatchExtractionPreviewHandler));
        _extractWorkspaceAssetsHandler = extractWorkspaceAssetsHandler ?? throw new ArgumentNullException(nameof(extractWorkspaceAssetsHandler));
        _getWorkspaceBatchReplacementPreviewHandler = getWorkspaceBatchReplacementPreviewHandler ?? throw new ArgumentNullException(nameof(getWorkspaceBatchReplacementPreviewHandler));
        _executeWorkspaceBatchReplacementHandler = executeWorkspaceBatchReplacementHandler ?? throw new ArgumentNullException(nameof(executeWorkspaceBatchReplacementHandler));
        _getFileReplacementCandidateHandler = getFileReplacementCandidateHandler ?? throw new ArgumentNullException(nameof(getFileReplacementCandidateHandler));
        _getWorkspaceReplacementCandidatesHandler = getWorkspaceReplacementCandidatesHandler ?? throw new ArgumentNullException(nameof(getWorkspaceReplacementCandidatesHandler));
        _replaceAssetFromFileHandler = replaceAssetFromFileHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromFileHandler));
        _replaceAssetFromWorkspaceHandler = replaceAssetFromWorkspaceHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromWorkspaceHandler));
        _undoAssetReplacementHandler = undoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(undoAssetReplacementHandler));
        _openAssetInPaneHandler = openAssetInPaneHandler ?? throw new ArgumentNullException(nameof(openAssetInPaneHandler));
    }

    public Task<Result<WorkspaceSummaryDto>> OpenWorkspaceAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default) =>
        _openWorkspaceHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceImportRegistrationResultDto>> RegisterWorkspaceImportAsync(
        RegisterWorkspaceImportCommand command,
        CancellationToken cancellationToken = default) =>
        _registerWorkspaceImportHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceImportProcessingResultDto>> ProcessWorkspaceImportsAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default) =>
        _processWorkspaceImportsHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBundleRemovalResultDto>> RemoveWorkspaceBundleAsync(
        RemoveWorkspaceBundleCommand command,
        CancellationToken cancellationToken = default) =>
        _removeWorkspaceBundleHandler.HandleAsync(command, cancellationToken);

    public Task<Result<IReadOnlyList<BundleTreeNodeDto>>> LoadBundleTreeAsync(
        LoadBundleTreeQuery query,
        CancellationToken cancellationToken = default) =>
        _loadBundleTreeHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetListResultDto>> LoadAssetListAsync(
        LoadAssetListQuery query,
        CancellationToken cancellationToken = default) =>
        _loadAssetListHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default) =>
        _getAssetDetailsHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetPreviewDto>> GetAssetPreviewAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getAssetPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceChangeSummaryHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceSavePreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default) =>
        _saveWorkspaceAsNewBundleHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceBatchExtractionPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default) =>
        _extractAssetHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default) =>
        _extractWorkspaceAssetsHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchReplacementPreviewDto>> GetWorkspaceBatchReplacementPreviewAsync(
        GetWorkspaceBatchReplacementPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceBatchReplacementPreviewHandler.HandleAsync(query, cancellationToken);

    public Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default) =>
        _executeWorkspaceBatchReplacementHandler.HandleAsync(command, cancellationToken);

    public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default) =>
        _getFileReplacementCandidateHandler.HandleAsync(query, cancellationToken);

    public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default) =>
        _getWorkspaceReplacementCandidatesHandler.HandleAsync(query, cancellationToken);

    public Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default) =>
        _replaceAssetFromFileHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default) =>
        _replaceAssetFromWorkspaceHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default) =>
        _undoAssetReplacementHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetPaneContextDto>> OpenAssetInPaneAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default) =>
        _openAssetInPaneHandler.HandleAsync(command, cancellationToken);
}
