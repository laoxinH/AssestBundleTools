using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 把用户选择的文件或目录路径展开为一层外部来源文件。
/// </summary>
public interface IReplacementSourcePathExpander
{
    Task<Result<IReadOnlyList<string>>> ExpandAsync(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default);
}
