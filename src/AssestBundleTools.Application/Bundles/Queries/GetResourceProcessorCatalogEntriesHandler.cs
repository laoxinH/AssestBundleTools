using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Queries;

/// <summary>
/// 返回当前阶段可用于导出窗口和插件管理页展示的资源处理器目录。
/// </summary>
public sealed class GetResourceProcessorCatalogEntriesHandler
    : IQueryHandler<GetResourceProcessorCatalogEntriesQuery, Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>>
{
    private static readonly IReadOnlyList<ResourceProcessorCatalogEntryDto> BuiltInEntries =
    [
        CreatePlaceholder("resource.processor.texture2d.cli", "Texture2D"),
        CreatePlaceholder("resource.processor.audio.cli", "Audio"),
        CreatePlaceholder("resource.processor.atlas.cli", "Atlas"),
        CreatePlaceholder("resource.processor.skel.cli", "Skel")
    ];

    public Task<Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>> HandleAsync(
        GetResourceProcessorCatalogEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>.Cancelled());
        }

        return Task.FromResult(Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>.Success(BuiltInEntries));
    }

    private static ResourceProcessorCatalogEntryDto CreatePlaceholder(
        string id,
        string resourceKind) =>
        new(
            id,
            id,
            Version: "1.0.0",
            Description: string.Empty,
            ResourceProcessorKind.ExternalProcess,
            [resourceKind],
            ResourceProcessorStatus.Available,
            IsBuiltIn: true,
            IsReadOnly: true,
            ManifestFilePath: null,
            ExecutablePath: null,
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments:
            [
                "--input",
                "{inputPath}",
                "--output",
                "{outputPath}",
                "--result",
                "{resultPath}",
                "--resource-kind",
                "{resourceKind}"
            ]);
}
