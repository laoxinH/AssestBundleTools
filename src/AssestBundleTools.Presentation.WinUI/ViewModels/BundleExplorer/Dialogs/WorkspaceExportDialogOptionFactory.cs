using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

internal sealed class WorkspaceExportDialogOptionFactory
{
    private readonly ILocalizationService _localizationService;

    public WorkspaceExportDialogOptionFactory(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public IEnumerable<WorkspaceExportBundleOptionViewModel> CreateBundleOptions(
        WorkspaceSavePreviewDto? savePreview,
        WorkspaceChangeSummaryDto? changeSummary)
    {
        if (savePreview is null)
        {
            yield break;
        }

        var bundleLastChangedAtLookup = BuildBundleLastChangedAtLookup(changeSummary);
        var defaultSelectedBundleIds = BuildRecentBundleSelectionLookup(savePreview.BundlePlans, bundleLastChangedAtLookup);
        foreach (var plan in savePreview.BundlePlans)
        {
            var lastChangedAtUtc = bundleLastChangedAtLookup.TryGetValue(plan.BundleId, out var resolvedLastChangedAtUtc)
                ? resolvedLastChangedAtUtc
                : (DateTimeOffset?)null;
            yield return new WorkspaceExportBundleOptionViewModel(
                plan.BundleId,
                plan.BundleDisplayName,
                plan.OutputFilePath,
                _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceExportDialogBundleSummaryFormat,
                    plan.ChangeCount,
                    plan.ModifiedAssetCount,
                    ResolveOriginalCompressionDisplayName(plan.OriginalCompression),
                    plan.WorkspaceReplacementCount,
                    plan.FileReplacementCount),
                plan.ChangeCount,
                plan.ModifiedAssetCount,
                plan.ExtractedAssetCount,
                plan.WorkspaceReplacementCount,
                plan.FileReplacementCount,
                lastChangedAtUtc,
                defaultSelectedBundleIds.Contains(plan.BundleId));
        }
    }

