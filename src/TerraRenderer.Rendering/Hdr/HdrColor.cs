using SkiaSharp;

namespace TerraRenderer.Rendering.Hdr;

internal readonly record struct HdrColor(float R, float G, float B)
{
    public static readonly HdrColor Black = new(0, 0, 0);

    public double Luminance => 0.2126 * R + 0.7152 * G + 0.0722 * B;

    public static HdrColor FromSrgb(SKColor color) => new(
        (float)ToLinear(color.Red / 255.0),
        (float)ToLinear(color.Green / 255.0),
        (float)ToLinear(color.Blue / 255.0));

    public HdrColor ClampMinimum(float minimum = 0) => new(
        Math.Max(minimum, R), Math.Max(minimum, G), Math.Max(minimum, B));

    public static HdrColor operator +(HdrColor a, HdrColor b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static HdrColor operator *(HdrColor a, double factor) => new((float)(a.R * factor), (float)(a.G * factor), (float)(a.B * factor));
    public static HdrColor operator *(HdrColor a, HdrColor b) => new(a.R * b.R, a.G * b.G, a.B * b.B);

    private static double ToLinear(double x) =>
        x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
}
