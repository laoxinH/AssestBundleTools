using System.Collections.ObjectModel;
using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载 Spine 组编辑态中的组成文件替换草稿和会话内撤销历史。
/// </summary>
public sealed class SpineAssetGroupEditDraftViewModel : ObservableObject
{
    private const int HistoryLimit = 20;
    private readonly ILocalizationService _localizationService;
    private readonly List<SpineEditDraftSnapshot> _history = [];
    private int _historyIndex = -1;

    public SpineAssetGroupEditDraftViewModel(
        SpineAssetGroupDto group,
        ILocalizationService localizationService)
    {
        Group = group ?? throw new ArgumentNullException(nameof(group));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        BatchId = CreateBatchId();
        RelationId = CreateRelationId();
        foreach (var part in EnumerateParts(group))
        {
            Parts.Add(CreatePart(part));
        }

        PushHistorySnapshot();
    }

    public SpineAssetGroupDto Group { get; }

    public ObservableCollection<SpineAssetGroupEditPartViewModel> Parts { get; } = new();

    public string BatchId { get; }

    public string RelationId { get; }

    public Visibility Visibility => Visibility.Visible;

    public string TitleText => GetString(ResourceKeys.InspectorSpineEditTitle);

    public string DescriptionText => GetString(ResourceKeys.InspectorSpineEditDescription);

    public string BatchMatchButtonText => GetString(ResourceKeys.InspectorSpineEditBatchMatchButton);

    public string ChangedBadgeText => GetString(ResourceKeys.InspectorSpineEditChangedBadge);

    public string EmptyText => GetString(ResourceKeys.InspectorSpineEditNoPartsText);

    public Visibility EmptyVisibility => Parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PartsVisibility => Parts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool HasChanges => Parts.Any(static part => part.IsChanged);

    public bool CanUndo => _historyIndex > 0;

    public bool CanRedo => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    public IReadOnlyList<SpineAssetGroupEditPartViewModel> ChangedParts =>
        Parts.Where(static part => part.IsChanged).ToArray();

    public bool ApplyPartSource(SpineAssetGroupEditPartViewModel part, string? sourceFilePath)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (!Parts.Contains(part))
        {
            return false;
        }

        var previousSourceFilePath = part.SelectedSourceFilePath;
        part.SetSelectedSourceFilePath(sourceFilePath);
        if (string.Equals(previousSourceFilePath, part.SelectedSourceFilePath, StringComparison.Ordinal))
        {
            return false;
        }

