using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求从完整文本预览载荷中读取一组文本行。
/// </summary>
public sealed record LoadPreviewTextLinesQuery(
    string WorkspaceDataRootPath,
    AssetPreviewFullContentReferenceDto Reference,
    int StartLineIndex,
    int LineCount);
