if (args.Length != 2 || !string.Equals(args[0], "post-export", StringComparison.Ordinal))
{
    return 2;
}

var manifestPath = Path.GetFullPath(args[1]);
if (!File.Exists(manifestPath))
{
    return 3;
}

await File.WriteAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "invoked.txt"),
    manifestPath);
return 0;
