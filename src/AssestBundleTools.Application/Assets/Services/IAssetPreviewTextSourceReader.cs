using System.IO;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 为结构化解析和运行时适配器创建完整文本读取器。
/// </summary>
public interface IAssetPreviewTextSourceReader
{
    TextReader OpenTextReader(AssetPreviewTextSourceDto source);
}
