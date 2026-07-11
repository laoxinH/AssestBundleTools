using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal sealed class ExternalAssetBundleCodecProcessRunner
{
    private readonly AssetBundleExternalProcessDecryptorManifest _manifest;
    private readonly string _manifestDirectoryPath;

    public ExternalAssetBundleCodecProcessRunner(
        AssetBundleExternalProcessDecryptorManifest manifest,
        string manifestDirectoryPath)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _manifestDirectoryPath = Path.GetFullPath(manifestDirectoryPath);
    }

    public async Task<ExternalProcessExecutionResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        string sourcePath,
        string outputPath,
        string resultPath,
        string workspaceKey,
        string workspaceDataRootPath,
        string workingDirectoryPath,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        CancellationTokenSource? timeoutSource = null;
        CancellationTokenSource? linkedSource = null;
        try
        {
            process = new Process();
            process.StartInfo.FileName = ExternalProcessAssetBundleDecryptorSupport.ResolveExecutablePath(
                _manifest.ExecutablePath,
                _manifestDirectoryPath);
            process.StartInfo.WorkingDirectory = workingDirectoryPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            foreach (var argument in ExpandArguments(
                         arguments,
                         sourcePath,
                         outputPath,
                         resultPath,
                         workspaceKey,
                         workspaceDataRootPath,
                         parameters))
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            if (_manifest.TimeoutMs > 0)
            {
                timeoutSource = new CancellationTokenSource(_manifest.TimeoutMs);
                linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);
            }

            var effectiveToken = linkedSource?.Token ?? cancellationToken;
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(effectiveToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(effectiveToken);
            await process.WaitForExitAsync(effectiveToken);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
                return new ExternalProcessExecutionResult(
                    false,
                    string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
                    ExternalProcessExecutionFailureKind.ProcessExited,
                    process.ExitCode);
            }

            return new ExternalProcessExecutionResult(
                true,
                null,
                ExternalProcessExecutionFailureKind.None,
                0);
        }
        catch (OperationCanceledException)
            when (timeoutSource is not null &&
                  timeoutSource.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            return new ExternalProcessExecutionResult(
                false,
                null,
                ExternalProcessExecutionFailureKind.TimedOut,
                0);
        }
        catch (OperationCanceledException)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            throw;
        }
        catch (Exception)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            return new ExternalProcessExecutionResult(
                false,
                null,
                ExternalProcessExecutionFailureKind.Exception,
                0);
        }
        finally
        {
            linkedSource?.Dispose();
            timeoutSource?.Dispose();
            process?.Dispose();
        }
    }

    private IEnumerable<string> ExpandArguments(
        IReadOnlyList<string> arguments,
        string sourcePath,
        string outputPath,
        string resultPath,
        string workspaceKey,
        string workspaceDataRootPath,
        IReadOnlyDictionary<string, string>? parameters)
    {
        foreach (var argument in arguments)
        {
            var expandedArgument = argument
                .Replace("{inputPath}", Path.GetFullPath(sourcePath), StringComparison.Ordinal)
                .Replace("{outputPath}", outputPath, StringComparison.Ordinal)
                .Replace("{resultPath}", resultPath, StringComparison.Ordinal)
                .Replace("{workspaceKey}", workspaceKey, StringComparison.Ordinal)
                .Replace("{workspaceDataRootPath}", workspaceDataRootPath, StringComparison.Ordinal)
                .Replace("{manifestDirectory}", _manifestDirectoryPath, StringComparison.Ordinal);
            yield return Regex.Replace(
                expandedArgument,
                "\\{parameter:([^{}]+)\\}",
                match => parameters is not null && parameters.TryGetValue(match.Groups[1].Value, out var value)
                    ? value
                    : string.Empty,
                RegexOptions.CultureInvariant);
        }
    }
}
