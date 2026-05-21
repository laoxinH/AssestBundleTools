using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合资产提取、替换与撤销替换相关 Handler。
/// </summary>
public sealed class WorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
{
    private readonly ExtractAssetHandler _extractAssetHandler;
    private readonly ExtractSpineAssetGroupHandler _extractSpineAssetGroupHandler;
    private readonly GetFileReplacementCandidateHandler _getFileReplacementCandidateHandler;
    private readonly GetWorkspaceReplacementCandidatesHandler _getWorkspaceReplacementCandidatesHandler;
    private readonly ReplaceAssetFromFileHandler _replaceAssetFromFileHandler;
    private readonly ReplaceAssetFromWorkspaceHandler _replaceAssetFromWorkspaceHandler;
    private readonly UndoAssetReplacementHandler _undoAssetReplacementHandler;
    private readonly RedoAssetReplacementHandler _redoAssetReplacementHandler;
    private readonly DeleteWorkspaceOperationRecordHandler _deleteWorkspaceOperationRecordHandler;

    public WorkspaceAssetOperationGateway(
        ExtractAssetHandler extractAssetHandler,
        ExtractSpineAssetGroupHandler extractSpineAssetGroupHandler,
        GetFileReplacementCandidateHandler getFileReplacementCandidateHandler,
        GetWorkspaceReplacementCandidatesHandler getWorkspaceReplacementCandidatesHandler,
        ReplaceAssetFromFileHandler replaceAssetFromFileHandler,
        ReplaceAssetFromWorkspaceHandler replaceAssetFromWorkspaceHandler,
        UndoAssetReplacementHandler undoAssetReplacementHandler,
        RedoAssetReplacementHandler redoAssetReplacementHandler,
        DeleteWorkspaceOperationRecordHandler deleteWorkspaceOperationRecordHandler)
    {
        _extractAssetHandler = extractAssetHandler ?? throw new ArgumentNullException(nameof(extractAssetHandler));
        _extractSpineAssetGroupHandler = extractSpineAssetGroupHandler ?? throw new ArgumentNullException(nameof(extractSpineAssetGroupHandler));
        _getFileReplacementCandidateHandler = getFileReplacementCandidateHandler ?? throw new ArgumentNullException(nameof(getFileReplacementCandidateHandler));
        _getWorkspaceReplacementCandidatesHandler = getWorkspaceReplacementCandidatesHandler ?? throw new ArgumentNullException(nameof(getWorkspaceReplacementCandidatesHandler));
        _replaceAssetFromFileHandler = replaceAssetFromFileHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromFileHandler));
        _replaceAssetFromWorkspaceHandler = replaceAssetFromWorkspaceHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromWorkspaceHandler));
        _undoAssetReplacementHandler = undoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(undoAssetReplacementHandler));
        _redoAssetReplacementHandler = redoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(redoAssetReplacementHandler));
        _deleteWorkspaceOperationRecordHandler = deleteWorkspaceOperationRecordHandler ?? throw new ArgumentNullException(nameof(deleteWorkspaceOperationRecordHandler));
    }

    public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default) =>
        _extractAssetHandler.HandleAsync(command, cancellationToken);

    public Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default) =>
        _extractSpineAssetGroupHandler.HandleAsync(command, cancellationToken);

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

    public Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
        RedoAssetReplacementCommand command,
        CancellationToken cancellationToken = default) =>
        _redoAssetReplacementHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
        DeleteWorkspaceOperationRecordCommand command,
        CancellationToken cancellationToken = default) =>
        _deleteWorkspaceOperationRecordHandler.HandleAsync(command, cancellationToken);
}
