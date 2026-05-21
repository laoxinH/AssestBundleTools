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
    bool IsEffectiveReplacement = false)
{
    public DateTimeOffset DisplayTimestampUtc => AppliedAtUtc ?? CreatedAtUtc;

    public static AssetChangeRecordDto FromOperation(
        WorkspaceOperationRecordDto operation,
        bool isEffectiveReplacement = false)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new AssetChangeRecordDto(
            Kind: MapKind(operation.OperationKind),
            CreatedAtUtc: operation.CreatedAtUtc,
            AppliedAtUtc: operation.AppliedAtUtc ?? operation.CompletedAtUtc,
            SourceBundleDisplayName: operation.Source?.SourceBundleDisplayName,
            SourceAssetName: operation.Source?.SourceAssetName,
            SourceFileName: operation.Source?.SourceFileName,
            SourceFilePath: operation.Source?.SourceFilePath,
            OperationId: operation.OperationId,
            Sequence: operation.Sequence,
            SupersedesOperationId: operation.SupersedesOperationId,
            IsEffectiveReplacement: isEffectiveReplacement);
    }

    private static AssetChangeRecordKind MapKind(WorkspaceOperationKind operationKind) =>
        operationKind switch
        {
            WorkspaceOperationKind.ReplaceFromFile => AssetChangeRecordKind.FileReplacement,
            WorkspaceOperationKind.ReplaceFromWorkspace => AssetChangeRecordKind.WorkspaceReplacement,
            WorkspaceOperationKind.ExtractAsset => AssetChangeRecordKind.Extraction,
            _ => AssetChangeRecordKind.Unknown
        };
}
