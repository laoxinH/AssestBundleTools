using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示当前阶段基于真实已选文件生成的候选外部文件来源。
/// </summary>
/// <remarks>
/// 当前 DTO 负责承载“文件选择器 -> 应用层轻量校验 -> 检查器待应用状态”这条链路中的候选信息，
/// 用来稳定检查器交互、工作区状态回写和批处理摘要链路。
/// </remarks>
public sealed record FileReplacementCandidateDto(
    string SourceFilePath,
    string SourceFileName,
    AssetType SourceAssetType);
