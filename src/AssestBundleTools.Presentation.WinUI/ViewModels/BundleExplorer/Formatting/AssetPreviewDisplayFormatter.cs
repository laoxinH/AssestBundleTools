using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

/// <summary>
/// 负责把结构化预览 DTO 转成检查器可直接消费的展示文本。
/// </summary>
/// <remarks>
/// Application / Infrastructure 只负责产出稳定语义，
/// 这里再统一补上本地化标签、单位和音频面板摘要，避免 UI 自己拼装字符串。
/// </remarks>
internal sealed class AssetPreviewDisplayFormatter
{
    private const string PreviewInfoLabelResourceKeyPrefix = "InspectorPanelView.PreviewInfo.";
    private const string PreviewInfoLabelResourceKeySuffix = ".Label";
    private const string PreviewMessageResourceKeyPrefix = "InspectorPanelView.PreviewMessage.";
    private const string PreviewMessageResourceKeySuffix = ".Text";
    private const string PreviewInfoValueResourceKeyPrefix = "InspectorPanelView.PreviewInfoValue.";
    private const string UnknownSentinelText = "Unknown";

    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;

    public AssetPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
    }

    public IReadOnlyList<InspectorInfoRowViewModel> BuildPreviewInfoRows(
        IReadOnlyList<AssetPreviewInfoItemDto>? items)
    {
        if (items is not { Count: > 0 })
        {
            return Array.Empty<InspectorInfoRowViewModel>();
        }

        var rows = new List<InspectorInfoRowViewModel>(items.Count);
        foreach (var item in items)
        {
            var value = FormatPreviewInfoValue(item);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            rows.Add(new InspectorInfoRowViewModel(
                ResolvePreviewInfoLabel(item.Key),
                value));
        }

        return rows;
    }

    public string BuildPreviewMessageText(AssetPreviewMessageDto? previewMessage)
    {
        if (previewMessage is null || previewMessage.Key == AssetPreviewMessageKey.None)
        {
            return string.Empty;
        }

        var resourceKey = $"{PreviewMessageResourceKeyPrefix}{previewMessage.Key}{PreviewMessageResourceKeySuffix}";
        return previewMessage.Arguments is { Count: > 0 }
            ? _localizationService.GetFormattedString(resourceKey, previewMessage.Arguments.Cast<object>().ToArray())
            : _localizationService.GetRequiredString(resourceKey);
    }

    public AudioPreviewDisplayState BuildAudioPreviewDisplayState(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        long? preferredDisplaySizeInBytes)
    {
        var directContainer = FindTextValue(items, AssetPreviewInfoKey.AudioContainer);
        var originalContainer = FindTextValue(items, AssetPreviewInfoKey.AudioOriginalContainer);
        var outputContainer = FindTextValue(items, AssetPreviewInfoKey.AudioOutputContainer);
        var payloadSize = FindFormattedValue(items, AssetPreviewInfoKey.AudioPayloadSize);
        var channelCount = FindInt64Value(items, AssetPreviewInfoKey.AudioChannels);
        var frequency = FindFormattedValue(items, AssetPreviewInfoKey.AudioFrequency);
        var duration = FindFormattedValue(items, AssetPreviewInfoKey.AudioDuration);

        var effectiveOriginalContainer = string.IsNullOrWhiteSpace(originalContainer)
            ? directContainer
            : originalContainer;
        var effectiveOutputContainer = string.IsNullOrWhiteSpace(outputContainer)
            ? directContainer
            : outputContainer;
        var isTranscodedPreview = !string.IsNullOrWhiteSpace(originalContainer) || !string.IsNullOrWhiteSpace(outputContainer);

        return new AudioPreviewDisplayState(
            SizeSummaryText: BuildAudioSizeSummary(payloadSize, preferredDisplaySizeInBytes, isTranscodedPreview),
            ContainerBadgeText: BuildAudioContainerBadgeText(effectiveOriginalContainer, effectiveOutputContainer, isTranscodedPreview),
            DurationBadgeText: duration,
            ChannelBadgeText: BuildAudioChannelBadgeText(channelCount),
            FrequencyBadgeText: frequency);
    }

    public SpritePreviewOverlayDisplayState? BuildSpritePreviewOverlayDisplayState(AssetPreviewDto preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var overlay = preview.ImageOverlay;
        var region = overlay?.HighlightRegion;
        if (preview.Kind != AssetPreviewKind.Image ||
            overlay is null ||
            region is null ||
            overlay.CanvasPixelWidth <= 0 ||
            overlay.CanvasPixelHeight <= 0 ||
            region.Width <= 0 ||
            region.Height <= 0)
        {
            return null;
        }

        return new SpritePreviewOverlayDisplayState(
            CanvasWidth: overlay.CanvasPixelWidth,
            CanvasHeight: overlay.CanvasPixelHeight,
            RegionX: region.X,
            RegionY: region.Y,
            RegionWidth: region.Width,
            RegionHeight: region.Height,
            PivotX: overlay.PivotPoint?.X,
            PivotY: overlay.PivotPoint?.Y,
            BorderLeft: overlay.NineSliceBorder?.Left,
            BorderTop: overlay.NineSliceBorder?.Top,
            BorderRight: overlay.NineSliceBorder?.Right,
            BorderBottom: overlay.NineSliceBorder?.Bottom,
            BadgeText: BuildSpritePreviewOverlayBadgeText(region, overlay.PivotPoint));
    }

    public ImagePreviewInfoDisplayState? BuildImagePreviewInfoDisplayState(
        IReadOnlyList<AssetPreviewInfoItemDto>? items)
    {
        if (items is not { Count: > 0 } ||
            !items.Any(static item => item.Key == AssetPreviewInfoKey.TextureFormatId))
        {
            return null;
        }

        var dimensions = FindFormattedValue(items, AssetPreviewInfoKey.ImageDimensions);
        if (string.IsNullOrWhiteSpace(dimensions))
        {
            dimensions = BuildDimensionsFromWidthAndHeight(items);
        }

        var primaryText = string.IsNullOrWhiteSpace(dimensions)
            ? string.Empty
            : _localizationService.GetFormattedString(
                ResourceKeys.InspectorTexturePreviewInfoDimensionsFormat,
                dimensions);

        var secondaryText = string.Join(
            " · ",
            BuildTextureInfoSegments(
                (ResourceKeys.InspectorTexturePreviewInfoEncodingSegmentFormat, FindFormattedValue(items, AssetPreviewInfoKey.PreviewPixelEncoding)),
                (ResourceKeys.InspectorTexturePreviewInfoOutputSegmentFormat, FindFormattedValue(items, AssetPreviewInfoKey.PreviewOutputFormat))));

        var detailText = string.Join(
            " · ",
            BuildTextureInfoSegments(
                (ResourceKeys.InspectorTexturePreviewInfoFormatIdSegmentFormat, FindFormattedValue(items, AssetPreviewInfoKey.TextureFormatId)),
                (ResourceKeys.InspectorTexturePreviewInfoMipSegmentFormat, FindFormattedValue(items, AssetPreviewInfoKey.TextureMipCount))));

        return string.IsNullOrWhiteSpace(primaryText) &&
               string.IsNullOrWhiteSpace(secondaryText) &&
               string.IsNullOrWhiteSpace(detailText)
            ? null
            : new ImagePreviewInfoDisplayState(primaryText, secondaryText, detailText);
    }

    public MeshPreviewDisplayState BuildMeshPreviewDisplayState(
        AssetPreviewDto preview,
        AssetDetailsDto? details = null)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var geometry = preview.MeshGeometry;
        if (preview.Kind != AssetPreviewKind.Mesh || geometry is null)
        {
            return new MeshPreviewDisplayState(string.Empty);
        }

        return new MeshPreviewDisplayState(
            BadgeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMeshPreviewBadgeFormat,
                geometry.SourceVertexCount.ToString(CultureInfo.CurrentCulture),
                geometry.Edges.Count.ToString(CultureInfo.CurrentCulture)),
            UvBadgeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMeshPreviewUvBadgeFormat,
                (geometry.Uv0?.Count ?? 0).ToString(CultureInfo.CurrentCulture),
                geometry.Edges.Count.ToString(CultureInfo.CurrentCulture)),
            TextureAssociationText: BuildMeshTextureAssociationText(details));
    }

    private string ResolvePreviewInfoLabel(AssetPreviewInfoKey key)
    {
        return _localizationService.GetRequiredString(
            $"{PreviewInfoLabelResourceKeyPrefix}{key}{PreviewInfoLabelResourceKeySuffix}");
    }

    private string FormatPreviewInfoValue(AssetPreviewInfoItemDto item)
    {
        return item.ValueKind switch
        {
            AssetPreviewInfoValueKind.Text => ResolveTextValue(item),
            AssetPreviewInfoValueKind.Integer => item.Int64Value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            AssetPreviewInfoValueKind.ByteSize => item.Int64Value is { } byteSize && byteSize > 0
                ? _localizedValueFormatter.FormatDetailedByteSize(byteSize)
                : string.Empty,
            AssetPreviewInfoValueKind.Decimal => item.NumberValue?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty,
            AssetPreviewInfoValueKind.DurationSeconds => item.NumberValue is { } duration
                ? _localizationService.GetFormattedString("InspectorPanelView.DurationSeconds.Format", duration.ToString("0.###", CultureInfo.CurrentCulture))
                : string.Empty,
            AssetPreviewInfoValueKind.FrequencyHertz => item.Int64Value is { } frequency
                ? _localizationService.GetFormattedString("InspectorPanelView.FrequencyHertz.Format", frequency.ToString(CultureInfo.CurrentCulture))
                : string.Empty,
            AssetPreviewInfoValueKind.BitsPerSample => item.Int64Value is { } bits
                ? _localizationService.GetFormattedString("InspectorPanelView.BitsPerSample.Format", bits.ToString(CultureInfo.CurrentCulture))
                : string.Empty,
            AssetPreviewInfoValueKind.Boolean => item.BooleanValue.HasValue
                ? _localizationService.GetRequiredString(item.BooleanValue.Value
                    ? "InspectorPanelView.Boolean.True"
                    : "InspectorPanelView.Boolean.False")
                : string.Empty,
            _ => string.Empty
        };
    }

    private string ResolveTextValue(AssetPreviewInfoItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.TextValue))
        {
            return string.Empty;
        }

        if (item.Key == AssetPreviewInfoKey.AudioPayloadSource)
        {
            return _localizationService.GetRequiredString(
                $"{PreviewInfoValueResourceKeyPrefix}AudioPayloadSource.{item.TextValue.Trim()}");
        }

        if (string.Equals(item.TextValue.Trim(), UnknownSentinelText, StringComparison.OrdinalIgnoreCase))
        {
            return _localizationService.GetRequiredString($"{PreviewInfoValueResourceKeyPrefix}Common.Unknown");
        }

        return item.TextValue.Trim();
    }

    private string BuildAudioSizeSummary(
        string payloadSize,
        long? preferredDisplaySizeInBytes,
        bool isTranscodedPreview)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(payloadSize))
        {
            segments.Add(isTranscodedPreview
                ? _localizationService.GetFormattedString("InspectorPanelView.AudioPreview.OriginalSize.Format", payloadSize)
                : _localizationService.GetFormattedString("InspectorPanelView.AudioPreview.AssetSize.Format", payloadSize));
        }

        if (isTranscodedPreview && preferredDisplaySizeInBytes is > 0)
        {
            segments.Add(_localizationService.GetFormattedString(
                "InspectorPanelView.AudioPreview.GeneratedSize.Format",
                _localizedValueFormatter.FormatDetailedByteSize(preferredDisplaySizeInBytes.Value)));
        }

        return string.Join("  ·  ", segments);
    }

    private string BuildAudioContainerBadgeText(
        string originalContainer,
        string outputContainer,
        bool isTranscodedPreview)
    {
        if (isTranscodedPreview &&
            !string.IsNullOrWhiteSpace(originalContainer) &&
            !string.IsNullOrWhiteSpace(outputContainer))
        {
            return _localizationService.GetFormattedString(
                "InspectorPanelView.AudioPreview.ContainerBadgeTranscoded.Format",
                originalContainer,
                outputContainer);
        }

        return outputContainer;
    }

    private string BuildAudioChannelBadgeText(long? channelCount)
    {
        return channelCount.HasValue
            ? _localizationService.GetFormattedString(
                "InspectorPanelView.AudioPreview.ChannelBadge.Format",
                channelCount.Value.ToString(CultureInfo.CurrentCulture))
            : string.Empty;
    }

    private string FindFormattedValue(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        AssetPreviewInfoKey key)
    {
        var item = items?.FirstOrDefault(candidate => candidate.Key == key);
        return item is null
            ? string.Empty
            : FormatPreviewInfoValue(item);
    }

    private string FindTextValue(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        AssetPreviewInfoKey key)
    {
        var item = items?.FirstOrDefault(candidate => candidate.Key == key);
        return item is null
            ? string.Empty
            : ResolveTextValue(item);
    }

    private static long? FindInt64Value(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        AssetPreviewInfoKey key)
    {
        return items?
            .FirstOrDefault(candidate => candidate.Key == key)?
            .Int64Value;
    }

    private string BuildSpritePreviewOverlayBadgeText(
        AssetPreviewImageRegionDto region,
        AssetPreviewImagePointDto? pivotPoint)
    {
        if (pivotPoint is null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpritePreviewOverlayBadgeFormat,
                FormatOverlayNumber(region.X),
                FormatOverlayNumber(region.Y),
                FormatOverlayNumber(region.Width),
                FormatOverlayNumber(region.Height));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpritePreviewOverlayBadgeWithPivotFormat,
            FormatOverlayNumber(region.X),
            FormatOverlayNumber(region.Y),
            FormatOverlayNumber(region.Width),
            FormatOverlayNumber(region.Height),
            FormatOverlayNumber(pivotPoint.X),
            FormatOverlayNumber(pivotPoint.Y));
    }

    private static string FormatOverlayNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private string BuildMeshTextureAssociationText(AssetDetailsDto? details)
    {
        var associations = CollectMeshTextureAssociations(details);
        if (associations.Count == 0)
        {
            return string.Empty;
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMeshPreviewTextureAssociationFormat,
            string.Join(", ", associations));
    }

    private IReadOnlyList<string> CollectMeshTextureAssociations(AssetDetailsDto? details)
    {
        var associations = new List<string>();
        foreach (var dependency in MeshTextureAssociationCandidateResolver.CollectDisplayCandidates(details, maxCount: 4))
        {
            associations.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat,
                dependency.Name,
                ResolveAssociationTypeText(dependency)));
        }

        return associations;
    }

    private string ResolveAssociationTypeText(AssetDependencyDto dependency)
    {
        if (!string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName))
        {
            return dependency.AssetTypeDisplayName.Trim();
        }

        return dependency.AssetType switch
        {
            AssetType.Material => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplayMaterial),
            AssetType.Texture2D => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.Sprite => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplaySprite),
            _ => dependency.AssetType.ToString()
        };
    }

    private IEnumerable<string> BuildTextureInfoSegments(
        params (string ResourceKey, string Value)[] candidates)
    {
        foreach (var (resourceKey, value) in candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            yield return _localizationService.GetFormattedString(resourceKey, value);
        }
    }

    private string BuildDimensionsFromWidthAndHeight(IReadOnlyList<AssetPreviewInfoItemDto> items)
    {
        var width = FindFormattedValue(items, AssetPreviewInfoKey.TextureWidth);
        var height = FindFormattedValue(items, AssetPreviewInfoKey.TextureHeight);
        return string.IsNullOrWhiteSpace(width) || string.IsNullOrWhiteSpace(height)
            ? string.Empty
            : $"{width} x {height}";
    }
}

