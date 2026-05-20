using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 从外部文件读取替换来源预览载荷。
/// </summary>
public sealed class FileSystemReplacementPreviewSourceProvider : IReplacementPreviewSourceProvider
{
    public async Task<Result<AssetPreviewContentDto>> LoadFilePreviewAsync(
        AssetDetailsDto targetDetails,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetDetails);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }

        if (targetDetails.AssetType != AssetType.Texture2D)
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement preview type is not supported."));
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement source path is empty."));
        }

        try
        {
            var normalizedPath = sourceFilePath.Trim();
            if (!File.Exists(normalizedPath))
            {
                return Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, "Replacement source file was not found."));
            }

            var content = await File.ReadAllBytesAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            return Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Image,
                    ContentType: ResolveImageContentType(normalizedPath),
                    Content: content,
                    PreferredDisplaySizeInBytes: content.LongLength));
        }
        catch (OperationCanceledException)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, ex.GetType().Name));
        }
    }

    private static string ResolveImageContentType(string path)
    {
        return Path.GetExtension(path).Trim().ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".tga" => "image/x-tga",
            ".dds" => "image/vnd-ms.dds",
            _ => "image/png"
        };
    }
}
