namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示启发式批量替换扫描到的外部来源文件。
/// </summary>
public sealed record WorkspaceBatchReplacementSourceFileEntryDto(
    string RelativePath,
    string FullPath,
    long? SizeInBytes);
