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
/// 聚合资产提取、替换与撤销替换相关能力。
/// </summary>
public interface IWorkspaceAssetOperationGateway
{
    Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
        RedoAssetReplacementCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
        ApplySpriteEditCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
        CreateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetTextEditAppliedResultDto>> ApplyTextAssetEditDraftAsync(
        ApplyTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<AssetTextEditDraftDiscardedResultDto>> DiscardTextAssetEditDraftAsync(
        DiscardTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
        DeleteWorkspaceOperationRecordCommand command,
        CancellationToken cancellationToken = default);
}
