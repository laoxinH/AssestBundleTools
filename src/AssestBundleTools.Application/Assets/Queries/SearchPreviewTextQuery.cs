using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求在完整文本预览载荷中搜索有限数量匹配。
/// </summary>
public sealed record SearchPreviewTextQuery(
    string WorkspaceDataRootPath,
    AssetPreviewFullContentReferenceDto Reference,
    string SearchText,
    int StartLineIndex,
    int MaxMatchCount,
    bool MatchCase);
