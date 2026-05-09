using System.Buffers;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineAnimationBatchRenderer : IDisposable
{
    private const string ShaderSource = """
        struct VSInput
        {
            float2 Position : POSITION;
            float2 TexCoord : TEXCOORD0;
            float4 Color : COLOR0;
        };

        struct PSInput
        {
            float4 Position : SV_POSITION;
            float2 TexCoord : TEXCOORD0;
            float4 Color : COLOR0;
        };

        Texture2D SpineTexture : register(t0);
        SamplerState SpineSampler : register(s0);

        PSInput VSMain(VSInput input)
        {
            PSInput output;
            output.Position = float4(input.Position, 0.0f, 1.0f);
            output.TexCoord = input.TexCoord;
            output.Color = input.Color;
            return output;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            return SpineTexture.Sample(SpineSampler, input.TexCoord) * input.Color;
        }
        """;

    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11RasterizerState? _rasterizerState;
    private ID3D11SamplerState? _samplerState;
    private ID3D11BlendState? _blendState;
    private ID3D11Texture2D? _fallbackTexture;
    private ID3D11ShaderResourceView? _fallbackTextureView;
    private readonly D3D11SpineDynamicBatchBuffer _batchBuffer = new();

    public void Draw(
        ID3D11Device device,
        ID3D11DeviceContext context,
        IReadOnlyList<SpineAnimationClipSpaceBatch> clipBatches,
        IReadOnlyDictionary<string, D3D11SpineTexturePage> texturePages,
        float viewportWidth,
        float viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clipBatches);
        ArgumentNullException.ThrowIfNull(texturePages);

        EnsureDeviceResources(device);
        if (clipBatches.Count == 0)
        {
            return;
        }

        ApplyPipeline(context, viewportWidth, viewportHeight);
        foreach (var batch in clipBatches)
        {
            DrawBatch(device, context, batch, ResolveTextureView(batch.PageName, texturePages));
        }
    }

    public void Dispose()
    {
        ReleaseDeviceResources();
    }

    public void ReleaseDeviceResources()
    {
        _batchBuffer.Clear();
        _fallbackTextureView?.Dispose();
        _fallbackTexture?.Dispose();
        _blendState?.Dispose();
        _samplerState?.Dispose();
        _rasterizerState?.Dispose();
        _inputLayout?.Dispose();
        _pixelShader?.Dispose();
        _vertexShader?.Dispose();
        _fallbackTextureView = null;
        _fallbackTexture = null;
        _blendState = null;
        _samplerState = null;
        _rasterizerState = null;
        _inputLayout = null;
        _pixelShader = null;
        _vertexShader = null;
    }

    private void EnsureDeviceResources(ID3D11Device device)
    {
        if (_vertexShader is not null)
        {
            return;
        }

        var vertexShaderBytecode = Compiler.Compile(
            ShaderSource,
            "VSMain",
            "SpineAnimationBatchSmoke.hlsl",
            "vs_4_0",
            ShaderFlags.None,
            EffectFlags.None);
        var pixelShaderBytecode = Compiler.Compile(
            ShaderSource,
            "PSMain",
            "SpineAnimationBatchSmoke.hlsl",
            "ps_4_0",
            ShaderFlags.None,
            EffectFlags.None);

        _vertexShader = device.CreateVertexShader(vertexShaderBytecode.Span, null!);
        _pixelShader = device.CreatePixelShader(pixelShaderBytecode.Span, null!);
        _inputLayout = device.CreateInputLayout(CreateInputElements(), vertexShaderBytecode.Span);
        _rasterizerState = device.CreateRasterizerState(RasterizerDescription.CullNone);
        _samplerState = device.CreateSamplerState(SamplerDescription.LinearClamp);
        _blendState = device.CreateBlendState(BlendDescription.NonPremultiplied);
        (_fallbackTexture, _fallbackTextureView) = CreateFallbackTexture(device);
    }

    internal static InputElementDescription[] CreateInputElements() =>
        [
            new("POSITION", 0, Format.R32G32_Float, offset: 0, slot: 0),
            new("TEXCOORD", 0, Format.R32G32_Float, offset: 8, slot: 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, offset: 16, slot: 0)
        ];

    private void ApplyPipeline(ID3D11DeviceContext context, float viewportWidth, float viewportHeight)
    {
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_inputLayout);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.RSSetState(_rasterizerState);
        context.RSSetViewport(0f, 0f, viewportWidth, viewportHeight, 0f, 1f);
        context.PSSetSamplers(0, [_samplerState!]);
        context.OMSetBlendState(_blendState);
    }

    private void DrawBatch(
        ID3D11Device device,
        ID3D11DeviceContext context,
        SpineAnimationClipSpaceBatch batch,
        ID3D11ShaderResourceView textureView)
    {
        if (batch.Vertices.Count == 0 || batch.Indices.Count == 0)
        {
            return;
        }

        var vertices = ArrayPool<D3D11SpineBatchVertex>.Shared.Rent(batch.Vertices.Count);
        var indices = ArrayPool<int>.Shared.Rent(batch.Indices.Count);
        try
        {
            CopyVertices(batch.Vertices, vertices);
            CopyIndices(batch.Indices, indices);
            _batchBuffer.Upload(
                device,
                context,
                vertices.AsSpan(0, batch.Vertices.Count),
                indices.AsSpan(0, batch.Indices.Count));
            context.IASetVertexBuffer(0, _batchBuffer.VertexBuffer, (uint)Marshal.SizeOf<D3D11SpineBatchVertex>(), 0);
            context.IASetIndexBuffer(_batchBuffer.IndexBuffer, Format.R32_UInt, 0);
            context.PSSetShaderResources(0, [textureView]);
            context.DrawIndexed((uint)batch.Indices.Count, 0, 0);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indices);
            ArrayPool<D3D11SpineBatchVertex>.Shared.Return(vertices);
        }
    }

    private ID3D11ShaderResourceView ResolveTextureView(
        string pageName,
        IReadOnlyDictionary<string, D3D11SpineTexturePage> texturePages) =>
        texturePages.TryGetValue(pageName, out var texturePage)
            ? texturePage.ShaderResourceView
            : _fallbackTextureView!;

    private static (ID3D11Texture2D Texture, ID3D11ShaderResourceView ShaderResourceView) CreateFallbackTexture(
        ID3D11Device device)
    {
        uint[] whitePixel = [0xffffffffu];
        var texture = device.CreateTexture2D(
            whitePixel,
            Format.B8G8R8A8_UNorm,
            1,
            1,
            1,
            1,
            BindFlags.ShaderResource,
            ResourceOptionFlags.None,
            ResourceUsage.Default,
            CpuAccessFlags.None);
        return (texture, device.CreateShaderResourceView(texture, null));
    }

    private static D3D11SpineBatchVertex CreateRenderVertex(SpineAnimationClipSpaceVertex vertex) =>
        new(vertex.X, vertex.Y, vertex.U, vertex.V, vertex.R, vertex.G, vertex.B, vertex.A);

    private static void CopyVertices(
        IReadOnlyList<SpineAnimationClipSpaceVertex> source,
        D3D11SpineBatchVertex[] destination)
    {
        for (var index = 0; index < source.Count; index++)
        {
            destination[index] = CreateRenderVertex(source[index]);
        }
    }

    private static void CopyIndices(
        IReadOnlyList<int> source,
        int[] destination)
    {
        for (var index = 0; index < source.Count; index++)
        {
            destination[index] = source[index];
        }
    }
}
