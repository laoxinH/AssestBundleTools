using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示已经成功写入导出资源包的资产及其有效写入来源。
/// </summary>
public sealed record WorkspaceBundleWrittenAssetDto(
    AssetId AssetId,
    string AssetName,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    string SerializedFileName,
    long PathId,
    string SourceKind,
    string? SourceFileName = null,
    BundleId? SourceBundleId = null,
    AssetId? SourceAssetId = null,
    string? SourceSerializedFileName = null,
    long? SourcePathId = null,
    string? OperationId = null,
    long Sequence = 0,
    string? ResourceKind = null,
    string? ProcessorId = null,
    WorkspaceResourceProcessorStatusDto? ProcessorStatus = null,
    string? ProcessorResultPath = null);

public sealed record WorkspaceBundleExportManifestWrittenAssetDto(
    string AssetId,
    string AssetName,
    string AssetType,
    string? AssetTypeDisplayName,
    string SerializedFileName,
    long PathId,
    string SourceKind,
    string? SourceFileName,
    string? SourceBundleId,
    string? SourceAssetId,
    string? SourceSerializedFileName,
    long? SourcePathId,
    string? OperationId,
    long Sequence,
    string? ResourceKind,
    string? ProcessorId,
    string? ProcessorStatus,
    string? ProcessorResultPath);
