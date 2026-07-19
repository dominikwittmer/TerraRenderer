using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.ToneMapping;

internal static class EarthToneMapper
{
    // Material-aware grading happens before the frame-wide HDR pass. Exposure,
    // highlight roll-off and display conversion are intentionally deferred to
    // EarthPostProcessor so bloom and ACES operate on the complete image.
    public static SKColor ApplySurfaceGrade(
        SKColor color,
        EarthSurfaceMaterial material,
        ToneMappingConfiguration config)
    {
        var r = ToLinear(color.Red / 255.0);
        var g = ToLinear(color.Green / 255.0);
        var b = ToLinear(color.Blue / 255.0);

        var luminance = Luminance(r, g, b);
        var shadowLift = config.ShadowLift * Math.Pow(1.0 - Math.Clamp(luminance, 0.0, 1.0), 2.2);
        r += shadowLift * 0.78;
        g += shadowLift * 0.90;
        b += shadowLift * 1.12;

        luminance = Luminance(r, g, b);
        r = luminance + (r - luminance) * config.Saturation;
        g = luminance + (g - luminance) * config.Saturation;
        b = luminance + (b - luminance) * config.Saturation;

        if (material.Water > 0.20)
        {
            var water = material.Water;
            var boost = config.OceanBlueBoost * water;
            var darkening = config.OceanDarkening * water;
            r *= 1.0 - darkening - 0.52 * boost;
            g *= 1.0 - 0.26 * darkening - 0.10 * boost;
            b *= 1.0 - 0.05 * darkening + 0.82 * boost;
        }
        else
        {
            var albedo = material.Albedo;
            var greenSignal = Math.Max(0.0, (albedo.Green - (albedo.Red + albedo.Blue) * 0.5) / 255.0);
            var desertSignal = Math.Max(0.0, (albedo.Red - albedo.Blue) / 255.0) *
                               Math.Max(0.0, (albedo.Green - albedo.Blue) / 255.0);

            var vegetation = config.VegetationBoost * greenSignal;
            r *= 1.0 - 0.20 * vegetation;
            g *= 1.0 + vegetation;
            b *= 1.0 - 0.28 * vegetation;

            var desert = config.DesertWarmth * desertSignal;
            r *= 1.0 + desert;
            g *= 1.0 + 0.34 * desert;
            b *= 1.0 - 0.50 * desert;
        }

        if (material.Ice > 0.05)
        {
            var ice = material.Ice;
            var highlight = Math.Max(r, Math.Max(g, b));
            var compression = 1.0 /
                              (1.0 + config.IceHighlightCompression * ice * Math.Max(0.0, highlight - 0.40) * 4.2);
            var suppression = 1.0 - config.PolarSuppression * ice;
            r *= compression * suppression * 0.985;
            g *= compression * suppression;
            b = b * compression * suppression + config.IceBlueShadow * ice * (1.0 - luminance);
        }

        return new SKColor(
            ToByte(ToSrgb(r) * 255.0),
            ToByte(ToSrgb(g) * 255.0),
            ToByte(ToSrgb(b) * 255.0),
            color.Alpha);
    }

    private static double Luminance(double r, double g, double b) =>
        0.2126 * r + 0.7152 * g + 0.0722 * b;

    private static double ToLinear(double x) =>
        x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double x) =>
        x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(Math.Max(0.0, x), 1.0 / 2.4) - 0.055;

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
