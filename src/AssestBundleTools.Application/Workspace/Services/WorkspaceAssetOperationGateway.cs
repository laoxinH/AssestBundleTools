using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合资产提取、替换与撤销替换相关 Handler。
/// </summary>
public sealed class WorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
{
    private readonly ExtractAssetHandler _extractAssetHandler;
    private readonly GetFileReplacementCandidateHandler _getFileReplacementCandidateHandler;
    private readonly GetWorkspaceReplacementCandidatesHandler _getWorkspaceReplacementCandidatesHandler;
    private readonly ReplaceAssetFromFileHandler _replaceAssetFromFileHandler;
    private readonly ReplaceAssetFromWorkspaceHandler _replaceAssetFromWorkspaceHandler;
    private readonly UndoAssetReplacementHandler _undoAssetReplacementHandler;

    public WorkspaceAssetOperationGateway(
        ExtractAssetHandler extractAssetHandler,
        GetFileReplacementCandidateHandler getFileReplacementCandidateHandler,
        GetWorkspaceReplacementCandidatesHandler getWorkspaceReplacementCandidatesHandler,
        ReplaceAssetFromFileHandler replaceAssetFromFileHandler,
        ReplaceAssetFromWorkspaceHandler replaceAssetFromWorkspaceHandler,
        UndoAssetReplacementHandler undoAssetReplacementHandler)
    {
        _extractAssetHandler = extractAssetHandler ?? throw new ArgumentNullException(nameof(extractAssetHandler));
        _getFileReplacementCandidateHandler = getFileReplacementCandidateHandler ?? throw new ArgumentNullException(nameof(getFileReplacementCandidateHandler));
        _getWorkspaceReplacementCandidatesHandler = getWorkspaceReplacementCandidatesHandler ?? throw new ArgumentNullException(nameof(getWorkspaceReplacementCandidatesHandler));
        _replaceAssetFromFileHandler = replaceAssetFromFileHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromFileHandler));
        _replaceAssetFromWorkspaceHandler = replaceAssetFromWorkspaceHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromWorkspaceHandler));
        _undoAssetReplacementHandler = undoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(undoAssetReplacementHandler));
    }

    public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default) =>
        _extractAssetHandler.HandleAsync(command, cancellationToken);

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
}
