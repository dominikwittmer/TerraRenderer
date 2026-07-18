namespace TerraRenderer.AssetBuilder;

internal sealed class Raster<T>(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public T[] Data { get; } = new T[checked(width * height)];

    public ref T this[int x, int y] => ref Data[checked(y * Width + x)];

    public T SampleWrappedBilinear(double x, double y)
    {
        x %= Width;
        if (x < 0) x += Width;
        y = Math.Clamp(y, 0.0, Height - 1.0);

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = (x0 + 1) % Width;
        var y1 = Math.Min(y0 + 1, Height - 1);
        var tx = x - x0;
        var ty = y - y0;

        if (typeof(T) != typeof(float))
            throw new NotSupportedException("Bilinear sampling is currently implemented for float rasters only.");

        var a = (float)(object)this[x0, y0]!;
        var b = (float)(object)this[x1, y0]!;
        var c = (float)(object)this[x0, y1]!;
        var d = (float)(object)this[x1, y1]!;
        var top = a + (b - a) * tx;
        var bottom = c + (d - c) * tx;
        return (T)(object)(float)(top + (bottom - top) * ty);
    }
}
