using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
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
    private readonly MatchAssetReplacementSourcesHandler _matchAssetReplacementSourcesHandler;
    private readonly MatchSpineReplacementSourcesHandler _matchSpineReplacementSourcesHandler;
    private readonly CheckSpineSkeletonReplacementCompatibilityHandler _checkSpineSkeletonReplacementCompatibilityHandler;
    private readonly GetWorkspaceReplacementCandidatesHandler _getWorkspaceReplacementCandidatesHandler;
    private readonly ReplaceAssetFromFileHandler _replaceAssetFromFileHandler;
    private readonly ReplaceAssetFromWorkspaceHandler _replaceAssetFromWorkspaceHandler;
    private readonly UndoAssetReplacementHandler _undoAssetReplacementHandler;
    private readonly RedoAssetReplacementHandler _redoAssetReplacementHandler;
    private readonly ApplySpriteEditHandler _applySpriteEditHandler;
    private readonly CreateTextAssetEditDraftHandler _createTextAssetEditDraftHandler;
    private readonly OpenTextAssetEditDraftHandler _openTextAssetEditDraftHandler;
    private readonly UpdateTextAssetEditDraftHandler _updateTextAssetEditDraftHandler;
    private readonly ApplyTextAssetEditDraftHandler _applyTextAssetEditDraftHandler;
    private readonly DiscardTextAssetEditDraftHandler _discardTextAssetEditDraftHandler;
    private readonly DeleteWorkspaceOperationRecordHandler _deleteWorkspaceOperationRecordHandler;

    public WorkspaceAssetOperationGateway(
        ExtractAssetHandler extractAssetHandler,
        ExtractSpineAssetGroupHandler extractSpineAssetGroupHandler,
        GetFileReplacementCandidateHandler getFileReplacementCandidateHandler,
        MatchAssetReplacementSourcesHandler matchAssetReplacementSourcesHandler,
        MatchSpineReplacementSourcesHandler matchSpineReplacementSourcesHandler,
        CheckSpineSkeletonReplacementCompatibilityHandler checkSpineSkeletonReplacementCompatibilityHandler,
        GetWorkspaceReplacementCandidatesHandler getWorkspaceReplacementCandidatesHandler,
        ReplaceAssetFromFileHandler replaceAssetFromFileHandler,
        ReplaceAssetFromWorkspaceHandler replaceAssetFromWorkspaceHandler,
        UndoAssetReplacementHandler undoAssetReplacementHandler,
        RedoAssetReplacementHandler redoAssetReplacementHandler,
        ApplySpriteEditHandler applySpriteEditHandler,
        CreateTextAssetEditDraftHandler createTextAssetEditDraftHandler,
        OpenTextAssetEditDraftHandler openTextAssetEditDraftHandler,
        UpdateTextAssetEditDraftHandler updateTextAssetEditDraftHandler,
        ApplyTextAssetEditDraftHandler applyTextAssetEditDraftHandler,
        DiscardTextAssetEditDraftHandler discardTextAssetEditDraftHandler,
        DeleteWorkspaceOperationRecordHandler deleteWorkspaceOperationRecordHandler)
    {
        _extractAssetHandler = extractAssetHandler ?? throw new ArgumentNullException(nameof(extractAssetHandler));
        _extractSpineAssetGroupHandler = extractSpineAssetGroupHandler ?? throw new ArgumentNullException(nameof(extractSpineAssetGroupHandler));
        _getFileReplacementCandidateHandler = getFileReplacementCandidateHandler ?? throw new ArgumentNullException(nameof(getFileReplacementCandidateHandler));
        _matchAssetReplacementSourcesHandler = matchAssetReplacementSourcesHandler ?? throw new ArgumentNullException(nameof(matchAssetReplacementSourcesHandler));
        _matchSpineReplacementSourcesHandler = matchSpineReplacementSourcesHandler ?? throw new ArgumentNullException(nameof(matchSpineReplacementSourcesHandler));
        _checkSpineSkeletonReplacementCompatibilityHandler = checkSpineSkeletonReplacementCompatibilityHandler ?? throw new ArgumentNullException(nameof(checkSpineSkeletonReplacementCompatibilityHandler));
        _getWorkspaceReplacementCandidatesHandler = getWorkspaceReplacementCandidatesHandler ?? throw new ArgumentNullException(nameof(getWorkspaceReplacementCandidatesHandler));
        _replaceAssetFromFileHandler = replaceAssetFromFileHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromFileHandler));
        _replaceAssetFromWorkspaceHandler = replaceAssetFromWorkspaceHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromWorkspaceHandler));
        _undoAssetReplacementHandler = undoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(undoAssetReplacementHandler));
        _redoAssetReplacementHandler = redoAssetReplacementHandler ?? throw new ArgumentNullException(nameof(redoAssetReplacementHandler));
        _applySpriteEditHandler = applySpriteEditHandler ?? throw new ArgumentNullException(nameof(applySpriteEditHandler));
        _createTextAssetEditDraftHandler = createTextAssetEditDraftHandler ?? throw new ArgumentNullException(nameof(createTextAssetEditDraftHandler));
        _openTextAssetEditDraftHandler = openTextAssetEditDraftHandler ?? throw new ArgumentNullException(nameof(openTextAssetEditDraftHandler));
        _updateTextAssetEditDraftHandler = updateTextAssetEditDraftHandler ?? throw new ArgumentNullException(nameof(updateTextAssetEditDraftHandler));
        _applyTextAssetEditDraftHandler = applyTextAssetEditDraftHandler ?? throw new ArgumentNullException(nameof(applyTextAssetEditDraftHandler));
        _discardTextAssetEditDraftHandler = discardTextAssetEditDraftHandler ?? throw new ArgumentNullException(nameof(discardTextAssetEditDraftHandler));
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

    public Task<Result<AssetReplacementSourceMatchPlanDto>> MatchAssetReplacementSourcesAsync(
        MatchAssetReplacementSourcesQuery query,
        CancellationToken cancellationToken = default) =>
        _matchAssetReplacementSourcesHandler.HandleAsync(query, cancellationToken);

    public Task<Result<IReadOnlyList<SpineReplacementSourceMatchDto>>> MatchSpineReplacementSourcesAsync(
        MatchSpineReplacementSourcesQuery query,
        CancellationToken cancellationToken = default) =>
        _matchSpineReplacementSourcesHandler.HandleAsync(query, cancellationToken);

    public Task<Result<SpineSkeletonFormatCompatibilityDto>> CheckSpineSkeletonReplacementCompatibilityAsync(
        CheckSpineSkeletonReplacementCompatibilityQuery query,
        CancellationToken cancellationToken = default) =>
        _checkSpineSkeletonReplacementCompatibilityHandler.HandleAsync(query, cancellationToken);

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

    public Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
        ApplySpriteEditCommand command,
        CancellationToken cancellationToken = default) =>
        _applySpriteEditHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
        CreateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _createTextAssetEditDraftHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetTextEditDraftDto>> OpenTextAssetEditDraftAsync(
        OpenTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _openTextAssetEditDraftHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetTextEditDraftUpdatedResultDto>> UpdateTextAssetEditDraftAsync(
        UpdateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _updateTextAssetEditDraftHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetTextEditAppliedResultDto>> ApplyTextAssetEditDraftAsync(
        ApplyTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _applyTextAssetEditDraftHandler.HandleAsync(command, cancellationToken);

    public Task<Result<AssetTextEditDraftDiscardedResultDto>> DiscardTextAssetEditDraftAsync(
        DiscardTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _discardTextAssetEditDraftHandler.HandleAsync(command, cancellationToken);

    public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
        DeleteWorkspaceOperationRecordCommand command,
        CancellationToken cancellationToken = default) =>
        _deleteWorkspaceOperationRecordHandler.HandleAsync(command, cancellationToken);
}
