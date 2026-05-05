namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示右侧检查器可消费的资源预览内容类型。
/// </summary>
public enum AssetPreviewKind
{
    None = 0,
    Image = 1,
    Text = 2,
    Audio = 3,
    Summary = 4,
    Mesh = 5,
    Material = 6,
    AnimationClip = 7,
    Font = 8,
    GameObject = 9,
    MonoBehaviour = 10,
    AnimatorController = 11
}
