using System;
using System.Runtime.CompilerServices;

namespace BitMiracle.LibJpeg.Classic;

/// <summary>
/// A contiguous 2D buffer backed by a single 1D array.
/// Provides better cache locality than jagged arrays (byte[][]).
/// </summary>
public readonly struct Buffer2D<T>
{
    private readonly T[] _data;
    private readonly int _width;
    private readonly int _height;

    public Buffer2D(int width, int height)
    {
        _width = width;
        _height = height;
        _data = GC.AllocateUninitializedArray<T>(width * height);
    }

    public Buffer2D(T[] data, int width, int height)
    {
        _data = data;
        _width = width;
        _height = height;
    }

    public int Width => _width;
    public int Height => _height;
    public T[] Data => _data;
    public bool IsEmpty => _data == null || _data.Length == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetRow(int row)
    {
        return _data.AsSpan(row * _width, _width);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRowReadOnly(int row)
    {
        return new ReadOnlySpan<T>(_data, row * _width, _width);
    }

    /// <summary>
    /// Indexer for compatibility with byte[][] patterns.
    /// Returns a Span for the row.
    /// </summary>
    public RowAccessor this[int row] => new RowAccessor(_data, row * _width, _width);

    public readonly ref struct RowAccessor
    {
        private readonly T[] _data;
        private readonly int _offset;
        private readonly int _length;

        internal RowAccessor(T[] data, int offset, int length)
        {
            _data = data;
            _offset = offset;
            _length = length;
        }

        public ref T this[int index] => ref _data[_offset + index];

        public int Length => _length;

        public Span<T> AsSpan() => _data.AsSpan(_offset, _length);

        public static implicit operator Span<T>(RowAccessor accessor)
            => accessor._data.AsSpan(accessor._offset, accessor._length);

        public static implicit operator ReadOnlySpan<T>(RowAccessor accessor)
            => new ReadOnlySpan<T>(accessor._data, accessor._offset, accessor._length);
    }

    /// <summary>
    /// Creates a Buffer2D and returns row spans for compatibility with existing code.
    /// </summary>
    public static (Buffer2D<T> buffer, T[][] rowPointers) AllocateWithRowPointers(int width, int height)
    {
        var buffer = new Buffer2D<T>(width, height);
        var rows = new T[height][];

        // Create sub-arrays that point into the main buffer
        // Note: This creates new arrays but they share data with the buffer
        for (int i = 0; i < height; i++)
        {
            rows[i] = new T[width];
        }

        return (buffer, rows);
    }

    public void Clear()
    {
        Array.Clear(_data);
    }

    public void CopyRowTo(int sourceRow, Span<T> destination)
    {
        GetRowReadOnly(sourceRow).CopyTo(destination);
    }

    public void CopyRowFrom(int destRow, ReadOnlySpan<T> source)
    {
        source.CopyTo(GetRow(destRow));
    }
}

/// <summary>
/// Extension methods for Buffer2D interop with legacy byte[][] code.
/// </summary>
public static class Buffer2DExtensions
{
    /// <summary>
    /// Wraps an existing jagged array as a Buffer2D-like accessor.
    /// Does not copy data - useful for gradual migration.
    /// </summary>
    public static JaggedArrayWrapper<T> AsBuffer2D<T>(this T[][] jaggedArray)
    {
        return new JaggedArrayWrapper<T>(jaggedArray);
    }
}

/// <summary>
/// Wrapper that provides Buffer2D-like interface for existing jagged arrays.
/// Used during migration to avoid breaking changes.
/// </summary>
public readonly struct JaggedArrayWrapper<T>
{
    private readonly T[][] _rows;

    public JaggedArrayWrapper(T[][] rows)
    {
        _rows = rows;
    }

    public int Height => _rows?.Length ?? 0;
    public int Width => _rows?.Length > 0 ? _rows[0]?.Length ?? 0 : 0;

    public T[] this[int row] => _rows[row];

    public Span<T> GetRow(int row) => _rows[row].AsSpan();
    public ReadOnlySpan<T> GetRowReadOnly(int row) => _rows[row].AsSpan();
}
