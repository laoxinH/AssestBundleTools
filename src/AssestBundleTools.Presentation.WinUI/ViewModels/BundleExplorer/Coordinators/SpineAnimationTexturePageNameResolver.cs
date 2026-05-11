using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 收集 Spine 动画预览可能需要的 atlas page 名称。
/// </summary>
internal static class SpineAnimationTexturePageNameResolver
{
    public static IReadOnlyList<string> Resolve(SpineAssetGroupDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var preview = details.AnimationPreview;
        if (preview is null)
        {
            return Array.Empty<string>();
        }

        var pageNames = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        AddAttachmentPageNames(pageNames, keys, preview.Attachments);
        AddFramePageNames(pageNames, keys, preview.SetupFrame);
        AddClipPageNames(pageNames, keys, preview.Animations);

        foreach (var skinPreview in preview.SkinPreviews)
        {
            AddAttachmentPageNames(pageNames, keys, skinPreview.Attachments);
            AddFramePageNames(pageNames, keys, skinPreview.SetupFrame);
            AddClipPageNames(pageNames, keys, skinPreview.Animations);
        }

        if (preview.PlaybackSource is not null || pageNames.Count == 0)
        {
            AddAtlasPageNames(pageNames, keys, details.AtlasPages);
        }

        return pageNames;
    }

    private static void AddAttachmentPageNames(
        ICollection<string> pageNames,
        ISet<string> keys,
        IEnumerable<SpineAnimationAttachmentDto> attachments)
    {
        foreach (var attachment in attachments)
        {
            AddPageName(pageNames, keys, attachment.PageName);
        }
    }

    private static void AddClipPageNames(
        ICollection<string> pageNames,
        ISet<string> keys,
        IEnumerable<SpineAnimationClipDto> clips)
    {
        foreach (var clip in clips)
        {
            foreach (var frame in clip.SampledFrames)
            {
                AddFramePageNames(pageNames, keys, frame);
            }
        }
    }

    private static void AddFramePageNames(
        ICollection<string> pageNames,
        ISet<string> keys,
        SpineAnimationFrameDto? frame)
    {
        if (frame is null)
        {
            return;
        }

        foreach (var drawCommand in frame.DrawCommands)
        {
            AddPageName(pageNames, keys, drawCommand.PageName);
        }
    }

    private static void AddAtlasPageNames(
        ICollection<string> pageNames,
        ISet<string> keys,
        IEnumerable<SpineAtlasPageDto> atlasPages)
    {
        foreach (var atlasPage in atlasPages)
        {
            if (atlasPage.TexturePageAsset is null)
            {
                continue;
            }

            AddPageName(pageNames, keys, atlasPage.Name);
        }
    }

    private static void AddPageName(
        ICollection<string> pageNames,
        ISet<string> keys,
        string? pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return;
        }

        var normalizedPageName = pageName.Trim();
        if (keys.Add(normalizedPageName))
        {
            pageNames.Add(normalizedPageName);
        }
    }
}
