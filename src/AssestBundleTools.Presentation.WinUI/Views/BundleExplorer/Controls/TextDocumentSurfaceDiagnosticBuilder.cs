using System.Text;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class TextDocumentSurfaceDiagnosticBuilder
{
    private readonly List<(string Name, string Value)> _items = new();

    public TextDocumentSurfaceDiagnosticBuilder Add(string name, string? value)
    {
        _items.Add((name, string.IsNullOrWhiteSpace(value) ? "(empty)" : value));
        return this;
    }

    public TextDocumentSurfaceDiagnosticBuilder Add(string name, bool value)
    {
        _items.Add((name, value ? "true" : "false"));
        return this;
    }

    public TextDocumentSurfaceDiagnosticBuilder Add(string name, int value)
    {
        _items.Add((name, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return this;
    }

    public TextDocumentSurfaceDiagnosticBuilder Add(string name, ElementTheme value)
    {
        _items.Add((name, value.ToString()));
        return this;
    }

    public TextDocumentSurfaceDiagnosticBuilder AddRuntimeProbe(TextDocumentWebViewRuntimeProbe probe)
    {
        Add("RuntimeAvailable", probe.IsAvailable);
        Add("RuntimeVersion", probe.Version);
        Add("RuntimeErrorCode", probe.ErrorCode);
        Add("RuntimeHResult", probe.HResult);
        Add("RuntimeMessage", probe.Message);
        return this;
    }

    public string Build(string description, string? detail)
    {
        var builder = new StringBuilder();
        builder.AppendLine(description);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            builder.AppendLine(detail);
        }

        foreach (var item in _items)
        {
            builder
                .Append(item.Name)
                .Append(": ")
                .AppendLine(item.Value);
        }

        return builder.ToString().TrimEnd();
    }
}
