using System.IO;
using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 通过既有预览管线取得真实资源 payload，并交给文件系统端口写出。
/// </summary>
public sealed class WorkspaceAssetExtractionExporter
{
    private static readonly JsonSerializerOptions StructuredJsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewHandler;
    private readonly IAssetExtractionFileStore _fileStore;

    public WorkspaceAssetExtractionExporter(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewHandler,
        IAssetExtractionFileStore fileStore)
    {
        _assetPreviewHandler = assetPreviewHandler ?? throw new ArgumentNullException(nameof(assetPreviewHandler));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    public async Task<Result<AssetExtractionResultDto>> ExportAsync(
        ExtractAssetCommand command,
        AssetDetailsDto targetDetails,
        AssetExtractionResultDto extractionResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(targetDetails);
        ArgumentNullException.ThrowIfNull(extractionResult);

        var previewResult = await _assetPreviewHandler.HandleAsync(
            new GetAssetPreviewQuery(
                command.WorkspaceId,
                command.WorkspaceKey ?? string.Empty,
                command.WorkspaceDataRootPath ?? string.Empty,
                command.BundleId,
                command.AssetId),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return Result<AssetExtractionResultDto>.Cancelled();
        }

        if (!previewResult.IsSuccess || previewResult.Value is null)
        {
            return Result<AssetExtractionResultDto>.Failure(previewResult.Errors);
        }

        var preview = previewResult.Value;
        var suggestedOutputPath = BuildSuggestedOutputPath(
            command.SuggestedOutputPath ?? extractionResult.SuggestedOutputPath,
            targetDetails,
            preview,
            command.PreserveSuggestedFileName,
            command.SpineSkeletonExtensionMode);
        var payload = ResolvePayload(preview, targetDetails, suggestedOutputPath);
        if (payload.Content is not { Length: > 0 } &&
            string.IsNullOrWhiteSpace(payload.SourcePayloadFilePath))
        {
            return Result<AssetExtractionResultDto>.Failure(
                ApplicationErrors.AssetExtractionPayloadUnavailable(command.AssetId));
        }

        var writeResult = await _fileStore.WriteAsync(
            new AssetExtractionWriteRequestDto(
                WorkspaceDataRootPath: command.WorkspaceDataRootPath ?? string.Empty,
                SuggestedOutputPath: suggestedOutputPath,
                Content: payload.Content,
                SourcePayloadFilePath: payload.SourcePayloadFilePath),
            cancellationToken);

        if (writeResult.IsCancelled)
        {
            return Result<AssetExtractionResultDto>.Cancelled();
        }

        if (!writeResult.IsSuccess || writeResult.Value is null)
        {
            return Result<AssetExtractionResultDto>.Failure(writeResult.Errors);
        }

        return Result<AssetExtractionResultDto>.Success(extractionResult with
        {
            SuggestedOutputPath = suggestedOutputPath,
            OutputPath = writeResult.Value.OutputPath,
            OutputDirectory = writeResult.Value.OutputDirectory,
            OutputFileExists = writeResult.Value.OutputFileExists,
            OutputSizeInBytes = writeResult.Value.OutputSizeInBytes,
            OutputKind = preview.Kind
        });
    }

    private static ExtractionPayload ResolvePayload(
        AssetPreviewDto preview,
        AssetDetailsDto targetDetails,
        string suggestedOutputPath)
    {
        var content = preview.Kind switch
        {
            AssetPreviewKind.Mesh => ResolveMeshPayload(preview, targetDetails, suggestedOutputPath),
            AssetPreviewKind.MonoBehaviour => ResolveMonoBehaviourPayload(preview, targetDetails, suggestedOutputPath),
            AssetPreviewKind.Material or
            AssetPreviewKind.AnimationClip or
            AssetPreviewKind.Font or
            AssetPreviewKind.GameObject or
            AssetPreviewKind.AnimatorController => TrySerializeStructuredPreview(preview),
            AssetPreviewKind.Text or AssetPreviewKind.Summary => TryResolveTextPayload(preview),
            _ => preview.BinaryContent is { Length: > 0 }
                ? preview.BinaryContent
                : TryResolveTextPayload(preview) ?? TrySerializeStructuredPreview(preview)
        };

        if (content is { Length: > 0 })
        {
            return new ExtractionPayload(content, SourcePayloadFilePath: null);
        }

        return new ExtractionPayload(Content: null, preview.PreviewPayloadFilePath);
    }

    private static byte[]? ResolveMeshPayload(
        AssetPreviewDto preview,
        AssetDetailsDto targetDetails,
        string suggestedOutputPath)
    {
        return MeshAssetExportPayloadBuilder.BuildPayload(
            preview,
            targetDetails,
            suggestedOutputPath,
            () => TrySerializeStructuredPreview(preview));
    }

    private static byte[]? ResolveMonoBehaviourPayload(
        AssetPreviewDto preview,
        AssetDetailsDto targetDetails,
        string suggestedOutputPath)
    {
        return MonoBehaviourAssetExportPayloadBuilder.BuildPayload(
            preview,
            targetDetails,
            suggestedOutputPath,
            () => TrySerializeStructuredPreview(preview));
    }

    private static byte[]? TryResolveTextPayload(AssetPreviewDto preview)
    {
        var textContent = preview.FullTextContent ?? preview.TextContent;
        return string.IsNullOrEmpty(textContent)
            ? null
            : Encoding.UTF8.GetBytes(textContent);
    }

    private static byte[]? TrySerializeStructuredPreview(AssetPreviewDto preview)
    {
        object? payload = preview.Kind switch
        {
            AssetPreviewKind.Mesh => preview.MeshGeometry,
            AssetPreviewKind.Material => preview.Material,
            AssetPreviewKind.AnimationClip => preview.AnimationClip,
            AssetPreviewKind.Font => preview.Font,
            AssetPreviewKind.GameObject => preview.GameObject,
            AssetPreviewKind.MonoBehaviour => preview.MonoBehaviour,
            AssetPreviewKind.AnimatorController => preview.AnimatorController,
            _ => null
        };

        return payload is null
            ? null
            : JsonSerializer.SerializeToUtf8Bytes(payload, StructuredJsonSerializerOptions);
    }

    private static string BuildSuggestedOutputPath(
        string suggestedOutputPath,
        AssetDetailsDto targetDetails,
        AssetPreviewDto preview,
        bool preserveSuggestedFileName,
        SpineSkeletonExportExtensionMode spineSkeletonExtensionMode)
    {
        if (preserveSuggestedFileName)
        {
            return suggestedOutputPath.Trim();
        }

        var directory = Path.GetDirectoryName(suggestedOutputPath);
        var fileName = BuildOutputFileName(
            targetDetails,
            preview,
            Path.GetFileName(suggestedOutputPath),
            spineSkeletonExtensionMode);
        return string.IsNullOrWhiteSpace(directory)
            ? fileName
            : Path.Combine(directory, fileName);
    }

    private static string BuildOutputFileName(
        AssetDetailsDto targetDetails,
        AssetPreviewDto preview,
        string? suggestedFileName,
        SpineSkeletonExportExtensionMode spineSkeletonExtensionMode)
    {
        var defaultSourceName = ResolveSourceFileName(targetDetails);
        var extension = ResolveOutputExtension(defaultSourceName, preview, spineSkeletonExtensionMode);
        if (spineSkeletonExtensionMode != SpineSkeletonExportExtensionMode.None)
        {
            var skeletonStem = ResolveSpineSkeletonFileNameStem(suggestedFileName) ??
                               ResolveSpineSkeletonFileNameStem(defaultSourceName) ??
                               targetDetails.AssetId.Value;
            return $"{skeletonStem}{extension}";
        }

        var sourceName = ShouldPreferSourceFileName(targetDetails, preview, defaultSourceName, extension, suggestedFileName)
            ? defaultSourceName
            : ResolveSuggestedFileNameStem(suggestedFileName, extension) ?? defaultSourceName;

        if (sourceName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return sourceName;
        }

        return $"{sourceName}{extension}";
    }

    private static string? ResolveSpineSkeletonFileNameStem(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var stem = Path.GetFileName(fileName.Trim());
        foreach (var extension in new[] { ".skel.bytes", ".skel", ".json", ".bytes" })
        {
            if (stem.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                stem = stem[..^extension.Length];
                break;
            }
        }

        return string.IsNullOrWhiteSpace(stem) ? null : stem;
    }

    private static bool ShouldPreferSourceFileName(
        AssetDetailsDto targetDetails,
        AssetPreviewDto preview,
        string sourceName,
        string outputExtension,
        string? suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return false;
        }

        var sourceExtension = Path.GetExtension(sourceName);
        if (string.IsNullOrWhiteSpace(sourceExtension))
        {
            return false;
        }

        var plannedExtension = WorkspaceAssetExportNameProjection.GetAssetExtension(targetDetails.AssetType);
        return (preview.Kind is AssetPreviewKind.Text or AssetPreviewKind.Binary) &&
               Path.GetExtension(suggestedFileName)
                   .Equals(plannedExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveSuggestedFileNameStem(string? suggestedFileName, string extension)
    {
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return null;
        }

        var trimmed = suggestedFileName.Trim();
        if (trimmed.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var stem = Path.GetFileNameWithoutExtension(trimmed);
        return string.IsNullOrWhiteSpace(stem)
            ? null
            : stem;
    }

    private static string ResolveSourceFileName(AssetDetailsDto targetDetails)
    {
        var fileName = ResolveFileNameFromPath(targetDetails.AssetPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        fileName = ResolveFileNameFromPath(targetDetails.ContainerPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return string.IsNullOrWhiteSpace(targetDetails.Name)
            ? targetDetails.AssetId.Value
            : targetDetails.Name.Trim();
    }

    private static string? ResolveFileNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var fileName = lastSeparatorIndex >= 0
            ? normalizedPath[(lastSeparatorIndex + 1)..]
            : normalizedPath;
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    private static string ResolveOutputExtension(
        string sourceName,
        AssetPreviewDto preview,
        SpineSkeletonExportExtensionMode spineSkeletonExtensionMode) =>
        spineSkeletonExtensionMode switch
        {
            SpineSkeletonExportExtensionMode.UniformSkel => ".skel",
            SpineSkeletonExportExtensionMode.ContentAware
                when SpineSkeletonPayloadFormatClassifier.Classify(preview) == SpineSkeletonAssetFormat.Json => ".json",
            SpineSkeletonExportExtensionMode.ContentAware => ".skel",
            _ => ResolveDefaultOutputExtension(sourceName, preview)
        };

    private static string ResolveDefaultOutputExtension(string sourceName, AssetPreviewDto preview) =>
        preview.Kind switch
        {
            AssetPreviewKind.Image => ".png",
            AssetPreviewKind.Audio => ResolveAudioExtension(preview.ContentType),
            AssetPreviewKind.Text => ResolveTextExtension(sourceName),
            AssetPreviewKind.Binary => ResolveBinaryExtension(sourceName),
            AssetPreviewKind.Mesh => ".obj",
            AssetPreviewKind.Material => ".material.json",
            AssetPreviewKind.AnimationClip => ".animation-clip.json",
            AssetPreviewKind.Font => ".font.json",
            AssetPreviewKind.GameObject => ".game-object.json",
            AssetPreviewKind.MonoBehaviour => ".typetree.json",
            AssetPreviewKind.AnimatorController => ".animator-controller.json",
            AssetPreviewKind.Summary => ".summary.txt",
            _ => ".bin"
        };

    private static string ResolveTextExtension(string sourceName)
    {
        if (sourceName.EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase))
        {
            return ".skel.bytes";
        }

        var extension = Path.GetExtension(sourceName);
        return string.IsNullOrWhiteSpace(extension)
            ? ".txt"
            : extension;
    }

    private static string ResolveBinaryExtension(string sourceName)
    {
        if (sourceName.EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase))
        {
            return ".skel.bytes";
        }

        var extension = Path.GetExtension(sourceName);
        return string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension;
    }

    private static string ResolveAudioExtension(string contentType) =>
        contentType.Trim().ToLowerInvariant() switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/mpeg" => ".mp3",
            "audio/flac" => ".flac",
            "audio/mp4" => ".m4a",
            "audio/aac" => ".aac",
            _ => ".bin"
        };

    private sealed record ExtractionPayload(
        byte[]? Content,
        string? SourcePayloadFilePath);
}
