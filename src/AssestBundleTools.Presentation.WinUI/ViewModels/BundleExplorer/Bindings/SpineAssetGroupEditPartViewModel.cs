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

    public SpineAssetGroupEditPartViewModel(
        SpineAssetGroupPartDto part,
        string roleText,
        string selectButtonText,
        string notSelectedText,
        string selectedStatusText,
        string unchangedStatusText)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
        RoleText = roleText ?? throw new ArgumentNullException(nameof(roleText));
        SelectButtonText = selectButtonText ?? throw new ArgumentNullException(nameof(selectButtonText));
        NotSelectedText = notSelectedText ?? throw new ArgumentNullException(nameof(notSelectedText));
        SelectedStatusText = selectedStatusText ?? throw new ArgumentNullException(nameof(selectedStatusText));
        UnchangedStatusText = unchangedStatusText ?? throw new ArgumentNullException(nameof(unchangedStatusText));
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

    public string SelectedSourceFileName => string.IsNullOrWhiteSpace(SelectedSourceFilePath)
        ? string.Empty
        : Path.GetFileName(SelectedSourceFilePath);

    public string SourceText => string.IsNullOrWhiteSpace(SelectedSourceFilePath)
        ? NotSelectedText
        : SelectedSourceFilePath!;

    public string StatusText => IsChanged ? SelectedStatusText : UnchangedStatusText;

    public bool IsChanged => !string.IsNullOrWhiteSpace(SelectedSourceFilePath);

    public Visibility ChangedBadgeVisibility => IsChanged ? Visibility.Visible : Visibility.Collapsed;

    public void SetSelectedSourceFilePath(string? sourceFilePath)
    {
        SelectedSourceFilePath = sourceFilePath;
    }

    private static string? NormalizeFilePath(string? sourceFilePath) =>
        string.IsNullOrWhiteSpace(sourceFilePath)
            ? null
            : sourceFilePath.Trim();
}
