using BitMiracle.LibTiff.Classic;

namespace TerraRenderer.AssetBuilder;

internal sealed record GeoTiffReadInfo(
    int SourceWidth,
    int SourceHeight,
    int OutputWidth,
    int OutputHeight,
    bool IsTiled,
    int BlockWidth,
    int BlockHeight,
    int BitsPerSample,
    SampleFormat SampleFormat,
    double? NoDataValue);

internal static class GeoTiffElevationReader
{
    public static (Raster<float> Raster, GeoTiffReadInfo Info) ReadResampled(
        string path,
        int outputWidth,
        int outputHeight,
        int tileCacheCapacity = 384)
    {
        using var tiff = Tiff.Open(path, "r") ?? throw new InvalidOperationException($"Cannot open GeoTIFF: {path}");
        var width = RequiredInt(tiff, TiffTag.IMAGEWIDTH);
        var height = RequiredInt(tiff, TiffTag.IMAGELENGTH);
        var bits = OptionalInt(tiff, TiffTag.BITSPERSAMPLE, 16);
        var samples = OptionalInt(tiff, TiffTag.SAMPLESPERPIXEL, 1);
        var format = (SampleFormat)OptionalInt(tiff, TiffTag.SAMPLEFORMAT, (int)SampleFormat.INT);
        var planar = (PlanarConfig)OptionalInt(tiff, TiffTag.PLANARCONFIG, (int)PlanarConfig.CONTIG);
        var noData = TryReadNoData(tiff);

        if (samples != 1)
            throw new NotSupportedException($"Expected a single-band elevation GeoTIFF, found {samples} bands.");
        if (planar != PlanarConfig.CONTIG)
            throw new NotSupportedException($"Unsupported planar configuration: {planar}.");
        ValidateSampleType(bits, format);

        var tiled = tiff.IsTiled();
        var blockWidth = tiled ? RequiredInt(tiff, TiffTag.TILEWIDTH) : width;
        var blockHeight = tiled ? RequiredInt(tiff, TiffTag.TILELENGTH) : 1;

        Console.WriteLine($"GeoTIFF source: {width} x {height}, {bits}-bit {format}, " +
                          (tiled ? $"tiled {blockWidth} x {blockHeight}" : "scanline/striped"));
        if (noData.HasValue)
            Console.WriteLine($"GeoTIFF NoData: {noData.Value}");
        Console.WriteLine($"Resampling elevation directly to {outputWidth} x {outputHeight}; the full source raster is not loaded into RAM.");

        using IRasterSource source = tiled
            ? new TiledRasterSource(tiff, width, height, blockWidth, blockHeight, bits, format, noData, tileCacheCapacity)
            : new ScanlineRasterSource(tiff, width, height, bits, format, noData);

        var output = new Raster<float>(outputWidth, outputHeight);
        for (var y = 0; y < outputHeight; y++)
        {
            var sy = ((y + 0.5) / outputHeight * height) - 0.5;
            for (var x = 0; x < outputWidth; x++)
            {
                var sx = ((x + 0.5) / outputWidth * width) - 0.5;
                output[x, y] = SampleBilinear(source, sx, sy);
            }

            if ((y + 1) % Math.Max(1, outputHeight / 20) == 0 || y + 1 == outputHeight)
                Console.WriteLine($"Elevation resampling: {(y + 1) * 100 / outputHeight}%");
        }

        var info = new GeoTiffReadInfo(width, height, outputWidth, outputHeight, tiled, blockWidth, blockHeight, bits, format, noData);
        return (output, info);
    }

    private static float SampleBilinear(IRasterSource source, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = x - x0;
        var ty = y - y0;

        var a = source.Get(Wrap(x0, source.Width), Clamp(y0, source.Height));
        var b = source.Get(Wrap(x0 + 1, source.Width), Clamp(y0, source.Height));
        var c = source.Get(Wrap(x0, source.Width), Clamp(y0 + 1, source.Height));
        var d = source.Get(Wrap(x0 + 1, source.Width), Clamp(y0 + 1, source.Height));

        var top = a + (b - a) * tx;
        var bottom = c + (d - c) * tx;
        return (float)(top + (bottom - top) * ty);
    }

    private static int Wrap(int value, int length)
    {
        var result = value % length;
        return result < 0 ? result + length : result;
    }

    private static int Clamp(int value, int length) => Math.Clamp(value, 0, length - 1);

    private static void ValidateSampleType(int bits, SampleFormat format)
    {
        _ = (bits, format) switch
        {
            (16, SampleFormat.INT) => true,
            (16, SampleFormat.UINT) => true,
            (32, SampleFormat.INT) => true,
            (32, SampleFormat.UINT) => true,
            (32, SampleFormat.IEEEFP) => true,
            (64, SampleFormat.IEEEFP) => true,
            _ => throw new NotSupportedException($"Unsupported GeoTIFF sample format: {bits}-bit {format}.")
        };
    }

