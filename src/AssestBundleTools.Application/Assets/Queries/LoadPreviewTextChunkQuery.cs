using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求从完整文本预览载荷中读取一段文本。
/// </summary>
public sealed record LoadPreviewTextChunkQuery(
    string WorkspaceDataRootPath,
    AssetPreviewFullContentReferenceDto Reference,
    int Offset,
    int Length);