        PushHistorySnapshot();
        NotifyStateChanged();
        return true;
    }

    public bool ApplyDirectoryMatches(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Directory.Exists(directoryPath))
        {
            return false;
        }

        var files = Directory.EnumerateFiles(directoryPath.Trim())
            .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(static path => new SpineEditDirectoryCandidate(
                path,
                EnumerateMatchKeys(Path.GetFileName(path)).ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        var changed = false;
        foreach (var part in Parts)
        {
            var partKeys = EnumeratePartMatchKeys(part).ToArray();
            var matchedFile = files.FirstOrDefault(candidate =>
                IsCandidateCompatible(part, candidate.FilePath) &&
                partKeys.Any(candidate.MatchKeys.Contains));
            if (matchedFile is not null)
            {
                part.SetSelectedSourceFilePath(matchedFile.FilePath);
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        PushHistorySnapshot();
        NotifyStateChanged();
        return true;
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        _historyIndex--;
        RestoreSnapshot(_history[_historyIndex]);
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        _historyIndex++;
        RestoreSnapshot(_history[_historyIndex]);
    }

    public void MarkSaved()
    {
        foreach (var part in Parts)
        {
            part.SetSelectedSourceFilePath(null);
        }

        _history.Clear();
        _historyIndex = -1;
        PushHistorySnapshot();
        NotifyStateChanged();
    }

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(ChangedParts));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(PartsVisibility));
    }

    private SpineAssetGroupEditPartViewModel CreatePart(SpineAssetGroupPartDto part) =>
        new(
            part,
            ResolvePartRoleText(part.Role),
            GetString(ResourceKeys.InspectorSpineEditSelectFileButton),
            GetString(ResourceKeys.InspectorSpineEditNoDraftSourceText),
            GetString(ResourceKeys.InspectorSpineEditSelectedStatusText),
            GetString(ResourceKeys.InspectorSpineEditUnchangedStatusText));

    private void PushHistorySnapshot()
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        var snapshot = CreateSnapshot();
        if (_history.Count > 0 && _history[^1].Equals(snapshot))
        {
            return;
        }

        _history.Add(snapshot);
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(0);
        }

        _historyIndex = _history.Count - 1;
    }

    private SpineEditDraftSnapshot CreateSnapshot() =>
        new(Parts.ToDictionary(
            static part => part.AssetId,
            static part => part.SelectedSourceFilePath,
            StringComparer.Ordinal));

    private void RestoreSnapshot(SpineEditDraftSnapshot snapshot)
    {
        foreach (var part in Parts)
        {
            snapshot.SourceFilePaths.TryGetValue(part.AssetId, out var sourceFilePath);
            part.SetSelectedSourceFilePath(sourceFilePath);
        }

        NotifyStateChanged();
    }

    private string ResolvePartRoleText(SpineAssetGroupPartRole role)
    {
        return role switch
        {
            SpineAssetGroupPartRole.Skeleton => GetString(ResourceKeys.InspectorSpineGroupPartRoleSkeleton),
            SpineAssetGroupPartRole.Atlas => GetString(ResourceKeys.InspectorSpineGroupPartRoleAtlas),
            SpineAssetGroupPartRole.TexturePage => GetString(ResourceKeys.InspectorSpineGroupPartRoleTexturePage),
            _ => role.ToString()
        };
    }

    private static IEnumerable<SpineAssetGroupPartDto> EnumerateParts(SpineAssetGroupDto group)
    {
        foreach (var part in group.SkeletonAssets)
        {
            yield return part;
        }

        foreach (var part in group.AtlasAssets)
        {
            yield return part;
        }

        foreach (var part in group.TexturePageAssets)
        {
            yield return part;
        }
    }

    private static string NormalizeMatchKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private static IEnumerable<string> EnumeratePartMatchKeys(SpineAssetGroupEditPartViewModel part)
    {
        foreach (var key in EnumerateMatchKeys(part.DisplayName))
        {
            yield return key;
        }

        foreach (var key in EnumerateMatchKeys(Path.GetFileName(NormalizePathSeparators(part.Part.AssetPath))))
        {
            yield return key;
        }
    }

    private static IEnumerable<string> EnumerateMatchKeys(string? value)
    {
        var current = NormalizeMatchKey(value);
        if (string.IsNullOrEmpty(current))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            yield return current;

            var withoutExtension = NormalizeMatchKey(Path.GetFileNameWithoutExtension(current));
            if (string.Equals(current, withoutExtension, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            current = withoutExtension;
        }
    }

    private static bool IsCandidateCompatible(SpineAssetGroupEditPartViewModel part, string sourceFilePath) =>
        part.Part.Role switch
        {
            SpineAssetGroupPartRole.Skeleton => HasExtension(sourceFilePath, ".skel") ||
                                                HasExtension(sourceFilePath, ".json") ||
                                                HasExtension(sourceFilePath, ".bytes"),
            SpineAssetGroupPartRole.Atlas => HasExtension(sourceFilePath, ".atlas") ||
                                             HasExtension(sourceFilePath, ".txt"),
            SpineAssetGroupPartRole.TexturePage => ReplacementFileTypeCatalog.IsFilePathCompatible(
                AssetType.Texture2D,
                sourceFilePath),
            _ => ReplacementFileTypeCatalog.IsFilePathCompatible(part.AssetType, sourceFilePath)
        };

    private static bool HasExtension(string sourceFilePath, string extension) =>
        string.Equals(Path.GetExtension(sourceFilePath), extension, StringComparison.OrdinalIgnoreCase);

    private static string NormalizePathSeparators(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('/', Path.DirectorySeparatorChar);

    private static string CreateBatchId() => string.Concat("spine_batch_", Guid.NewGuid().ToString("N"));

    private static string CreateRelationId() => string.Concat("spine_relation_", Guid.NewGuid().ToString("N"));

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private sealed record SpineEditDraftSnapshot(IReadOnlyDictionary<string, string?> SourceFilePaths);

    private sealed record SpineEditDirectoryCandidate(string FilePath, IReadOnlySet<string> MatchKeys);
}
