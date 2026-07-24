namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 指定预览查询读取当前有效内容，或绕过工作区变更读取原始资源内容。
/// </summary>
public enum AssetPreviewContentScope
{
    Effective = 0,
    Original = 1
}
