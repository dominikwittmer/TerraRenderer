using SkiaSharp;
using TerraRenderer.Assets;

namespace TerraRenderer.Rendering.Materials;

/// <summary>
/// Applies a restrained, material-aware colour reconstruction before lighting.
/// The source texture remains the detail carrier; this pass only corrects the
/// large-scale spectral response of water, vegetation, desert, rock and snow.
/// </summary>
internal static class SurfaceMaterialShading
{
    public static EarthSurfaceMaterial Apply(EarthSurfaceMaterial source, TerrainMaterial terrain)
    {
        var linear = ToLinear(source.Albedo);
        var r = linear.R;
        var g = linear.G;
        var b = linear.B;

        var water = SmoothStep(0.10, 0.72, terrain.Water);
        var vegetation = terrain.Vegetation * (1.0 - water);
        var desert = terrain.Desert * (1.0 - water);
        var rock = terrain.Rock * (1.0 - water);
        var snowIce = Math.Clamp(Math.Max(terrain.Snow, terrain.Ice), 0.0, 1.0);

        if (water > 0.001)
        {
            // Preserve texture detail while pulling cyan satellite imagery towards
            // a deeper, neutral orbital blue. Brighter source water acts as shelf water.
            var luminance = Luminance(r, g, b);
            var shelf = SmoothStep(0.075, 0.30, luminance) * (1.0 - snowIce);
            var deep = 1.0 - shelf;
            var targetR = 0.010 + 0.020 * shelf;
            var targetG = 0.035 + 0.085 * shelf;
            var targetB = 0.105 + 0.155 * shelf;
            var blend = water * (0.34 + 0.20 * deep);
            r = Lerp(r, targetR, blend);
            g = Lerp(g, targetG, blend);
            b = Lerp(b, targetB, blend);
        }
        else
        {
            // Vegetation receives more green separation without neon saturation.
            r *= 1.0 - 0.08 * vegetation;
            g *= 1.0 + 0.13 * vegetation;
            b *= 1.0 - 0.10 * vegetation;

            // Desert imagery is commonly too orange. Lift neutral mineral tones and
            // slightly reduce the red/blue separation.
            var desertNeutral = Luminance(r, g, b);
            r = Lerp(r, desertNeutral * 1.06, 0.18 * desert);
            g = Lerp(g, desertNeutral * 1.00, 0.14 * desert);
            b = Lerp(b, desertNeutral * 0.83, 0.10 * desert);

            // Exposed high rock becomes cooler and more neutral, improving mountain detail.
            var highRock = rock * SmoothStep(0.35, 0.82, terrain.Height);
            var rockNeutral = Luminance(r, g, b);
            r = Lerp(r, rockNeutral * 0.98, 0.20 * highRock);
            g = Lerp(g, rockNeutral * 1.00, 0.20 * highRock);
            b = Lerp(b, rockNeutral * 1.035, 0.20 * highRock);
        }

        if (snowIce > 0.001)
        {
            var neutral = Luminance(r, g, b);
            var blend = 0.44 * snowIce;
            r = Lerp(r, neutral * 0.985, blend);
            g = Lerp(g, neutral * 1.005, blend);
            b = Lerp(b, neutral * 1.035, blend);
        }

        return source with { Albedo = ToSrgb(r, g, b) };
    }

    private static (double R, double G, double B) ToLinear(SKColor color) =>
        (SrgbToLinear(color.Red / 255.0), SrgbToLinear(color.Green / 255.0), SrgbToLinear(color.Blue / 255.0));

    private static SKColor ToSrgb(double r, double g, double b) => new(
        ToByte(LinearToSrgb(Math.Max(0.0, r)) * 255.0),
        ToByte(LinearToSrgb(Math.Max(0.0, g)) * 255.0),
        ToByte(LinearToSrgb(Math.Max(0.0, b)) * 255.0),
        255);

    private static double SrgbToLinear(double value) =>
        value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

    private static double LinearToSrgb(double value) =>
        value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    private static double Luminance(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
    private static double SmoothStep(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / Math.Max(1e-9, b - a), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
