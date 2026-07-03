using AssestBundleTools.Application.Workspace.Dtos;
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
    IReadOnlyList<WorkspaceExportProcessorSelectionDto> ProcessorSelections,
    string OperationId);

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
    string? WrittenFileSha256 = null);

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
    string? WrittenFileSha256 = null);

public sealed record WorkspaceBundleExportManifestProcessorSelectionDto(
    string ResourceKind,
    string? ProcessorId);
