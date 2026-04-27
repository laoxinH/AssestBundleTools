using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 当前阶段用于识别 Unity 资源包文件的启发式 Reader。
/// </summary>
/// <remarks>
/// 该实现先只产出资源包级索引，不做资产级真实解析，
/// 目的是优先把“导入来源 -> 缓存 -> 资源包列表”主链路稳定下来。
/// </remarks>
public sealed class HeuristicAssetBundleReader : IAssetBundleReader
{
    private const string ParserVersion = "heuristic-reader/1.0";

    public string CacheParserVersion => ParserVersion;

    public bool CanRead(string bundlePath) => UnityAssetBundleFileInspector.CanTreatAsBundle(bundlePath);

    public bool SupportsCacheParserVersion(string parserVersion)
    {
        return string.Equals(parserVersion, ParserVersion, StringComparison.OrdinalIgnoreCase);
    }

    public Task<Result<AssetBundleReadResultDto>> ReadAsync(
        AssetBundleReadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(request.BundlePath))
        {
            return Task.FromResult(Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleSourceMissing(request.BundlePath)));
        }

        if (!CanRead(request.BundlePath))
        {
            return Task.FromResult(Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleReadFormatUnrecognized(request.BundlePath)));
        }

        var logicalPath = UnityAssetBundleFileInspector.NormalizeLogicalPath(request.LogicalPath, request.BundlePath);
        var result = new AssetBundleReadResultDto(
            DisplayName: Path.GetFileName(request.BundlePath),
            LogicalPath: logicalPath,
            ParserVersion: ParserVersion,
            Dependencies: Array.Empty<string>(),
            Assets: Array.Empty<AssetBundleReadAssetDto>(),
            SerializedFileNames: Array.Empty<string>());

        return Task.FromResult(Result<AssetBundleReadResultDto>.Success(result));
    }
}
