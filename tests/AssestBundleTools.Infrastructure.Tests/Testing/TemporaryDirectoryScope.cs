namespace AssestBundleTools.Infrastructure.Tests.Testing;

internal sealed class TemporaryDirectoryScope : IDisposable
{
    public TemporaryDirectoryScope()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools.Tests",
            Guid.NewGuid().ToString("N"));
    }

    public string DirectoryPath { get; }

    public void Dispose()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return;
        }

        Directory.Delete(DirectoryPath, recursive: true);
    }
}
