namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 描述导出 manifest 中单个文件在后续批量替换里的身份角色。
/// </summary>
public enum WorkspaceExportManifestOutputRole
{
    Asset = 0,
    SpineSkeleton = 1,
    SpineAtlas = 2,
    SpineTexturePage = 3,
    SpineAlphaTexturePage = 4
}
