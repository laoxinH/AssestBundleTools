using AssestBundleTools.Infrastructure.Workspace.Replacement;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Replacement;

internal static class FileSystemReplacementSourcePathExpanderTests
{
    public static async Task ExpandAsync_ShouldCombineLooseFilesAndDirectoryFilesWithoutDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"abt-replacement-expand-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "folder");
        Directory.CreateDirectory(directory);
        try
        {
            var texturePath = Path.Combine(directory, "hero.png");
            var audioPath = Path.Combine(root, "voice.ogg");
            await File.WriteAllTextAsync(texturePath, "texture");
            await File.WriteAllTextAsync(audioPath, "audio");
            var expander = new FileSystemReplacementSourcePathExpander();

            var result = await expander.ExpandAsync([directory, audioPath, texturePath]);

            TestAssert.True(result.IsSuccess, "Mixed replacement source expansion should succeed.");
            var paths = result.Value ?? throw new InvalidOperationException("Expected expanded replacement paths.");
            TestAssert.Equal(2, paths.Count, "Directory files and loose files should be combined and deduplicated.");
            TestAssert.True(paths.Contains(texturePath), "The directory file should be included.");
            TestAssert.True(paths.Contains(audioPath), "The loose file should be included.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
