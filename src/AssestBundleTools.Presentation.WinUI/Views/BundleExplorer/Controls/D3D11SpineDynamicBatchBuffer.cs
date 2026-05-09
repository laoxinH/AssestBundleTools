using System.Buffers;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineDynamicBatchBuffer : IDisposable
{
    internal const int MinimumVertexCapacity = 256;
    internal const int MinimumIndexCapacity = 384;

    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _indexBuffer;
    private int _vertexCapacity;
    private int _indexCapacity;

    public ID3D11Buffer VertexBuffer => _vertexBuffer ?? throw new InvalidOperationException();

    public ID3D11Buffer IndexBuffer => _indexBuffer ?? throw new InvalidOperationException();

    public int VertexCapacity => _vertexCapacity;

    public int IndexCapacity => _indexCapacity;

    public void Upload(
        ID3D11Device device,
        ID3D11DeviceContext context,
        ReadOnlySpan<D3D11SpineBatchVertex> vertices,
        ReadOnlySpan<int> indices)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(context);

        if (vertices.IsEmpty || indices.IsEmpty)
        {
            return;
        }

        EnsureVertexBuffer(device, vertices.Length);
        EnsureIndexBuffer(device, indices.Length);
        UploadToBuffer(context, VertexBuffer, vertices);
        UploadToBuffer(context, IndexBuffer, indices);
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        _indexBuffer?.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer = null;
        _vertexBuffer = null;
        _indexCapacity = 0;
        _vertexCapacity = 0;
    }

    internal static int CalculateCapacity(int requestedCount, int minimumCapacity)
    {
        if (requestedCount <= 0)
        {
            return 0;
        }

        var capacity = Math.Max(1, minimumCapacity);
        while (capacity < requestedCount)
        {
            capacity = checked(capacity * 2);
        }

        return capacity;
    }

    private void EnsureVertexBuffer(ID3D11Device device, int vertexCount)
    {
        if (vertexCount <= _vertexCapacity)
        {
            return;
        }

        var capacity = CalculateCapacity(vertexCount, MinimumVertexCapacity);
        var buffer = CreateDynamicBuffer(
            device,
            capacity,
            Marshal.SizeOf<D3D11SpineBatchVertex>(),
            BindFlags.VertexBuffer);
        _vertexBuffer?.Dispose();
        _vertexBuffer = buffer;
        _vertexCapacity = capacity;
    }

    private void EnsureIndexBuffer(ID3D11Device device, int indexCount)
    {
        if (indexCount <= _indexCapacity)
        {
            return;
        }

        var capacity = CalculateCapacity(indexCount, MinimumIndexCapacity);
        var buffer = CreateDynamicBuffer(
            device,
            capacity,
            sizeof(int),
            BindFlags.IndexBuffer);
        _indexBuffer?.Dispose();
        _indexBuffer = buffer;
        _indexCapacity = capacity;
    }

    private static ID3D11Buffer CreateDynamicBuffer(
        ID3D11Device device,
        int capacity,
        int elementSize,
        BindFlags bindFlags) =>
        device.CreateBuffer(
            checked((uint)(capacity * elementSize)),
            bindFlags,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

    private static void UploadToBuffer<T>(
        ID3D11DeviceContext context,
        ID3D11Buffer buffer,
        ReadOnlySpan<T> items)
        where T : struct
    {
        var bytes = MemoryMarshal.AsBytes(items);
        var rented = ArrayPool<byte>.Shared.Rent(bytes.Length);
        try
        {
            bytes.CopyTo(rented.AsSpan(0, bytes.Length));
            var mapped = context.Map(buffer, MapMode.WriteDiscard, MapFlags.None);
            try
            {
                Marshal.Copy(rented, 0, mapped.DataPointer, bytes.Length);
            }
            finally
            {
                context.Unmap(buffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct D3D11SpineBatchVertex(
    float X,
    float Y,
    float U,
    float V,
    float R,
    float G,
    float B,
    float A);
