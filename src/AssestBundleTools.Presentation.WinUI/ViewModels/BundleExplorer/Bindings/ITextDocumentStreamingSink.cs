using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public interface ITextDocumentStreamingSink
{
    bool CanStreamDocument { get; }

    Task BeginStreamingDocumentAsync(
        string language,
        long expectedLength,
        CancellationToken cancellationToken = default);

    Task AppendDocumentChunkAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task EndStreamingDocumentAsync(
        TextAssetEditDraftPatchBaselineDto baseline,
        bool readOnly,
        CancellationToken cancellationToken = default);

    Task CancelStreamingDocumentAsync(CancellationToken cancellationToken = default);
}
