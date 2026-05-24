using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示单个资产详情页可展示的一条已确认工作区操作记录。
/// </summary>
public sealed record AssetChangeRecordDto(
    AssetChangeRecordKind Kind,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AppliedAtUtc = null,
    string? SourceBundleDisplayName = null,
    string? SourceAssetName = null,
    string? SourceFileName = null,
    string? SourceFilePath = null,
    string? OperationId = null,
    long Sequence = 0,
    string? SupersedesOperationId = null,
    bool IsEffectiveReplacement = false,
    string? DraftId = null,
    string? DraftMode = null,
    bool IsEffectiveEdit = false)
{
    public DateTimeOffset DisplayTimestampUtc => AppliedAtUtc ?? CreatedAtUtc;

    public static AssetChangeRecordDto FromOperation(
        WorkspaceOperationRecordDto operation,
        bool isEffectiveOperation = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var kind = MapKind(operation.OperationKind);

        return new AssetChangeRecordDto(
            Kind: kind,
            CreatedAtUtc: operation.CreatedAtUtc,
            AppliedAtUtc: operation.AppliedAtUtc ?? operation.CompletedAtUtc,
            SourceBundleDisplayName: operation.Source?.SourceBundleDisplayName,
            SourceAssetName: operation.Source?.SourceAssetName,
            SourceFileName: operation.Source?.SourceFileName,
            SourceFilePath: operation.Source?.SourceFilePath,
            OperationId: operation.OperationId,
            Sequence: operation.Sequence,
            SupersedesOperationId: operation.SupersedesOperationId,
            IsEffectiveReplacement: isEffectiveOperation &&
                kind is AssetChangeRecordKind.FileReplacement or AssetChangeRecordKind.WorkspaceReplacement,
            DraftId: operation.Source?.DraftId ?? operation.Result?.DraftId,
            DraftMode: operation.Source?.DraftMode,
            IsEffectiveEdit: isEffectiveOperation && kind == AssetChangeRecordKind.SpriteEdit);
    }

    private static AssetChangeRecordKind MapKind(WorkspaceOperationKind operationKind) =>
        operationKind switch
        {
            WorkspaceOperationKind.ReplaceFromFile => AssetChangeRecordKind.FileReplacement,
            WorkspaceOperationKind.ReplaceFromWorkspace => AssetChangeRecordKind.WorkspaceReplacement,
            WorkspaceOperationKind.ExtractAsset => AssetChangeRecordKind.Extraction,
            WorkspaceOperationKind.EditDraftApplied => AssetChangeRecordKind.SpriteEdit,
            _ => AssetChangeRecordKind.Unknown
        };
}