    private static float Decode(byte[] buffer, int byteOffset, int bits, SampleFormat format, double? noData)
    {
        double value = (bits, format) switch
        {
            (16, SampleFormat.INT) => BitConverter.ToInt16(buffer, byteOffset),
            (16, SampleFormat.UINT) => BitConverter.ToUInt16(buffer, byteOffset),
            (32, SampleFormat.INT) => BitConverter.ToInt32(buffer, byteOffset),
            (32, SampleFormat.UINT) => BitConverter.ToUInt32(buffer, byteOffset),
            (32, SampleFormat.IEEEFP) => BitConverter.ToSingle(buffer, byteOffset),
            (64, SampleFormat.IEEEFP) => BitConverter.ToDouble(buffer, byteOffset),
            _ => throw new NotSupportedException($"Unsupported GeoTIFF sample format: {bits}-bit {format}.")
        };

        if (!double.IsFinite(value) || (noData.HasValue && Math.Abs(value - noData.Value) < 1e-6))
            return 0.0f;
        return (float)value;
    }

    private static double? TryReadNoData(Tiff tiff)
    {
        // GDAL_NODATA is TIFF tag 42113. LibTiff.NET does not expose it as a named enum member.
        var field = tiff.GetField((TiffTag)42113);
        if (field is null || field.Length == 0)
            return null;
        var text = field[0].ToString();
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static int RequiredInt(Tiff tiff, TiffTag tag) =>
        tiff.GetField(tag)?[0].ToInt() ?? throw new InvalidOperationException($"GeoTIFF is missing required tag {tag}.");

    private static int OptionalInt(Tiff tiff, TiffTag tag, int fallback) =>
        tiff.GetField(tag)?[0].ToInt() ?? fallback;

    private interface IRasterSource : IDisposable
    {
        int Width { get; }
        int Height { get; }
        float Get(int x, int y);
    }

    private sealed class TiledRasterSource : IRasterSource
    {
        private readonly Tiff _tiff;
        private readonly int _tileWidth;
        private readonly int _tileHeight;
        private readonly int _bytesPerSample;
        private readonly int _bits;
        private readonly SampleFormat _format;
        private readonly double? _noData;
        private readonly int _capacity;
        private readonly Dictionary<int, CacheEntry> _cache = new();
        private readonly LinkedList<int> _lru = new();

        public TiledRasterSource(Tiff tiff, int width, int height, int tileWidth, int tileHeight,
            int bits, SampleFormat format, double? noData, int capacity)
        {
            _tiff = tiff;
            Width = width;
            Height = height;
            _tileWidth = tileWidth;
            _tileHeight = tileHeight;
            _bytesPerSample = bits / 8;
            _bits = bits;
            _format = format;
            _noData = noData;
            _capacity = Math.Max(16, capacity);
        }

        public int Width { get; }
        public int Height { get; }

        public float Get(int x, int y)
        {
            var tileX = x / _tileWidth;
            var tileY = y / _tileHeight;
            var tileIndex = _tiff.ComputeTile(x, y, 0, 0);
            var buffer = GetTile(tileIndex);
            var localX = x - tileX * _tileWidth;
            var localY = y - tileY * _tileHeight;
            var offset = checked((localY * _tileWidth + localX) * _bytesPerSample);
            return Decode(buffer, offset, _bits, _format, _noData);
        }

        private byte[] GetTile(int tileIndex)
        {
            if (_cache.TryGetValue(tileIndex, out var cached))
            {
                _lru.Remove(cached.Node);
                _lru.AddFirst(cached.Node);
                return cached.Buffer;
            }

            var buffer = new byte[_tiff.TileSize()];
            var bytesRead = _tiff.ReadEncodedTile(tileIndex, buffer, 0, buffer.Length);
            if (bytesRead < 0)
                throw new InvalidOperationException($"Unable to read GeoTIFF tile {tileIndex}.");

            var node = _lru.AddFirst(tileIndex);
            _cache[tileIndex] = new CacheEntry(buffer, node);
            while (_cache.Count > _capacity)
            {
                var last = _lru.Last!;
                _cache.Remove(last.Value);
                _lru.RemoveLast();
            }
            return buffer;
        }

        public void Dispose()
        {
            _cache.Clear();
            _lru.Clear();
        }

        private sealed record CacheEntry(byte[] Buffer, LinkedListNode<int> Node);
    }

    private sealed class ScanlineRasterSource : IRasterSource
    {
        private readonly Tiff _tiff;
        private readonly int _bytesPerSample;
        private readonly int _bits;
        private readonly SampleFormat _format;
        private readonly double? _noData;
        private readonly Dictionary<int, byte[]> _lines = new();
        private readonly Queue<int> _lineOrder = new();

        public ScanlineRasterSource(Tiff tiff, int width, int height, int bits, SampleFormat format, double? noData)
        {
            _tiff = tiff;
            Width = width;
            Height = height;
            _bytesPerSample = bits / 8;
            _bits = bits;
            _format = format;
            _noData = noData;
        }

        public int Width { get; }
        public int Height { get; }

        public float Get(int x, int y)
        {
            if (!_lines.TryGetValue(y, out var line))
            {
                line = new byte[_tiff.ScanlineSize()];
                if (!_tiff.ReadScanline(line, y))
                    throw new InvalidOperationException($"Unable to read GeoTIFF scanline {y}.");
                _lines[y] = line;
                _lineOrder.Enqueue(y);
                while (_lineOrder.Count > 4)
                    _lines.Remove(_lineOrder.Dequeue());
            }
            return Decode(line, checked(x * _bytesPerSample), _bits, _format, _noData);
        }

        public void Dispose()
        {
            _lines.Clear();
            _lineOrder.Clear();
        }
    }
}
