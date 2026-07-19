using AssestBundleTools.Application.Common.Diagnostics;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示详细日志面板中的一条展示记录。
/// </summary>
public sealed record LogEntryViewModel(
    string TimestampText,
    string Message,
    WorkspaceDiagnosticLogLevel Level = WorkspaceDiagnosticLogLevel.Information)
{
    public Visibility InformationVisibility =>
        Level == WorkspaceDiagnosticLogLevel.Information ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WarningVisibility =>
        Level == WorkspaceDiagnosticLogLevel.Warning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility =>
        Level == WorkspaceDiagnosticLogLevel.Error ? Visibility.Visible : Visibility.Collapsed;

    public string CopyText => $"[{TimestampText}] [{Level}] {NormalizeMessage(Message)}";

    private static string NormalizeMessage(string message) =>
        (message ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}
