using AssetsTools.NET;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 管理 AB 内外部资源条目的惰性加载、追加和最终回写。
/// </summary>
internal sealed class AssetsToolsBundleResourceDataWriteContext
{
    private readonly AssetBundleFile _bundleFile;
    private readonly Dictionary<int, MutableResourceFile> _resourceFiles = new();

    public AssetsToolsBundleResourceDataWriteContext(AssetBundleFile bundleFile)
    {
        _bundleFile = bundleFile ?? throw new ArgumentNullException(nameof(bundleFile));
    }

    public Result<BundleResourceAppendResult> AppendPayload(
        string? resourceSource,
        byte[] payload,
        string assetName)
    {
        if (payload.Length == 0)
        {
            return Result<BundleResourceAppendResult>.Failure(
                ApplicationErrors.BundleExportWriteFailed(assetName, "Save.BundleWrite.AudioPayloadEmpty"));
        }

        var resourceIndex = ResolveResourceFileIndex(resourceSource);
        if (resourceIndex < 0)
        {
            return Result<BundleResourceAppendResult>.Failure(
                ApplicationErrors.BundleExportWriteFailed(assetName, "Save.BundleWrite.AudioResourceFileNotFound"));
        }

        var resourceFile = GetOrLoadResourceFile(resourceIndex);
        var newOffset = resourceFile.Content.Length;
        resourceFile.Content.Position = newOffset;
        resourceFile.Content.Write(payload, 0, payload.Length);
        resourceFile.IsDirty = true;

        return Result<BundleResourceAppendResult>.Success(new BundleResourceAppendResult(
            ResolveResourceSource(resourceSource, resourceFile.EntryName),
            newOffset,
            payload.LongLength));
    }

    public void ApplyModifiedFiles()
    {
        var directoryInfos = _bundleFile.BlockAndDirInfo.DirectoryInfos;
        foreach (var resourceFile in _resourceFiles.Values.Where(static file => file.IsDirty))
        {
            var directoryInfo = directoryInfos[resourceFile.DirectoryIndex];
            directoryInfo.SetNewData(resourceFile.Content.ToArray());
            directoryInfos[resourceFile.DirectoryIndex] = directoryInfo;
        }
    }

    private MutableResourceFile GetOrLoadResourceFile(int directoryIndex)
    {
        if (_resourceFiles.TryGetValue(directoryIndex, out var existing))
        {
            return existing;
        }

        var directoryInfo = _bundleFile.BlockAndDirInfo.DirectoryInfos[directoryIndex];
        _bundleFile.GetFileRange(directoryIndex, out var fileOffset, out var fileLength);
        if (fileLength < 0 || fileLength > int.MaxValue)
        {
            throw new InvalidOperationException("Bundle resource file is too large to rewrite.");
        }

        _bundleFile.DataReader.Position = fileOffset;
        var content = _bundleFile.DataReader.ReadBytes((int)fileLength);
        var contentStream = new MemoryStream(Math.Max(content.Length, content.Length + 1024));
        contentStream.Write(content, 0, content.Length);
        var resourceFile = new MutableResourceFile(
            directoryIndex,
            directoryInfo.Name,
            contentStream);
        _resourceFiles[directoryIndex] = resourceFile;
        return resourceFile;
    }

    private int ResolveResourceFileIndex(string? resourceSource)
    {
        var resourceEntryName = ResolveBundleEntryName(resourceSource);
        if (!string.IsNullOrWhiteSpace(resourceEntryName))
        {
            var exactIndex = _bundleFile.GetFileIndex(resourceEntryName);
            if (exactIndex >= 0)
            {
                return exactIndex;
            }

            var directoryInfos = _bundleFile.BlockAndDirInfo.DirectoryInfos;
            for (var index = 0; index < directoryInfos.Count; index++)
            {
                if (string.Equals(directoryInfos[index].Name, resourceEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        return ResolveFirstResourceFileIndex();
    }

    private int ResolveFirstResourceFileIndex()
    {
        var directoryInfos = _bundleFile.BlockAndDirInfo.DirectoryInfos;
        for (var index = 0; index < directoryInfos.Count; index++)
        {
            var entryName = directoryInfos[index].Name;
            if (entryName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                entryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveResourceSource(
        string? originalResourceSource,
        string entryName) =>
        string.IsNullOrWhiteSpace(originalResourceSource)
            ? entryName
            : originalResourceSource.Trim();

    private static string? ResolveBundleEntryName(string? resourceSource)
    {
        if (string.IsNullOrWhiteSpace(resourceSource))
        {
            return null;
        }

        var normalizedSource = resourceSource.Trim().Replace('\\', '/');
        var lastSlashIndex = normalizedSource.LastIndexOf('/');
        return lastSlashIndex < 0 || lastSlashIndex >= normalizedSource.Length - 1
            ? Path.GetFileName(normalizedSource)
            : normalizedSource[(lastSlashIndex + 1)..];
    }

    private sealed record MutableResourceFile(
        int DirectoryIndex,
        string EntryName,
        MemoryStream Content)
    {
        public bool IsDirty { get; set; }
    }
}

internal sealed record BundleResourceAppendResult(
    string ResourceSource,
    long Offset,
    long Size);
