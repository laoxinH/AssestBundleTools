using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Services;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class NativeOleFileDropDataObject : IDataObject, IDisposable
{
    private const short ClipboardFormatFileDrop = 15;
    private const int DataFormatError = unchecked((int)0x80040064);
    private const int NotImplemented = unchecked((int)0x80004001);
    private const int AdviseNotSupported = unchecked((int)0x80040003);
    private const int SameFormat = 0x00040130;
    private readonly Func<IReadOnlyList<string>> _pathsProvider;
    private readonly string? _diagnosticPath;
    private readonly List<StoredFormat> _storedFormats = [];
    private readonly HashSet<short> _queriedFormats = [];
    private IReadOnlyList<string>? _paths;
    private bool _disposed;

    public NativeOleFileDropDataObject(
        Func<IReadOnlyList<string>> pathsProvider,
        string? diagnosticPath = null)
    {
        _pathsProvider = pathsProvider ?? throw new ArgumentNullException(nameof(pathsProvider));
        _diagnosticPath = diagnosticPath;
    }

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsFileDropFormat(format))
        {
            _paths ??= _pathsProvider();
            AppendDiagnostic($"CF_HDROP requested. Paths={_paths.Count}.");
            medium = new STGMEDIUM
            {
                tymed = TYMED.TYMED_HGLOBAL,
                unionmember = CreateFileDropHandle(_paths),
                pUnkForRelease = null
            };
            return;
        }

        var requestedFormat = format;
        var stored = _storedFormats.FirstOrDefault(
            candidate => Matches(candidate.Format, requestedFormat));
        if (stored is null)
        {
            throw new COMException("The requested drag format is unavailable.", DataFormatError);
        }

        AppendDiagnostic($"Private drag format requested. Format={stored.Format.cfFormat}.");
        medium = CloneMedium(stored.Medium, stored.Format.cfFormat);
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) =>
        throw new COMException("GetDataHere is not supported.", NotImplemented);

    public int QueryGetData(ref FORMATETC format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var requestedFormat = format;
        var available = IsFileDropFormat(requestedFormat) ||
                        _storedFormats.Any(candidate => Matches(candidate.Format, requestedFormat));
        if (_queriedFormats.Add(requestedFormat.cfFormat))
        {
            AppendDiagnostic(
                $"Drag format queried. Format={requestedFormat.cfFormat}; Available={available}.");
        }

        return available ? 0 : DataFormatError;
    }

    public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
    {
        formatOut = formatIn;
        formatOut.ptd = IntPtr.Zero;
        return SameFormat;
    }

    public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (medium.tymed == TYMED.TYMED_NULL || formatIn.cfFormat == 0)
        {
            throw new COMException("The drag format medium is invalid.", DataFormatError);
        }

        var normalizedFormat = formatIn;
        normalizedFormat.ptd = IntPtr.Zero;
        var ownedMedium = release ? medium : CloneMedium(medium, formatIn.cfFormat);
        var existingIndex = _storedFormats.FindIndex(
            candidate => SameIdentity(candidate.Format, normalizedFormat));
        if (existingIndex >= 0)
        {
            var existing = _storedFormats[existingIndex];
            ReleaseStgMedium(ref existing.Medium);
            _storedFormats[existingIndex] = new StoredFormat(normalizedFormat, ownedMedium);
            return;
        }

        _storedFormats.Add(new StoredFormat(normalizedFormat, ownedMedium));
        AppendDiagnostic($"Private drag format stored. Format={normalizedFormat.cfFormat}.");
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (direction != DATADIR.DATADIR_GET)
        {
            throw new COMException("Only data retrieval formats are supported.", NotImplemented);
        }

        return new NativeOleFormatEnumerator(
            [CreateFileDropFormat(), .. _storedFormats.Select(static stored => stored.Format)]);
    }

    public int DAdvise(ref FORMATETC format, ADVF advf, IAdviseSink adviseSink, out int connection)
    {
        connection = 0;
        return AdviseNotSupported;
    }

    public void DUnadvise(int connection) =>
        throw new COMException("Data advise is not supported.", AdviseNotSupported);

    public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
    {
        enumAdvise = null;
        return AdviseNotSupported;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var stored in _storedFormats)
        {
            ReleaseStgMedium(ref stored.Medium);
        }

        _storedFormats.Clear();
        _disposed = true;
    }

    private static bool IsFileDropFormat(FORMATETC format) =>
        format.cfFormat == ClipboardFormatFileDrop &&
        format.dwAspect == DVASPECT.DVASPECT_CONTENT &&
        (format.tymed & TYMED.TYMED_HGLOBAL) != 0;

    private static bool Matches(FORMATETC available, FORMATETC requested) =>
        available.cfFormat == requested.cfFormat &&
        available.dwAspect == requested.dwAspect &&
        available.lindex == requested.lindex &&
        (available.tymed & requested.tymed) != 0;

    private static bool SameIdentity(FORMATETC left, FORMATETC right) =>
        left.cfFormat == right.cfFormat &&
        left.dwAspect == right.dwAspect &&
        left.lindex == right.lindex &&
        left.tymed == right.tymed;

    private static FORMATETC CreateFileDropFormat() => new()
    {
        cfFormat = ClipboardFormatFileDrop,
        ptd = IntPtr.Zero,
        dwAspect = DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        tymed = TYMED.TYMED_HGLOBAL
    };

    private static STGMEDIUM CloneMedium(STGMEDIUM source, short clipboardFormat)
    {
        if (source.pUnkForRelease is not null)
        {
            throw new COMException("Custom STGMEDIUM release owners are not supported.", DataFormatError);
        }

        IntPtr duplicate;
        switch (source.tymed)
        {
            case TYMED.TYMED_HGLOBAL:
            case TYMED.TYMED_GDI:
            case TYMED.TYMED_ENHMF:
            case TYMED.TYMED_MFPICT:
                duplicate = OleDuplicateData(source.unionmember, clipboardFormat, 0);
                break;
            case TYMED.TYMED_FILE:
                var fileName = Marshal.PtrToStringUni(source.unionmember) ?? string.Empty;
                duplicate = Marshal.StringToCoTaskMemUni(fileName);
                break;
            case TYMED.TYMED_ISTREAM:
            case TYMED.TYMED_ISTORAGE:
                duplicate = source.unionmember;
                _ = Marshal.AddRef(duplicate);
                break;
            default:
                throw new COMException("The drag format medium cannot be duplicated.", DataFormatError);
        }

        if (duplicate == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Duplicating the drag format medium failed.");
        }

        return new STGMEDIUM
        {
            tymed = source.tymed,
            unionmember = duplicate,
            pUnkForRelease = null
        };
    }

    private static IntPtr CreateFileDropHandle(IReadOnlyList<string> paths)
    {
        var pathBytes = Encoding.Unicode.GetBytes(string.Concat(string.Join('\0', paths), "\0\0"));
        var headerSize = Marshal.SizeOf<DropFiles>();
        var handle = GlobalAlloc(0x0042, checked((UIntPtr)(headerSize + pathBytes.Length)));
        if (handle == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Allocating the CF_HDROP payload failed.");
        }

        var memory = GlobalLock(handle);
        if (memory == IntPtr.Zero)
        {
            _ = GlobalFree(handle);
            throw new OutOfMemoryException("Locking the CF_HDROP payload failed.");
        }

        try
        {
            Marshal.StructureToPtr(
                new DropFiles { FileListOffset = checked((uint)headerSize), IsWide = 1 },
                memory,
                fDeleteOld: false);
            Marshal.Copy(pathBytes, 0, IntPtr.Add(memory, headerSize), pathBytes.Length);
        }
        finally
        {
            _ = GlobalUnlock(handle);
        }

        return handle;
    }

    private void AppendDiagnostic(string message)
    {
        if (_diagnosticPath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_diagnosticPath, string.Concat(message, Environment.NewLine));
        }
        catch
        {
        }
    }

    private sealed class StoredFormat
    {
        public StoredFormat(FORMATETC format, STGMEDIUM medium)
        {
            Format = format;
            Medium = medium;
        }

        public FORMATETC Format { get; }

        public STGMEDIUM Medium;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DropFiles
    {
        public uint FileListOffset;
        public int X;
        public int Y;
        public int IsNonClient;
        public int IsWide;
    }

    [DllImport("ole32.dll")]
    private static extern IntPtr OleDuplicateData(IntPtr source, short clipboardFormat, uint flags);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}

internal sealed class NativeOleFormatEnumerator : IEnumFORMATETC
{
    private readonly IReadOnlyList<FORMATETC> _formats;
    private int _index;

    public NativeOleFormatEnumerator(IReadOnlyList<FORMATETC> formats, int index = 0)
    {
        _formats = formats;
        _index = index;
    }

    public int Next(int count, FORMATETC[] formats, int[]? fetched)
    {
        var copied = 0;
        while (copied < count && copied < formats.Length && _index < _formats.Count)
        {
            formats[copied++] = _formats[_index++];
        }

        if (fetched is { Length: > 0 })
        {
            fetched[0] = copied;
        }

        return copied == count ? 0 : 1;
    }

    public int Skip(int count)
    {
        var available = _formats.Count - _index;
        var skipped = Math.Min(Math.Max(count, 0), available);
        _index += skipped;
        return skipped == count ? 0 : 1;
    }

    public int Reset()
    {
        _index = 0;
        return 0;
    }

    public void Clone(out IEnumFORMATETC newEnum) =>
        newEnum = new NativeOleFormatEnumerator(_formats, _index);
}
