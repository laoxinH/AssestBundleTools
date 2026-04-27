using AssestBundleTools.Application.Common.Messages;

namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示对单个导入来源执行快速识别后的结果。
/// </summary>
public sealed record AssetBundleSourceInspectionResultDto(
    AssetBundleSourceInspectionKind Kind,
    LocalizedMessageDto? DetailMessage = null);
