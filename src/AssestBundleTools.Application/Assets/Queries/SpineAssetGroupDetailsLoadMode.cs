namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 控制 Spine 详情查询在交互预览和完整诊断之间的取舍。
/// </summary>
public enum SpineAssetGroupDetailsLoadMode
{
    Full = 0,
    InteractivePreview = 1,
    InteractiveDiagnostics = 2
}
