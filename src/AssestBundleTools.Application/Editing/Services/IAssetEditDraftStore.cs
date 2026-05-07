using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// Provides metadata lookup for stored edit drafts.
/// </summary>
public interface IAssetEditDraftStore
{
    AssetEditDraftDescriptorDto? Load(AssetEditDraftLookupKey lookupKey);
}
