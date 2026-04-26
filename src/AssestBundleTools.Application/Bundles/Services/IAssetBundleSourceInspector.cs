using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 提供导入来源的轻量快速识别。
/// </summary>
/// <remarks>
/// 该接口只负责“解密前”的保守直检：
/// 1. 尽早命中明显就是普通 Unity 资源包的文件；
/// 2. 拒绝明显无效的输入；
/// 3. 其余不确定项交给解密器自动识别继续判断。
/// </remarks>
public interface IAssetBundleSourceInspector
{
    AssetBundleSourceInspectionResultDto Inspect(string sourcePath);
}