    public IEnumerable<WorkspaceExportBundleSortOptionViewModel> CreateBundleSortOptions()
    {
        yield return new WorkspaceExportBundleSortOptionViewModel(
            "time",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleSortByTime));
        yield return new WorkspaceExportBundleSortOptionViewModel(
            "name",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleSortByName));
    }

    public IEnumerable<WorkspaceExportResourceProcessorOptionViewModel> CreateResourceProcessorOptions(
        IReadOnlyList<ResourceProcessorCatalogEntryDto> catalogEntries)
    {
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "texture2d",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2DDescription),
            CreateProcessorOptions("texture2d", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "audio",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudioDescription),
            CreateProcessorOptions("audio", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "atlas",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlasDescription),
            CreateProcessorOptions("atlas", catalogEntries));
        yield return new WorkspaceExportResourceProcessorOptionViewModel(
            "skel",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkelDescription),
            CreateProcessorOptions("skel", catalogEntries));
    }

    public IEnumerable<WorkspaceExportCompressionOptionViewModel> CreateCompressionOptions()
    {
        yield return new WorkspaceExportCompressionOptionViewModel(
            WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionPreserveOriginal),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionPreserveOriginalDescription));
        yield return new WorkspaceExportCompressionOptionViewModel(
            WorkspaceBundleExportCompressionModeDto.Lzma,
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionLzma),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionLzmaDescription));
        yield return new WorkspaceExportCompressionOptionViewModel(
            WorkspaceBundleExportCompressionModeDto.Lz4,
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionLz4),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionLz4Description));
        yield return new WorkspaceExportCompressionOptionViewModel(
            WorkspaceBundleExportCompressionModeDto.FastLz4,
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionFastLz4),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionFastLz4Description));
        yield return new WorkspaceExportCompressionOptionViewModel(
            WorkspaceBundleExportCompressionModeDto.Uncompressed,
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionUncompressed),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionUncompressedDescription));
    }

    public WorkspaceExportProcessorSelectionDto CreateProcessorSelection(
        WorkspaceExportResourceProcessorOptionViewModel processor)
    {
        return new WorkspaceExportProcessorSelectionDto(
            NormalizeResourceKind(processor.Id),
            ResolveSelectedProcessorId(processor));
    }

    public WorkspaceExportProcessorSettingSnapshotDto CreateProcessorSettingSnapshot(
        WorkspaceExportResourceProcessorOptionViewModel processor)
    {
        return new WorkspaceExportProcessorSettingSnapshotDto(
            NormalizeResourceKind(processor.Id),
            processor.IsEnabled,
            ResolveSelectedProcessorId(processor));
    }

    private IEnumerable<WorkspaceExportProcessorOptionViewModel> CreateProcessorOptions(
        string resourceKind,
        IReadOnlyList<ResourceProcessorCatalogEntryDto> catalogEntries)
    {
        yield return new WorkspaceExportProcessorOptionViewModel(
            $"{resourceKind}:none",
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNone),
            _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogProcessorNoneDescription));

        foreach (var entry in catalogEntries.Where(entry => IsCompatibleResourceProcessor(entry, resourceKind)))
        {
            yield return new WorkspaceExportProcessorOptionViewModel(
                entry.Id,
                ResolveProcessorDisplayName(entry, resourceKind),
                ResolveProcessorDescription(entry, resourceKind));
        }
    }

    private static bool IsCompatibleResourceProcessor(
        ResourceProcessorCatalogEntryDto entry,
        string resourceProcessorId)
    {
        return entry.Status == ResourceProcessorStatus.Available &&
               entry.ResourceKinds.Any(resourceKind =>
                   string.Equals(
                       NormalizeResourceKind(resourceKind),
                       NormalizeResourceKind(resourceProcessorId),
                       StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveProcessorDisplayName(
        ResourceProcessorCatalogEntryDto entry,
        string resourceKind)
    {
        return entry.IsBuiltIn
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat,
                ResolveResourceKindDisplayName(resourceKind))
            : string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.Id
                : entry.DisplayName;
    }

    private string ResolveProcessorDescription(
        ResourceProcessorCatalogEntryDto entry,
        string resourceKind)
    {
        return string.IsNullOrWhiteSpace(entry.Description)
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDescriptionFormat,
                ResolveResourceKindDisplayName(resourceKind))
            : entry.Description;
    }

    private string ResolveResourceKindDisplayName(string resourceKind)
    {
        return NormalizeResourceKind(resourceKind) switch
        {
            "texture2d" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
            "audio" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
            "atlas" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
            "skel" => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
            _ => resourceKind
        };
    }

    private string ResolveOriginalCompressionDisplayName(string? originalCompression)
    {
        if (string.IsNullOrWhiteSpace(originalCompression))
        {
            return _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogCompressionUnknown);
        }

        return originalCompression.Trim();
    }

    private static string NormalizeResourceKind(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string? ResolveSelectedProcessorId(WorkspaceExportResourceProcessorOptionViewModel processor)
    {
        var selectedProcessorId = processor.IsEnabled
            ? processor.SelectedProcessor?.Id
            : null;
        if (selectedProcessorId is not null &&
            selectedProcessorId.EndsWith(":none", StringComparison.OrdinalIgnoreCase))
        {
            selectedProcessorId = null;
        }

        return selectedProcessorId;
    }

    private static IReadOnlySet<BundleId> BuildRecentBundleSelectionLookup(
        IReadOnlyList<WorkspaceSaveBundlePlanDto> bundlePlans,
        IReadOnlyDictionary<BundleId, DateTimeOffset> bundleLastChangedAtLookup)
    {
        if (bundlePlans.Count == 0)
        {
            return new HashSet<BundleId>();
        }

        if (bundleLastChangedAtLookup.Count == 0)
        {
            return bundlePlans
                .Select(static plan => plan.BundleId)
                .ToHashSet();
        }

        var latestChangedAt = bundleLastChangedAtLookup.Values.Max();
        var recentCutoff = latestChangedAt.AddHours(-24);
        return bundlePlans
            .Where(plan =>
                bundleLastChangedAtLookup.TryGetValue(plan.BundleId, out var lastChangedAtUtc) &&
                lastChangedAtUtc >= recentCutoff)
            .Select(static plan => plan.BundleId)
            .ToHashSet();
    }

    private static IReadOnlyDictionary<BundleId, DateTimeOffset> BuildBundleLastChangedAtLookup(
        WorkspaceChangeSummaryDto? changeSummary)
    {
        if (changeSummary is null)
        {
            return new Dictionary<BundleId, DateTimeOffset>();
        }

        return changeSummary.Items
            .Where(static item => item.LastChangedAtUtc is not null)
            .GroupBy(static item => item.BundleId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Max(static item => item.LastChangedAtUtc!.Value));
    }
}
