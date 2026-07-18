using SkiaSharp;

namespace TerraRenderer.Assets;

internal sealed class EquirectangularSampler(SKBitmap bitmap)
{
    public SKColor Sample(double latitudeDegrees, double longitudeDegrees)
    {
        var u = Wrap(longitudeDegrees / 360.0 + 0.5);
        var v = Math.Clamp(0.5 - latitudeDegrees / 180.0, 0.0, 1.0);
        var fx = u * (bitmap.Width - 1);
        var fy = v * (bitmap.Height - 1);
        var x0 = (int)Math.Floor(fx);
        var y0 = (int)Math.Floor(fy);
        var x1 = (x0 + 1) % bitmap.Width;
        var y1 = Math.Min(bitmap.Height - 1, y0 + 1);
        var tx = fx - x0;
        var ty = fy - y0;
        var c00 = bitmap.GetPixel(x0, y0);
        var c10 = bitmap.GetPixel(x1, y0);
        var c01 = bitmap.GetPixel(x0, y1);
        var c11 = bitmap.GetPixel(x1, y1);
        return new SKColor(
            Blend(c00.Red, c10.Red, c01.Red, c11.Red, tx, ty),
            Blend(c00.Green, c10.Green, c01.Green, c11.Green, tx, ty),
            Blend(c00.Blue, c10.Blue, c01.Blue, c11.Blue, tx, ty), 255);
    }

    private static byte Blend(byte c00, byte c10, byte c01, byte c11, double tx, double ty)
    {
        var top = c00 + (c10 - c00) * tx;
        var bottom = c01 + (c11 - c01) * tx;
        return (byte)Math.Clamp(Math.Round(top + (bottom - top) * ty), 0, 255);
    }

    private static double Wrap(double value)
    {
        value -= Math.Floor(value);
        return value < 0 ? value + 1 : value;
    }
}
