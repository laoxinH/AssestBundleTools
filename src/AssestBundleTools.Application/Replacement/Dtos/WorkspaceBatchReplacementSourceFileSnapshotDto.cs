namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示批量替换来源文件当前状态。
/// </summary>
public sealed record WorkspaceBatchReplacementSourceFileSnapshotDto(
    string FullPath,
    bool Exists,
    long? SizeInBytes,
    string? HashAlgorithm,
    string? Hash);
