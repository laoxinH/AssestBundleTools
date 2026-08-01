using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

/// <summary>
/// 维护插件管理页的目录、筛选与详情状态。
/// </summary>
public sealed partial class PluginManagementViewModel : ObservableObject
{
    private readonly IAssetBundleDecryptorCatalogService _assetBundleDecryptorCatalogService;
    private readonly IResourceProcessorCatalogService _resourceProcessorCatalogService;
    private readonly IPostExportPluginCatalogService _postExportPluginCatalogService;
    private readonly IPluginPackageImportService _pluginPackageImportService;
    private readonly Action _reloadAssetBundleDecryptorRegistry;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly PluginManagementPluginPresenter _pluginPresenter;
    private readonly Dictionary<string, AssetBundleDecryptorCatalogEntryDto> _decryptorEntriesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceProcessorCatalogEntryDto> _resourceProcessorEntriesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PostExportPluginCatalogEntryDto> _postExportPluginEntriesById = new(StringComparer.OrdinalIgnoreCase);
    private string _searchText = string.Empty;
    private string _selectedDiagnosticText = string.Empty;
    private bool _isLoading;
    private PluginManagementFilterOptionViewModel? _selectedTypeFilter;
    private PluginManagementFilterOptionViewModel? _selectedStatusFilter;
    private PluginManagementPluginItemViewModel? _selectedPlugin;

