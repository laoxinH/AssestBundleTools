using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 基于 Unity 资源包头和常见扩展名执行导入源直检。
/// </summary>
public sealed class UnityAssetBundleSourceInspector : IAssetBundleSourceInspector
{
    public AssetBundleSourceInspectionResultDto Inspect(string sourcePath)
    {
        return UnityAssetBundleFileInspector.Inspect(sourcePath);
    }
}
