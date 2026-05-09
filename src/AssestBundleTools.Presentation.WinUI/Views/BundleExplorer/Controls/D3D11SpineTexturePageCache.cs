using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineTexturePageCache : IDisposable
{
    private readonly Dictionary<string, D3D11SpineDecodedTexturePage> _decodedPages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, D3D11SpineTexturePage> _gpuPages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpineAnimationTexturePageSize> _texturePageSizes = new(StringComparer.Ordinal);
    private long _gpuPageBytes;

    public IReadOnlyDictionary<string, SpineAnimationTexturePageSize> TexturePageSizes => _texturePageSizes;

    public IReadOnlyDictionary<string, D3D11SpineTexturePage> GpuPages => _gpuPages;

    public long GpuPageBytes => _gpuPageBytes;

    public void ReplaceDecodedPages(IReadOnlyDictionary<string, D3D11SpineDecodedTexturePage> decodedPages)
    {
        ArgumentNullException.ThrowIfNull(decodedPages);

        ClearGpuPages();
        _decodedPages.Clear();
        _texturePageSizes.Clear();
        foreach (var pair in decodedPages)
        {
            if (pair.Value.Width <= 0 || pair.Value.Height <= 0 || pair.Value.BgraPixels.Length == 0)
            {
                continue;
            }

            _decodedPages[pair.Key] = pair.Value;
            _texturePageSizes[pair.Key] = new SpineAnimationTexturePageSize(pair.Value.Width, pair.Value.Height);
        }
    }

    public D3D11SpineTextureUploadResult EnsureGpuPages(ID3D11Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var uploadedPageCount = 0;
        long uploadedBytes = 0;
        foreach (var pair in _decodedPages)
        {
            if (_gpuPages.ContainsKey(pair.Key))
            {
                continue;
            }

            var texturePage = D3D11SpineTexturePage.TryCreate(device, pair.Value);
            if (texturePage is not null)
            {
                _gpuPages[pair.Key] = texturePage;
                uploadedPageCount++;
                uploadedBytes += texturePage.ByteSize;
                _gpuPageBytes += texturePage.ByteSize;
            }
        }

        return new D3D11SpineTextureUploadResult(uploadedPageCount, uploadedBytes);
    }

    public void Clear()
    {
        ClearGpuPages();
        _decodedPages.Clear();
        _texturePageSizes.Clear();
    }

    public void Dispose()
    {
        Clear();
    }

    private void ClearGpuPages()
    {
        foreach (var page in _gpuPages.Values)
        {
            page.Dispose();
        }

        _gpuPages.Clear();
        _gpuPageBytes = 0;
    }
}

internal sealed class D3D11SpineTexturePage : IDisposable
{
    private D3D11SpineTexturePage(
        ID3D11Texture2D texture,
        ID3D11ShaderResourceView shaderResourceView,
        int width,
        int height)
    {
        Texture = texture;
        ShaderResourceView = shaderResourceView;
        Width = width;
        Height = height;
        ByteSize = checked((long)width * height * 4);
    }

    public ID3D11Texture2D Texture { get; }

    public ID3D11ShaderResourceView ShaderResourceView { get; }

    public int Width { get; }

    public int Height { get; }

    public long ByteSize { get; }

    public static D3D11SpineTexturePage? TryCreate(
        ID3D11Device device,
        D3D11SpineDecodedTexturePage decodedPage)
    {
        ID3D11Texture2D? texture = null;
        try
        {
            var pixels = ToUInt32Pixels(decodedPage.BgraPixels);
            texture = device.CreateTexture2D(
                pixels,
                Format.B8G8R8A8_UNorm,
                (uint)decodedPage.Width,
                (uint)decodedPage.Height,
                1,
                1,
                BindFlags.ShaderResource,
                ResourceOptionFlags.None,
                ResourceUsage.Default,
                CpuAccessFlags.None);
            var shaderResourceView = device.CreateShaderResourceView(texture, null);
            return new D3D11SpineTexturePage(
                texture,
                shaderResourceView,
                decodedPage.Width,
                decodedPage.Height);
        }
        catch
        {
            texture?.Dispose();
            return null;
        }
    }

    public void Dispose()
    {
        ShaderResourceView.Dispose();
        Texture.Dispose();
    }

    private static uint[] ToUInt32Pixels(byte[] bgraPixels)
    {
        var pixels = new uint[bgraPixels.Length / sizeof(uint)];
        Buffer.BlockCopy(bgraPixels, 0, pixels, 0, pixels.Length * sizeof(uint));
        return pixels;
    }
}

internal readonly record struct D3D11SpineTextureUploadResult(
    int UploadedPageCount,
    long UploadedBytes);
