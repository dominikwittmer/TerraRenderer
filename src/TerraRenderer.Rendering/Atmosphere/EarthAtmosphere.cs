using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.Atmosphere;

internal static class EarthAtmosphere
{
    public static SKColor OuterGlow(double normalizedAltitude, double edgeLight, AtmosphereConfiguration config)
    {
        var altitude = Math.Clamp(normalizedAltitude, 0.0, 1.0);

        // Narrow white-blue rim with a fast radial falloff. Night-side scattering is nearly absent.
        var shell = Math.Pow(1.0 - altitude, config.RadialFalloff);
        var daylight = config.NightSideStrength +
                       (1.0 - config.NightSideStrength) * SmoothStep(-0.24, 0.52, edgeLight);
        var terminator = Math.Exp(-Math.Pow(edgeLight / config.TerminatorWidth, 2.0));
        var forward = Math.Pow(SmoothStep(-0.06, 0.82, edgeLight), 2.15);

        var rayleigh = shell * daylight *
                       (1.0 + config.TerminatorBoost * terminator) * config.RayleighStrength;
        var mie = Math.Pow(shell, 4.8) * forward * config.MieStrength;
        var warm = terminator * SmoothStep(-0.18, 0.18, edgeLight) * config.SunsetWarmth;

        return new SKColor(
            ToByte(142 * rayleigh + 177 * mie + 70 * warm),
            ToByte(188 * rayleigh + 185 * mie + 34 * warm),
            ToByte(235 * rayleigh + 194 * mie + 8 * warm), 255);
    }

    public static SKColor ApplySurfaceHaze(SKColor color, double sphereZ, double surfaceLight,
        AtmosphereConfiguration config)
    {
        var mu = Math.Clamp(sphereZ, 0.0, 1.0);
        var limb = Math.Pow(1.0 - mu, 3.65);
        var daylight = config.NightSideStrength +
                       (1.0 - config.NightSideStrength) * SmoothStep(-0.24, 0.54, surfaceLight);
        var terminator = Math.Exp(-Math.Pow(surfaceLight / config.TerminatorWidth, 2.0));
        var amount = Math.Clamp(
            limb * daylight * (1.0 + config.TerminatorBoost * 0.24 * terminator) * config.SurfaceHazeStrength,
            0.0, 0.20);

        var warm = terminator * config.SunsetWarmth * 0.22;
        var haze = new SKColor(
            ToByte(156 + 58 * warm),
            ToByte(194 + 20 * warm),
            ToByte(232 - 52 * warm), 255);
        return Mix(color, haze, amount);
    }

    private static SKColor Mix(SKColor a, SKColor b, double amount) => new(
        ToByte(a.Red + (b.Red - a.Red) * amount),
        ToByte(a.Green + (b.Green - a.Green) * amount),
        ToByte(a.Blue + (b.Blue - a.Blue) * amount), 255);

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
