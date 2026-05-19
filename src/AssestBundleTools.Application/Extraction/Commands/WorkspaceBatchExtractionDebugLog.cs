using System.Diagnostics;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Commands;

internal static class WorkspaceBatchExtractionDebugLog
{
    [Conditional("DEBUG")]
    public static void Write(string message) =>
        Debug.WriteLine($"[WorkspaceBatchExtraction] {DateTimeOffset.Now:HH:mm:ss.fff} {message}");

    public static string FormatTaskId() =>
        Task.CurrentId?.ToString() ?? "-";

    public static string FormatCategories(
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories)
    {
        if (categories.Count == 0)
        {
            return "AllImplicit";
        }

        return string.Join(
            ",",
            categories.Select(static category => category.Kind switch
            {
                WorkspaceBatchExtractionCategoryKind.All => "All",
                WorkspaceBatchExtractionCategoryKind.SpineGroup => "SpineGroup",
                WorkspaceBatchExtractionCategoryKind.UnityAssetType =>
                    string.IsNullOrWhiteSpace(category.AssetTypeDisplayName)
                        ? category.AssetType?.ToString() ?? "UnityAssetType"
                        : $"{category.AssetType}:{category.AssetTypeDisplayName}",
                _ => category.Kind.ToString()
            }));
    }
}
