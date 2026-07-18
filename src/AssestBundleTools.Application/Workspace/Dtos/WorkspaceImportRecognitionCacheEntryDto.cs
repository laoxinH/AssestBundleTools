using AssestBundleTools.Application.Common.Messages;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 保存单个导入来源已经确认的轻量识别结论，用于跳过未变化文件的重复 CLI Probe。
/// </summary>
public sealed record WorkspaceImportRecognitionCacheEntryDto(
    string WorkspaceKey,
    string BundleCacheKey,
    string SourcePath,
    string LogicalPath,
    WorkspaceFileFingerprintDto SourceFingerprint,
    string RecognitionVersion,
    string CodecCatalogFingerprint,
    WorkspaceImportRecognitionDisposition Disposition,
    string? DecryptorId,
    string? DecryptorVersion,
    string? ReaderParserVersion,
    WorkspaceImportIssueKind? IssueKind,
    LocalizedMessageDto? DetailMessage,
    DateTimeOffset CachedAtUtc);

public enum WorkspaceImportRecognitionDisposition
{
    Candidate = 0,
    ScreenedOut = 1,
    ReaderRejected = 2
}
