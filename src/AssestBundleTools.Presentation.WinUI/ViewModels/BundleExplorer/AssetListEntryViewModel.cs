using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示资产列表中用于展示的一行数据。
/// </summary>
/// <remarks>
/// 这里保留原始 DTO，方便页面继续围绕“列表展示 + 检查器详情 + 标签页状态恢复”组织交互，
/// 同时把界面需要的格式化文本集中放在 UI 层，避免把展示逻辑回流到应用层。
/// </remarks>
public sealed record AssetListEntryViewModel(
    AssetListItemDto Item,
    string AssetTypeText,
    string SizeText,
    string StateText);
