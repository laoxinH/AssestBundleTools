using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpinePreviewBackgroundRenderer : IDisposable
{
    private const double CheckerboardCellSizeInXamlPixels = 12d;
    private const float CheckerboardDarkRed = 195f / 255f;
    private const float CheckerboardDarkGreen = 201f / 255f;
    private const float CheckerboardDarkBlue = 210f / 255f;

    private static readonly Color4 SolidBackgroundColor = new(1f, 1f, 1f, 1f);
    private static readonly Color4 CheckerboardLightColor = new(235f / 255f, 238f / 255f, 242f / 255f, 1f);

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
            float4 Color : COLOR0;
        };

        PSInput VSMain(VSInput input)
        {
            PSInput output;
            output.Position = float4(input.Position, 0.0f, 1.0f);
            output.Color = input.Color;
            return output;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            return input.Color;
        }
        """;

    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11RasterizerState? _rasterizerState;
    private ID3D11BlendState? _blendState;
    private readonly D3D11SpineDynamicBatchBuffer _batchBuffer = new();
    private CheckerboardGeometry? _checkerboardGeometry;

    public void Draw(
        ID3D11Device device,
        ID3D11DeviceContext context,
        ID3D11RenderTargetView renderTargetView,
        int width,
        int height,
        double compositionScaleX,
        double compositionScaleY,
        InspectorPreviewImageBackgroundMode backgroundMode)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(renderTargetView);

        if (backgroundMode != InspectorPreviewImageBackgroundMode.Checkerboard)
        {
            context.ClearRenderTargetView(renderTargetView, SolidBackgroundColor);
            return;
        }

        context.ClearRenderTargetView(renderTargetView, CheckerboardLightColor);
        if (width <= 1 || height <= 1)
        {
            return;
        }

        EnsureDeviceResources(device);
        var geometry = ResolveCheckerboardGeometry(width, height, compositionScaleX, compositionScaleY);
        if (geometry.Vertices.Length == 0 || geometry.Indices.Length == 0)
        {
            return;
        }

        ApplyPipeline(context, width, height);
        _batchBuffer.Upload(device, context, geometry.Vertices, geometry.Indices);
        context.IASetVertexBuffer(
            0,
            _batchBuffer.VertexBuffer,
            (uint)Marshal.SizeOf<D3D11SpineBatchVertex>(),
            0);
        context.IASetIndexBuffer(_batchBuffer.IndexBuffer, Format.R32_UInt, 0);
        context.DrawIndexed((uint)geometry.Indices.Length, 0, 0);
    }

    public void ReleaseDeviceResources()
    {
        _batchBuffer.Clear();
        _blendState?.Dispose();
        _rasterizerState?.Dispose();
        _inputLayout?.Dispose();
        _pixelShader?.Dispose();
        _vertexShader?.Dispose();
        _blendState = null;
        _rasterizerState = null;
        _inputLayout = null;
        _pixelShader = null;
        _vertexShader = null;
    }

    public void Dispose()
    {
        ReleaseDeviceResources();
        _checkerboardGeometry = null;
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
            "SpinePreviewBackground.hlsl",
            "vs_4_0",
            ShaderFlags.None,
            EffectFlags.None);
        var pixelShaderBytecode = Compiler.Compile(
            ShaderSource,
            "PSMain",
            "SpinePreviewBackground.hlsl",
            "ps_4_0",
            ShaderFlags.None,
            EffectFlags.None);
        var vertexShaderBytes = vertexShaderBytecode.Span.ToArray();
        var pixelShaderBytes = pixelShaderBytecode.Span.ToArray();

        _vertexShader = device.CreateVertexShader(vertexShaderBytes, null!);
        _pixelShader = device.CreatePixelShader(pixelShaderBytes, null!);
        _inputLayout = device.CreateInputLayout(
            D3D11SpineAnimationBatchRenderer.CreateInputElements(),
            vertexShaderBytes);
        _rasterizerState = device.CreateRasterizerState(RasterizerDescription.CullNone);
        _blendState = device.CreateBlendState(new BlendDescription(
            Blend.One,
            Blend.Zero,
            Blend.One,
            Blend.Zero));
    }

    private void ApplyPipeline(ID3D11DeviceContext context, int width, int height)
    {
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_inputLayout);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.RSSetState(_rasterizerState);
        context.RSSetViewport(0f, 0f, width, height, 0f, 1f);
        context.OMSetBlendState(_blendState);
    }

    private CheckerboardGeometry ResolveCheckerboardGeometry(
        int width,
        int height,
        double compositionScaleX,
        double compositionScaleY)
    {
        var scale = D3D11SpineSwapChainComposition.ResolveCheckerboardScale(
            compositionScaleX,
            compositionScaleY);
        var cellSize = Math.Max(1d, CheckerboardCellSizeInXamlPixels * scale);
        if (_checkerboardGeometry is { } cached &&
            cached.Width == width &&
            cached.Height == height &&
            Math.Abs(cached.CellSize - cellSize) < 0.0001d)
        {
            return cached;
        }

        _checkerboardGeometry = BuildCheckerboardGeometry(width, height, cellSize);
        return _checkerboardGeometry;
    }

    private static CheckerboardGeometry BuildCheckerboardGeometry(
        int width,
        int height,
        double cellSize)
    {
        var tiles = CheckerboardTileLayoutBuilder.Build(width, height, cellSize);
        var vertices = new D3D11SpineBatchVertex[tiles.Count * 4];
        var indices = new int[tiles.Count * 6];
        var vertexIndex = 0;
        var indexIndex = 0;
        foreach (var tile in tiles)
        {
            var left = ProjectX(tile.X, width);
            var right = ProjectX(tile.X + tile.Width, width);
            var top = ProjectY(tile.Y, height);
            var bottom = ProjectY(tile.Y + tile.Height, height);

            vertices[vertexIndex] = CreateVertex(left, top);
            vertices[vertexIndex + 1] = CreateVertex(right, top);
            vertices[vertexIndex + 2] = CreateVertex(right, bottom);
            vertices[vertexIndex + 3] = CreateVertex(left, bottom);

            indices[indexIndex] = vertexIndex;
            indices[indexIndex + 1] = vertexIndex + 1;
            indices[indexIndex + 2] = vertexIndex + 2;
            indices[indexIndex + 3] = vertexIndex;
            indices[indexIndex + 4] = vertexIndex + 2;
            indices[indexIndex + 5] = vertexIndex + 3;

            vertexIndex += 4;
            indexIndex += 6;
        }

        return new CheckerboardGeometry(width, height, cellSize, vertices, indices);
    }

    private static D3D11SpineBatchVertex CreateVertex(float x, float y) =>
        new(
            x,
            y,
            0f,
            0f,
            CheckerboardDarkRed,
            CheckerboardDarkGreen,
            CheckerboardDarkBlue,
            1f);

    private static float ProjectX(double x, int width) =>
        (float)((x / width * 2d) - 1d);

    private static float ProjectY(double y, int height) =>
        (float)(1d - (y / height * 2d));

    private sealed record CheckerboardGeometry(
        int Width,
        int Height,
        double CellSize,
        D3D11SpineBatchVertex[] Vertices,
        int[] Indices);
}
