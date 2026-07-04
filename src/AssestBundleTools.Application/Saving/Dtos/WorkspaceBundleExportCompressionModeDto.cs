namespace AssestBundleTools.Application.Saving.Dtos;

public enum WorkspaceBundleExportCompressionModeDto
{
    PreserveOriginal = 0,
    FastLz4 = 1,
    Uncompressed = 2,
    Lz4 = 3,
    Lzma = 4
}
