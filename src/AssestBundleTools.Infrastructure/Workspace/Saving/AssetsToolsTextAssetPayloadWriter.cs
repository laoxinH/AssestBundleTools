using System.Text;
using AssetsTools.NET;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class AssetsToolsTextAssetPayloadWriter
{
    private const int TextDraftChunkLength = 32768;

    public static Result<object> ApplyFilePayload(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath)
    {
        return ApplyTextAssetPayload(baseField, writeItem.AssetName, File.ReadAllText(payloadPath, Encoding.UTF8));
    }

    public static Result<object> ApplyDraft(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem)
    {
        if (writeItem.AssetType != AssetType.TextAsset)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.UnsupportedTextDraft"));
        }

        var text = LoadTextDraftContent(writeItem);
        if (text is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.TextDraftMissing"));
        }

        return ApplyTextAssetPayload(baseField, writeItem.AssetName, text);
    }

    private static Result<object> ApplyTextAssetPayload(
        AssetTypeValueField baseField,
        string assetName,
        string text)
    {
        var scriptField = AssetsToolsFieldReader.TryGetField(baseField, "m_Script");
        if (scriptField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    assetName,
                    "Save.BundleWrite.TextAssetScriptUnavailable"));
        }

        scriptField.AsString = text;
        return Result<object>.Success(new object());
    }

    private static string? LoadTextDraftContent(WorkspaceBundleAssetWriteItemDto writeItem)
    {
        if (string.IsNullOrWhiteSpace(writeItem.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(writeItem.WorkspaceDataRootPath) ||
            string.IsNullOrWhiteSpace(writeItem.DraftId))
        {
            return null;
        }

        var draftMode = string.IsNullOrWhiteSpace(writeItem.DraftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : writeItem.DraftMode.Trim();
        var store = new JsonAssetEditDraftTextStore(writeItem.WorkspaceDataRootPath.Trim());
        var builder = new StringBuilder();
        var offset = 0;

        while (true)
        {
            var chunk = store.LoadTextChunk(
                writeItem.WorkspaceKey.Trim(),
                writeItem.DraftId.Trim(),
                draftMode,
                offset,
                TextDraftChunkLength,
                writeItem.DraftRevision);
            if (chunk is null)
            {
                return null;
            }

            builder.Append(chunk.Text);
            offset = chunk.Offset + chunk.Length;
            var totalLength = Math.Max(chunk.TotalLength, offset);
            if (offset >= totalLength || chunk.Length == 0)
            {
                return builder.ToString();
            }
        }
    }
}
