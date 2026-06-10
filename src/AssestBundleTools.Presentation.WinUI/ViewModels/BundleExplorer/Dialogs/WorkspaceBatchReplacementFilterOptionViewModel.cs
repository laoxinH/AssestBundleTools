namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementFilterOptionViewModel
{
    private const string AllKey = "__all";

    public WorkspaceBatchReplacementFilterOptionViewModel(string key, string displayText)
    {
        Key = string.IsNullOrWhiteSpace(key) ? AllKey : key;
        DisplayText = displayText ?? string.Empty;
    }

    public string Key { get; }

    public string DisplayText { get; }

    public bool IsAll => string.Equals(Key, AllKey, StringComparison.Ordinal);

    public static WorkspaceBatchReplacementFilterOptionViewModel All(string displayText) => new(AllKey, displayText);

    public override string ToString() => DisplayText;
}
