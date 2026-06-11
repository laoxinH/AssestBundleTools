using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementFilterOptionViewModel : ObservableObject
{
    private const string AllKey = "__all";
    private bool _isSelected;

    public WorkspaceBatchReplacementFilterOptionViewModel(string key, string displayText)
    {
        Key = string.IsNullOrWhiteSpace(key) ? AllKey : key;
        DisplayText = displayText ?? string.Empty;
    }

    public string Key { get; }

    public string DisplayText { get; }

    public bool IsAll => string.Equals(Key, AllKey, StringComparison.Ordinal);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? SelectionChanged;

    public static WorkspaceBatchReplacementFilterOptionViewModel All(string displayText) => new(AllKey, displayText);

    public override string ToString() => DisplayText;
}
