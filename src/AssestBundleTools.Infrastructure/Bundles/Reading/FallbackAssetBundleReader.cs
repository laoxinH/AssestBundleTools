using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 顺序尝试多个 Reader，并在真实解析失败时保留启发式回退能力。
/// </summary>
public sealed class FallbackAssetBundleReader : IAssetBundleReader
{
    private readonly IReadOnlyList<IAssetBundleReader> _readers;

    public FallbackAssetBundleReader(params IAssetBundleReader[] readers)
    {
        ArgumentNullException.ThrowIfNull(readers);

        _readers = readers
            .Where(static reader => reader is not null)
            .ToArray();
        if (_readers.Count == 0)
        {
            throw new ArgumentException("At least one reader is required.", nameof(readers));
        }
    }

    public string CacheParserVersion => string.Join(
        " | ",
        _readers.Select(static reader => reader.CacheParserVersion).Distinct(StringComparer.OrdinalIgnoreCase));

    public bool CanRead(string bundlePath)
    {
        return _readers.Any(reader => reader.CanRead(bundlePath));
    }

    public bool SupportsCacheParserVersion(string parserVersion)
    {
        return _readers.Any(reader => reader.SupportsCacheParserVersion(parserVersion));
    }

    public async Task<Result<AssetBundleReadResultDto>> ReadAsync(
        AssetBundleReadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<AppError>();

        foreach (var reader in _readers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!reader.CanRead(request.BundlePath))
            {
                continue;
            }

            var result = await reader.ReadAsync(request, cancellationToken);
            if (result.IsSuccess || result.IsCancelled)
            {
                return result;
            }

            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            return Result<AssetBundleReadResultDto>.Failure(failures);
        }

        return Result<AssetBundleReadResultDto>.Failure(
            ApplicationErrors.BundleReadNoCompatibleReader(request.BundlePath));
    }
}
