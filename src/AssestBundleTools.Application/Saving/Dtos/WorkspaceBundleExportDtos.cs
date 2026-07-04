using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

public sealed record WorkspaceBundleExportRequestDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    BundleId BundleId,
    string BundleDisplayName,
    string OutputFilePath,
    string SourceArtifactPath,
    string? BundleCacheKey,
    IReadOnlyList<WorkspaceChangeItemDto> ChangeItems,
    IReadOnlyList<WorkspaceBundleAssetWriteItemDto> AssetWriteItems,
    IReadOnlyList<WorkspaceExportProcessorSelectionDto> ProcessorSelections,
    IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> ProcessorResults,
    string OperationId,
    WorkspaceBundleExportCompressionModeDto CompressionMode = WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
    WorkspaceBundleExportOutputConflictPolicyDto OutputConflictPolicy = WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting,
    string? OriginalCompression = null,
    string? BundleSessionCacheKey = null);

public sealed record WorkspaceBundleExportResultDto(
    BundleId BundleId,
    string BundleDisplayName,
    string OutputFilePath,
    string SourceArtifactPath,
    string? BundleCacheKey,
    long WrittenFileSizeInBytes,
    string WrittenFileHashAlgorithm,
    string WrittenFileHash,
    DateTimeOffset WrittenFileLastWriteTimeUtc,
    string? WrittenFileMd5 = null,
    string? WrittenFileSha256 = null,
    IReadOnlyList<WorkspaceBundleProcessorExecutionSummaryDto>? ProcessorResults = null,
    WorkspaceBundleExportCompressionModeDto CompressionMode = WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
    string? OriginalCompression = null,
    string? WrittenCompression = null);

public sealed record WorkspaceBundleExportManifestSaveRequestDto(
    WorkspaceId WorkspaceId,
    string? WorkspaceKey,
    string WorkspaceDisplayName,
    string OutputDirectory,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> BundleResults,
    IReadOnlyList<WorkspaceExportProcessorSelectionDto> ProcessorSelections);

public sealed record WorkspaceBundleExportManifestSaveResultDto(
    string ManifestPath,
    WorkspaceBundleExportManifestDto Manifest);

public sealed record WorkspaceBundleExportManifestDto(
    int SchemaVersion,
    string WorkspaceId,
    string? WorkspaceKey,
    string WorkspaceDisplayName,
    string OutputDirectory,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<WorkspaceBundleExportManifestItemDto> Bundles,
    IReadOnlyList<WorkspaceBundleExportManifestProcessorSelectionDto> ProcessorSelections);

public sealed record WorkspaceBundleExportManifestItemDto(
    string BundleId,
    string BundleDisplayName,
    string RelativePath,
    string OutputPath,
    string Status,
    int ChangeCount,
    int ModifiedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    string? BundleCacheKey,
    string? SourceArtifactPath,
    long WrittenFileSizeInBytes,
    string? WrittenFileHashAlgorithm,
    string? WrittenFileHash,
    DateTimeOffset? WrittenFileLastWriteTimeUtc,
    string? DiagnosticCode,
    string? DiagnosticDetail,
    string? WrittenFileMd5 = null,
    string? WrittenFileSha256 = null,
    IReadOnlyList<WorkspaceBundleExportManifestProcessorResultDto>? ProcessorResults = null,
    string? CompressionMode = null,
    string? OriginalCompression = null,
    string? WrittenCompression = null);

public sealed record WorkspaceBundleExportManifestProcessorSelectionDto(
    string ResourceKind,
    string? ProcessorId);

public sealed record WorkspaceBundleExportManifestProcessorResultDto(
    string ResourceKind,
    string ProcessorId,
    string Status,
    int InputCount,
    int ProcessedCount,
    int FailedCount,
    IReadOnlyList<string> ResultPaths,
    IReadOnlyList<WorkspaceResourceProcessorDiagnosticDto> Diagnostics);

public sealed record WorkspaceBundleAssetWriteItemDto(
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    string SerializedFileName,
    long PathId,
    string SourceKind,
    string? SourceFilePath = null,
    BundleId? SourceBundleId = null,
    AssetId? SourceAssetId = null,
    string? SourceSerializedFileName = null,
    long? SourcePathId = null,
    WorkspaceSpriteEditGeometryDto? SpriteEditGeometry = null,
    string? OperationId = null,
    long Sequence = 0,
    string? ProcessorOutputPath = null,
    string? ProcessorId = null,
    string? ResourceKind = null);
