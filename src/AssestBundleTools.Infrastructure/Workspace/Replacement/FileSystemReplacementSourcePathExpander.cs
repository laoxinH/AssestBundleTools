using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Replacement;

/// <summary>
/// 基于文件系统展开直接文件和目录内第一层文件。
/// </summary>
public sealed class FileSystemReplacementSourcePathExpander : IReplacementSourcePathExpander
{
    public Task<Result<IReadOnlyList<string>>> ExpandAsync(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var files = new List<string>();
            foreach (var sourcePath in sourcePaths
                         .Where(static path => !string.IsNullOrWhiteSpace(path))
                         .Select(static path => path.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(sourcePath))
                {
                    files.AddRange(Directory.EnumerateFiles(sourcePath));
                }
                else if (File.Exists(sourcePath))
                {
                    files.Add(sourcePath);
                }
            }

            return Task.FromResult(Result<IReadOnlyList<string>>.Success(
                files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<IReadOnlyList<string>>.Cancelled());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult(Result<IReadOnlyList<string>>.Failure(
                ApplicationErrors.BatchReplacementSourceFileReadFailed(
                    sourcePaths.FirstOrDefault() ?? string.Empty,
                    exception.GetType().Name)));
        }
    }
}
