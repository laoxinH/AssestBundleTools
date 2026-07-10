using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示单个资源包在保存导出阶段的写出结果。
/// </summary>
public sealed record WorkspaceSaveBundleExecutionResultDto(
    BundleId BundleId,
    string BundleDisplayName,
    string OutputFilePath,
    WorkspaceSaveBundleExecutionStatusDto Status,
    int ChangeCount,
    int ModifiedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    string? SourceArtifactPath = null,
    string? BundleCacheKey = null,
    long WrittenFileSizeInBytes = 0,
    string? WrittenFileHashAlgorithm = null,
    string? WrittenFileHash = null,
    DateTimeOffset? WrittenFileLastWriteTimeUtc = null,
    string? WrittenFileMd5 = null,
    string? WrittenFileSha256 = null,
    string? DiagnosticCode = null,
    string? DiagnosticDetail = null,
    IReadOnlyList<WorkspaceBundleProcessorExecutionSummaryDto>? ProcessorResults = null,
    WorkspaceBundleExportCompressionModeDto CompressionMode = WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
    string? OriginalCompression = null,
    string? WrittenCompression = null,
    string? OutputCodecId = null,
    string? OutputCodecVersion = null);
