namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 定义资产详情页当前可展示的操作记录类型。
/// </summary>
public enum AssetChangeRecordKind
{
    Unknown = 0,
    Extraction = 1,
    FileReplacement = 2,
    WorkspaceReplacement = 3,
    SpriteEdit = 4,
    AssociatedFileReplacement = 5,
    TextEdit = 6
}
