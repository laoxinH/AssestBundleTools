using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 将预览生成阶段的标准错误映射为摘要回退语义。
/// </summary>
internal static class AssetPreviewGenerationFallbackResolver
{
    private const string BundlePreviewOutOfMemoryErrorCode = "Bundle.Preview.OutOfMemory";

    public static AssetPreviewGenerationFallback Resolve(IReadOnlyList<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var isOutOfMemory = errors.Count > 0 &&
                            string.Equals(errors[0].Code, BundlePreviewOutOfMemoryErrorCode, StringComparison.Ordinal);
        return isOutOfMemory
            ? new AssetPreviewGenerationFallback(
                AssetPreviewMessageKey.OutOfMemory,
                AssetPreviewFallbackReasonKind.OutOfMemory)
            : new AssetPreviewGenerationFallback(
                AssetPreviewMessageKey.GenerationFailed,
                AssetPreviewFallbackReasonKind.GenerationFailed);
    }
}

internal sealed record AssetPreviewGenerationFallback(
    AssetPreviewMessageKey MessageKey,
    AssetPreviewFallbackReasonKind FallbackReason);
