using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示 Spine 编辑态中一个可替换组成资产的草稿行。
/// </summary>
public sealed class SpineAssetGroupEditPartViewModel : ObservableObject
{
    private string? _selectedSourceFilePath;
    private string? _appliedSourceFilePath;

    public SpineAssetGroupEditPartViewModel(
        SpineAssetGroupPartDto part,
        string roleText,
        string selectButtonText,
        string notSelectedText,
        string selectedStatusText,
        string unchangedStatusText,
        string? appliedSourceFilePath = null)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
        RoleText = roleText ?? throw new ArgumentNullException(nameof(roleText));
        SelectButtonText = selectButtonText ?? throw new ArgumentNullException(nameof(selectButtonText));
        NotSelectedText = notSelectedText ?? throw new ArgumentNullException(nameof(notSelectedText));
        SelectedStatusText = selectedStatusText ?? throw new ArgumentNullException(nameof(selectedStatusText));
        UnchangedStatusText = unchangedStatusText ?? throw new ArgumentNullException(nameof(unchangedStatusText));
        _appliedSourceFilePath = NormalizeFilePath(appliedSourceFilePath);
    }

    public SpineAssetGroupPartDto Part { get; }

    public BundleId BundleId => Part.BundleId;

    public AssetType AssetType => Part.AssetType;

    public string AssetId => Part.AssetId.Value;

    public string RoleText { get; }

    public string SelectButtonText { get; }

    public string NotSelectedText { get; }

    public string SelectedStatusText { get; }

    public string UnchangedStatusText { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Part.Name)
        ? Part.AssetId.Value
        : Part.Name.Trim();

    public string MetadataText => string.IsNullOrWhiteSpace(Part.AssetPath)
        ? Part.AssetId.Value
        : Part.AssetPath.Trim().Replace('\\', '/');

    public string? SelectedSourceFilePath
    {
        get => _selectedSourceFilePath;
        private set
        {
            if (SetProperty(ref _selectedSourceFilePath, NormalizeFilePath(value)))
            {
                OnPropertyChanged(nameof(SelectedSourceFileName));
                OnPropertyChanged(nameof(SourceText));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(ChangedBadgeVisibility));
            }
        }
    }

    public string? AppliedSourceFilePath
    {
        get => _appliedSourceFilePath;
        private set
        {
            if (SetProperty(ref _appliedSourceFilePath, NormalizeFilePath(value)))
            {
                OnPropertyChanged(nameof(SourceText));
            }
        }
    }

    public string SelectedSourceFileName => string.IsNullOrWhiteSpace(SelectedSourceFilePath)
        ? string.Empty
        : Path.GetFileName(SelectedSourceFilePath);

    public string SourceText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SelectedSourceFilePath))
            {
                return SelectedSourceFilePath!;
            }

            return string.IsNullOrWhiteSpace(AppliedSourceFilePath)
                ? NotSelectedText
                : AppliedSourceFilePath!;
        }
    }

    public string StatusText => IsChanged ? SelectedStatusText : UnchangedStatusText;

    public bool IsChanged => !string.IsNullOrWhiteSpace(SelectedSourceFilePath);

    public Visibility ChangedBadgeVisibility => IsChanged ? Visibility.Visible : Visibility.Collapsed;

    public void SetSelectedSourceFilePath(string? sourceFilePath)
    {
        SelectedSourceFilePath = sourceFilePath;
    }

    public void MarkSelectedSourceSaved()
    {
        if (!string.IsNullOrWhiteSpace(SelectedSourceFilePath))
        {
            AppliedSourceFilePath = SelectedSourceFilePath;
        }

        SelectedSourceFilePath = null;
    }

    private static string? NormalizeFilePath(string? sourceFilePath) =>
        string.IsNullOrWhiteSpace(sourceFilePath)
            ? null
            : sourceFilePath.Trim();
}
