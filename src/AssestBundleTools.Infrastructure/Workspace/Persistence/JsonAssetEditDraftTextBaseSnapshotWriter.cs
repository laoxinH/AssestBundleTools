using System.Text;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed class JsonAssetEditDraftTextBaseSnapshotWriter
{
    private readonly JsonAssetEditDraftTextSourceReader _sourceReader;

    public JsonAssetEditDraftTextBaseSnapshotWriter(JsonAssetEditDraftTextSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    public TextAssetEditDraftPatchStatisticsDto? TryCreate(
        string baseSnapshotFilePath,
        WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        if (File.Exists(baseSnapshotFilePath))
        {
            using var existingReader = JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(baseSnapshotFilePath);
            return JsonAssetEditDraftTextReadSupport.CalculateStatistics(
                existingReader,
                int.MaxValue,
                out _);
        }

        using var sourceReader = _sourceReader.CreateSourceReader(sourceReference);
        return sourceReader is null
            ? null
            : WriteNormalizedBaseSnapshot(baseSnapshotFilePath, sourceReader);
    }

    private static TextAssetEditDraftPatchStatisticsDto WriteNormalizedBaseSnapshot(
        string baseSnapshotFilePath,
        TextReader sourceReader)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(baseSnapshotFilePath)!);
        var temporaryFilePath = $"{baseSnapshotFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryFilePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096))
            {
                while (JsonAssetEditDraftTextReadSupport.TryReadNormalizedCharacter(sourceReader, out var character))
                {
                    writer.Write(character);
                }
            }

            File.Move(temporaryFilePath, baseSnapshotFilePath, overwrite: true);
        }
        finally
        {
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(temporaryFilePath);
        }

        using var snapshotReader = JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(baseSnapshotFilePath);
        return JsonAssetEditDraftTextReadSupport.CalculateStatistics(
            snapshotReader,
            int.MaxValue,
            out _)!;
    }
}