    public PluginManagementViewModel(
        IAssetBundleDecryptorCatalogService assetBundleDecryptorCatalogService,
        IResourceProcessorCatalogService resourceProcessorCatalogService,
        IPostExportPluginCatalogService postExportPluginCatalogService,
        IPluginPackageImportService pluginPackageImportService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        Action reloadAssetBundleDecryptorRegistry,
        IAppNotificationService appNotificationService,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IPathNavigationService pathNavigationService)
    {
        _assetBundleDecryptorCatalogService = assetBundleDecryptorCatalogService ?? throw new ArgumentNullException(nameof(assetBundleDecryptorCatalogService));
        _resourceProcessorCatalogService = resourceProcessorCatalogService ?? throw new ArgumentNullException(nameof(resourceProcessorCatalogService));
        _postExportPluginCatalogService = postExportPluginCatalogService ?? throw new ArgumentNullException(nameof(postExportPluginCatalogService));
        _pluginPackageImportService = pluginPackageImportService ?? throw new ArgumentNullException(nameof(pluginPackageImportService));
        _reloadAssetBundleDecryptorRegistry = reloadAssetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(reloadAssetBundleDecryptorRegistry));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _pluginPresenter = new PluginManagementPluginPresenter(
            _localizationService,
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));

        TypeFilters = new ObservableCollection<PluginManagementFilterOptionViewModel>(CreateTypeFilters());
        StatusFilters = new ObservableCollection<PluginManagementFilterOptionViewModel>(CreateStatusFilters());
        _selectedTypeFilter = TypeFilters.FirstOrDefault();
        _selectedStatusFilter = StatusFilters.FirstOrDefault();
        _selectedDiagnosticText = GetString(ResourceKeys.PluginManagementDiagnosticNotRun);
    }

    public ObservableCollection<PluginManagementPluginItemViewModel> Plugins { get; } = new();

    public ObservableCollection<PluginManagementPluginItemViewModel> VisiblePlugins { get; } = new();

    public ObservableCollection<PluginManagementPluginItemViewModel> VisibleDecryptorPlugins { get; } = new();

    public ObservableCollection<PluginManagementPluginItemViewModel> VisibleResourceProcessorPlugins { get; } = new();

    public ObservableCollection<PluginManagementPluginItemViewModel> VisiblePostExportPlugins { get; } = new();

    public ObservableCollection<PluginManagementFilterOptionViewModel> TypeFilters { get; }

    public ObservableCollection<PluginManagementFilterOptionViewModel> StatusFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshVisiblePlugins();
            }
        }
    }

    public PluginManagementFilterOptionViewModel? SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                RefreshVisiblePlugins();
            }
        }
    }

    public PluginManagementFilterOptionViewModel? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                RefreshVisiblePlugins();
            }
        }
    }

    public PluginManagementPluginItemViewModel? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (SetProperty(ref _selectedPlugin, value))
            {
                SelectedDiagnosticText = value?.DiagnosticText ?? GetString(ResourceKeys.PluginManagementDiagnosticNoSelection);
                NotifySelectionState();
            }
        }
    }

    public string SelectedDiagnosticText
    {
        get => _selectedDiagnosticText;
        private set => SetProperty(ref _selectedDiagnosticText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasVisiblePlugins => VisiblePlugins.Count > 0;

    public bool HasVisibleDecryptorPlugins => VisibleDecryptorPlugins.Count > 0;

    public bool HasVisibleResourceProcessorPlugins => VisibleResourceProcessorPlugins.Count > 0;

    public bool HasVisiblePostExportPlugins => VisiblePostExportPlugins.Count > 0;

    public string VisiblePluginCountText => VisiblePlugins.Count.ToString(CultureInfo.CurrentCulture);

    public string VisibleDecryptorPluginCountText => VisibleDecryptorPlugins.Count.ToString(CultureInfo.CurrentCulture);

    public string VisibleResourceProcessorPluginCountText => VisibleResourceProcessorPlugins.Count.ToString(CultureInfo.CurrentCulture);

    public string VisiblePostExportPluginCountText => VisiblePostExportPlugins.Count.ToString(CultureInfo.CurrentCulture);

    public bool HasSelectedPlugin => SelectedPlugin is not null;

    public string SelectedPluginIconGlyph => SelectedPlugin?.IsDecryptor == true
        ? "\uE8EB"
        : SelectedPlugin?.IsPostExportPlugin == true
            ? "\uE74C"
            : "\uE6E9";

    public bool CanEditSelectedPlugin => SelectedPlugin?.CanEdit == true;

    public bool CanDeleteSelectedPlugin => SelectedPlugin?.CanDelete == true;

    public bool CanOpenSelectedPluginLocation =>
        !string.IsNullOrWhiteSpace(SelectedPlugin?.DecryptorEntry?.ManifestFilePath) ||
        !string.IsNullOrWhiteSpace(SelectedPlugin?.ResourceProcessorEntry?.ManifestFilePath) ||
        !string.IsNullOrWhiteSpace(SelectedPlugin?.PostExportPluginEntry?.ManifestFilePath) ||
        !string.IsNullOrWhiteSpace(SelectedPlugin?.DecryptorEntry?.Definition.ExecutablePath) ||
        !string.IsNullOrWhiteSpace(SelectedPlugin?.ResourceProcessorEntry?.ExecutablePath) ||
        !string.IsNullOrWhiteSpace(SelectedPlugin?.PostExportPluginEntry?.ExecutablePath);

    public Visibility EmptyStateVisibility => HasVisiblePlugins ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DecryptorGroupVisibility => HasVisibleDecryptorPlugins ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ResourceProcessorGroupVisibility => HasVisibleResourceProcessorPlugins ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PostExportGroupVisibility => HasVisiblePostExportPlugins ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DetailsVisibility => HasSelectedPlugin ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DetailsEmptyVisibility => HasSelectedPlugin ? Visibility.Collapsed : Visibility.Visible;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Plugins.Count > 0)
        {
            return;
        }

        await RefreshCatalogAsync(cancellationToken);
    }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var decryptorResult = await _assetBundleDecryptorCatalogService.GetEntriesAsync(cancellationToken);
            if (!TryGetValue(decryptorResult, out var decryptorEntries, ResourceKeys.PluginManagementDecryptorLoadFailedMessage))
            {
                return;
            }

            var processorResult = await _resourceProcessorCatalogService.GetEntriesAsync(cancellationToken);
            if (!TryGetValue(processorResult, out var processorEntries, ResourceKeys.PluginManagementProcessorLoadFailedMessage))
            {
                return;
            }

            var postExportResult = await _postExportPluginCatalogService.GetEntriesAsync(cancellationToken);
            if (!TryGetValue(postExportResult, out var postExportEntries, ResourceKeys.PluginManagementPostExportLoadFailedMessage))
            {
                return;
            }

            ReplacePlugins(
                decryptorEntries ?? Array.Empty<AssetBundleDecryptorCatalogEntryDto>(),
                processorEntries ?? Array.Empty<ResourceProcessorCatalogEntryDto>(),
                postExportEntries ?? Array.Empty<PostExportPluginCatalogEntryDto>());
        }
        finally
        {
            IsLoading = false;
        }
    }

    public bool TryGetDecryptorEntry(string decryptorId, out AssetBundleDecryptorCatalogEntryDto? entry)
    {
        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            entry = null;
            return false;
        }

        return _decryptorEntriesById.TryGetValue(decryptorId.Trim(), out entry);
    }

    public bool TryGetResourceProcessorEntry(string processorId, out ResourceProcessorCatalogEntryDto? entry)
    {
        if (string.IsNullOrWhiteSpace(processorId))
        {
            entry = null;
            return false;
        }

        return _resourceProcessorEntriesById.TryGetValue(processorId.Trim(), out entry);
    }

    public async Task<bool> SaveDecryptorAsync(
        UpsertAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _assetBundleDecryptorCatalogService.UpsertAsync(command, cancellationToken);
        if (!TryGetValue(result, out var savedEntry, ResourceKeys.PluginManagementDecryptorSaveFailedMessage) ||
            savedEntry is null)
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementDecryptorSaveSucceededMessageFormat,
                _pluginPresenter.GetDecryptorDisplayName(savedEntry.Definition.Id)),
            AppNotificationSeverity.Success);
        return true;
    }

    public async Task<bool> DeleteDecryptorAsync(
        string decryptorId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetDecryptorEntry(decryptorId, out var entry) ||
            entry is null)
        {
            return false;
        }

        var result = await _assetBundleDecryptorCatalogService.DeleteAsync(
            new DeleteAssetBundleDecryptorCommand(decryptorId),
            cancellationToken);
        if (!TryGetValue(result, ResourceKeys.PluginManagementDecryptorDeleteFailedMessage))
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementDecryptorDeleteSucceededMessageFormat,
                _pluginPresenter.GetDecryptorDisplayName(entry.Definition.Id)),
            AppNotificationSeverity.Success);
        return true;
    }

    public async Task<bool> SaveResourceProcessorAsync(
        UpsertResourceProcessorCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _resourceProcessorCatalogService.UpsertAsync(command, cancellationToken);
        if (!TryGetValue(result, out var savedEntry, ResourceKeys.PluginManagementProcessorSaveFailedMessage) ||
            savedEntry is null)
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementProcessorSaveSucceededMessageFormat,
                ResolveProcessorDisplayName(savedEntry)),
            AppNotificationSeverity.Success);
        return true;
    }

    public async Task<bool> DeleteResourceProcessorAsync(
        string processorId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetResourceProcessorEntry(processorId, out var entry) ||
            entry is null)
        {
            return false;
        }

        var result = await _resourceProcessorCatalogService.DeleteAsync(
            new DeleteResourceProcessorCommand(processorId),
            cancellationToken);
        if (!TryGetValue(result, ResourceKeys.PluginManagementProcessorDeleteFailedMessage))
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementProcessorDeleteSucceededMessageFormat,
                ResolveProcessorDisplayName(entry)),
            AppNotificationSeverity.Success);
        return true;
    }

    public void ValidateSelectedPlugin()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        SelectedDiagnosticText = _localizationService.GetFormattedString(
            ResourceKeys.PluginManagementValidationCompletedMessageFormat,
            SelectedPlugin.DisplayName);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            SelectedDiagnosticText,
            AppNotificationSeverity.Success);
    }

    public bool TryOpenSelectedPluginLocation()
    {
        var targetPath =
            SelectedPlugin?.DecryptorEntry?.ManifestFilePath ??
            SelectedPlugin?.ResourceProcessorEntry?.ManifestFilePath ??
            SelectedPlugin?.PostExportPluginEntry?.ManifestFilePath ??
            SelectedPlugin?.DecryptorEntry?.Definition.ExecutablePath ??
            SelectedPlugin?.ResourceProcessorEntry?.ExecutablePath ??
            SelectedPlugin?.PostExportPluginEntry?.ExecutablePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        return _pathNavigationService.TryOpenContainingFolder(targetPath);
    }

    public string BuildSelectedDiagnosticsText()
    {
        if (SelectedPlugin is null)
        {
            return GetString(ResourceKeys.PluginManagementDiagnosticNoSelection);
        }

        var lines = new List<string>
        {
            SelectedPlugin.DisplayName,
            $"{GetString(ResourceKeys.PluginManagementDetailIdLabel)}: {SelectedPlugin.Id}",
            $"{GetString(ResourceKeys.PluginManagementDetailTypeLabel)}: {SelectedPlugin.TypeText}",
            $"{GetString(ResourceKeys.PluginManagementDetailStatusLabel)}: {SelectedPlugin.SecondaryBadgeText}",
            $"{GetString(ResourceKeys.PluginManagementDiagnosticSectionTitle)}: {SelectedDiagnosticText}"
        };

        foreach (var detail in SelectedPlugin.DetailItems)
        {
            lines.Add($"{detail.Label}: {detail.Value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void ReplacePlugins(
        IReadOnlyList<AssetBundleDecryptorCatalogEntryDto> decryptorEntries,
        IReadOnlyList<ResourceProcessorCatalogEntryDto> processorEntries,
        IReadOnlyList<PostExportPluginCatalogEntryDto> postExportEntries)
    {
        var selectedId = SelectedPlugin?.Id;
        _decryptorEntriesById.Clear();
        _resourceProcessorEntriesById.Clear();
        _postExportPluginEntriesById.Clear();
        Plugins.Clear();

        Plugins.Add(_pluginPresenter.BuildAutoDetectDecryptor());
        foreach (var entry in decryptorEntries)
        {
            _decryptorEntriesById[entry.Definition.Id] = entry;
            Plugins.Add(_pluginPresenter.BuildDecryptor(entry));
        }

        foreach (var entry in processorEntries)
        {
            _resourceProcessorEntriesById[entry.Id] = entry;
            Plugins.Add(_pluginPresenter.BuildResourceProcessor(entry));
        }

        foreach (var entry in postExportEntries)
        {
            _postExportPluginEntriesById[entry.Id] = entry;
            Plugins.Add(_pluginPresenter.BuildPostExportPlugin(entry));
        }

        RefreshVisiblePlugins();
        SelectedPlugin = VisiblePlugins.FirstOrDefault(plugin =>
                             string.Equals(plugin.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                         ?? VisiblePlugins.FirstOrDefault();
    }

    private void RefreshVisiblePlugins()
    {
        var query = SearchText.Trim();
        var typeFilter = SelectedTypeFilter?.Id ?? PluginManagementFilterKeys.All;
        var statusFilter = SelectedStatusFilter?.Id ?? PluginManagementFilterKeys.All;

        var candidates = Plugins
            .Where(plugin => plugin.MatchesTypeFilter(typeFilter) && plugin.MatchesStatusFilter(statusFilter));

        if (!string.IsNullOrWhiteSpace(query))
        {
            candidates = candidates.Where(plugin =>
                plugin.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var selectedId = SelectedPlugin?.Id;
        VisiblePlugins.Clear();
        VisibleDecryptorPlugins.Clear();
        VisibleResourceProcessorPlugins.Clear();
        VisiblePostExportPlugins.Clear();
        foreach (var plugin in candidates
                     .OrderBy(static plugin => plugin.IsDecryptor ? 0 : plugin.IsPostExportPlugin ? 2 : 1)
                     .ThenBy(static plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            VisiblePlugins.Add(plugin);
            if (plugin.IsDecryptor)
            {
                VisibleDecryptorPlugins.Add(plugin);
            }
            else if (plugin.IsPostExportPlugin)
            {
                VisiblePostExportPlugins.Add(plugin);
            }
            else
            {
                VisibleResourceProcessorPlugins.Add(plugin);
            }
        }

        if (VisiblePlugins.Count == 0)
        {
            SelectedPlugin = null;
        }
        else if (SelectedPlugin is null ||
                 !VisiblePlugins.Any(plugin => string.Equals(plugin.Id, selectedId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedPlugin = VisiblePlugins.FirstOrDefault();
        }

        NotifyListState();
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private string ResolveProcessorDisplayName(ResourceProcessorCatalogEntryDto entry)
    {
        var resourceKindText = entry.ResourceKinds.Count == 0
            ? GetString(ResourceKeys.PluginManagementResourceKindUnknown)
            : string.Join(
                ", ",
                entry.ResourceKinds.Select(_pluginPresenter.GetResourceKindDisplayName));

        return entry.IsBuiltIn
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat,
                resourceKindText)
            : string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.Id
                : entry.DisplayName;
    }

    private bool TryGetValue<T>(
        Result<T> result,
        out T? value,
        string fallbackMessageResourceKey)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;
        if (result.IsCancelled)
        {
            return false;
        }

        ReportErrors(result.Errors, fallbackMessageResourceKey);
        return false;
    }

    private bool TryGetValue(
        Result result,
        string fallbackMessageResourceKey)
    {
        if (result.IsSuccess)
        {
            return true;
        }

        if (result.IsCancelled)
        {
            return false;
        }

        ReportErrors(result.Errors, fallbackMessageResourceKey);
        return false;
    }

    private void ReportErrors(IReadOnlyList<AppError> errors, string fallbackMessageResourceKey)
    {
        var message = ResolveErrorMessage(errors, fallbackMessageResourceKey);
        _appLogService.AppendError(message);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            message,
            AppNotificationSeverity.Error);
    }

    private string ResolveErrorMessage(IReadOnlyList<AppError> errors, string fallbackMessageResourceKey)
    {
        if (errors.Count == 0)
        {
            return GetString(fallbackMessageResourceKey);
        }

        var firstError = errors[0];
        return firstError.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                firstError.MessageToken,
                firstError.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : _localizationService.GetRequiredString(firstError.MessageToken);
    }
}
