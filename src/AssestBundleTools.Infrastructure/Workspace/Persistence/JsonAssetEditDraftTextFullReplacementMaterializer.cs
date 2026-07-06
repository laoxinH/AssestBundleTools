using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal static class JsonAssetEditDraftTextFullReplacementMaterializer
{
    public static AssetEditDraftTextPatchResultDto? TryMaterialize(
        AssetEditDraftTextPatchDto patch,
        string descriptorFilePath,
        string payloadFilePath,
        string patchManifestFilePath,
        string generatorVersion)
    {
        if (patch.SourceReference is null)
        {
            return null;
        }

        if (!TryGetFullReplacementPatchText(patch, out var normalizedText))
        {
            return null;
        }

        if (normalizedText.Length > patch.MaxTextLength)
        {
            return AssetEditDraftTextPatchResultDto.LengthExceeded();
        }

        var descriptor = CreateDescriptor(
            patch,
            normalizedText,
            generatorVersion);
        var content = Encoding.UTF8.GetBytes(normalizedText);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            descriptorFilePath,
            descriptor,
            JsonWorkspaceSerialization.SerializerOptions);
        JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, content);
        JsonPreviewCacheTextLineIndex.Save(descriptor, payloadFilePath, content);
        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(patchManifestFilePath);

        return AssetEditDraftTextPatchResultDto.Applied(
            normalizedText.Length,
            content.Length,
            revision: 0,
            TextAssetEditDraftPatchBaseline.Create(normalizedText));
    }

    private static bool TryGetFullReplacementPatchText(
        AssetEditDraftTextPatchDto patch,
        out string normalizedText)
    {
        normalizedText = string.Empty;
        var rangeEdits = patch.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>();
        if (rangeEdits.Count != 1)
        {
            return false;
        }

        var edit = rangeEdits[0];
        if (edit.Start != 0 ||
            edit.Length != patch.Baseline.ContentLength)
        {
            return false;
        }

        normalizedText = NormalizeTextContent(edit.Text);
        return true;
    }

    private static PreviewCacheDescriptorDto CreateDescriptor(
        AssetEditDraftTextPatchDto patch,
        string normalizedText,
        string generatorVersion)
    {
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);
        return new PreviewCacheDescriptorDto(
            WorkspaceKey: patch.WorkspaceKey.Trim(),
            PreviewCacheKey: string.Concat("draft.", patch.DraftMode.Trim(), ".", patch.DraftId.Trim()),
            BundleCacheKey: $"draft:{patch.DraftId.Trim()}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: string.IsNullOrWhiteSpace(patch.ContentType)
                ? "text/plain; charset=utf-8"
                : patch.ContentType.Trim(),
            RelativePayloadPath: "text.txt",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: $"draft:{patch.DraftId.Trim()}:{patch.DraftMode.Trim()}",
                FileSizeInBytes: Encoding.UTF8.GetByteCount(normalizedText),
                LastWriteTimeUtc: patch.UpdatedAtUtc,
                ContentHash: patch.SourceFingerprint),
            GeneratedAtUtc: patch.UpdatedAtUtc,
            GeneratorVersion: generatorVersion,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata),
            PreferredDisplaySizeInBytes: Encoding.UTF8.GetByteCount(display.TextContent));
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