/// <summary>
/// 表示音频预览头部面板所需的已格式化展示状态。
/// </summary>
public sealed record AudioPreviewDisplayState(
    string SizeSummaryText,
    string ContainerBadgeText,
    string DurationBadgeText,
    string ChannelBadgeText,
    string FrequencyBadgeText);

/// <summary>
/// 表示 Texture2D 图片预览右下角信息覆盖层需要的已格式化展示状态。
/// </summary>
public sealed record ImagePreviewInfoDisplayState(
    string PrimaryText,
    string SecondaryText,
    string DetailText);

/// <summary>
/// 表示 Sprite 实际裁切结果预览所需的展示状态。
/// </summary>
public sealed record SpriteFocusPreviewDisplayState(
    Microsoft.UI.Xaml.Media.ImageSource ImageSource,
    string TitleText,
    string SizeText,
    int PixelWidth = 1,
    int PixelHeight = 1);

/// <summary>
/// 表示 Sprite 图片预览叠加裁切框所需的展示状态。
/// </summary>
public sealed record SpritePreviewOverlayDisplayState(
    double CanvasWidth,
    double CanvasHeight,
    double RegionX,
    double RegionY,
    double RegionWidth,
    double RegionHeight,
    double? PivotX,
    double? PivotY,
    double? BorderLeft,
    double? BorderTop,
    double? BorderRight,
    double? BorderBottom,
    string BadgeText);

/// <summary>
/// 表示 Mesh 线框预览需要的已格式化展示状态。
/// </summary>
public sealed record MeshPreviewDisplayState(
    string BadgeText,
    string UvBadgeText = "",
    string TextureAssociationText = "");
