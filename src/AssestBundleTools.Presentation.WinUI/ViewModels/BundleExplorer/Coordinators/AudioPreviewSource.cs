using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed record AudioPreviewSource(
    MediaSource MediaSource,
    IRandomAccessStream Stream);
