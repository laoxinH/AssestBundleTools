namespace AssestBundleTools.Domain.Assets;

/// <summary>
/// 表示工具当前识别到的高层资源类型。
/// </summary>
public enum AssetType
{
    Unknown = 0,
    Texture2D = 1,
    Sprite = 2,
    TextAsset = 3,
    AudioClip = 4,
    Mesh = 5,
    Material = 6
}
