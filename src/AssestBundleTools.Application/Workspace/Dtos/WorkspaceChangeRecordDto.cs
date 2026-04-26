namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区暂存层中的一条变更记录。
/// </summary>
/// <remarks>
/// 当前阶段先使用通用结构承接提取、替换、保存前暂存等操作摘要，
/// 后续当真实 AB 保存链路落地时，再按需要拆成更细的专用记录类型。
/// </remarks>
public sealed record WorkspaceChangeRecordDto(
    string ChangeKey,
    string ChangeKind,
    string BundleId,
    string AssetId,
    string Summary,
    DateTimeOffset RecordedAtUtc,
    string? SourceDescriptor,
    string? OutputPath);
