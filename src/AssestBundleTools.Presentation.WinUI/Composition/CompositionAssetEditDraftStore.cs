using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 当前阶段尚未落地真实草稿暂存层，组合根先提供只读空仓储以打通恢复预检闭环。
/// </summary>
internal sealed class CompositionAssetEditDraftStore : IAssetEditDraftStore
{
    public AssetEditDraftDescriptorDto? Load(AssetEditDraftLookupKey lookupKey) => null;
}
