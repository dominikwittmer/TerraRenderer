using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.Atmosphere;

internal static class EarthAtmosphere
{
    public static SKColor OuterGlow(double normalizedAltitude, double edgeLight, AtmosphereConfiguration config)
    {
        var altitude = Math.Clamp(normalizedAltitude, 0.0, 1.0);
        var shell = Math.Pow(1.0 - altitude, config.RadialFalloff);
        var daylight = config.NightSideStrength +
                       (1.0 - config.NightSideStrength) * SmoothStep(-0.30, 0.52, edgeLight);
        var terminator = Math.Exp(-Math.Pow(edgeLight / config.TerminatorWidth, 2.0));
        var forward = Math.Pow(SmoothStep(-0.10, 0.86, edgeLight), 2.0);

        var horizon = shell * config.HorizonGlowStrength;
        var rayleigh = shell * daylight *
                       (1.0 + config.TerminatorBoost * terminator) * config.RayleighStrength;
        var mie = Math.Pow(shell, 4.2) * forward * config.MieStrength;
        var sunset = shell * terminator * SmoothStep(-0.20, 0.20, edgeLight) *
                     config.SunsetWarmth * config.SunsetGlowStrength;
        var night = Math.Pow(shell, 2.2) * (1.0 - daylight) * config.NightLimbStrength;

        return new SKColor(
            ToByte(116 * rayleigh + 185 * mie + 58 * horizon + 255 * sunset + 28 * night),
            ToByte(174 * rayleigh + 190 * mie + 105 * horizon + 112 * sunset + 46 * night),
            ToByte(242 * rayleigh + 202 * mie + 188 * horizon + 28 * sunset + 92 * night), 255);
    }

    public static SKColor ApplySurfaceHaze(SKColor color, double sphereZ, double surfaceLight,
        AtmosphereConfiguration config)
    {
        var mu = Math.Clamp(sphereZ, 0.0, 1.0);
        var limb = Math.Pow(1.0 - mu, 3.15);
        var daylight = config.NightSideStrength +
                       (1.0 - config.NightSideStrength) * SmoothStep(-0.28, 0.54, surfaceLight);
        var terminator = Math.Exp(-Math.Pow(surfaceLight / config.TerminatorWidth, 2.0));

        var dayAmount = limb * daylight * config.SurfaceHazeStrength;
        var limbAmount = limb * config.LimbStrength * (0.30 + 0.70 * daylight);
        var nightAmount = limb * (1.0 - daylight) * config.NightLimbStrength;
        var amount = Math.Clamp(dayAmount + limbAmount + nightAmount, 0.0, 0.28);

        var sunset = terminator * config.SunsetWarmth * config.SunsetGlowStrength;
        var haze = new SKColor(
            ToByte(142 + 103 * sunset),
            ToByte(188 + 46 * sunset),
            ToByte(238 - 72 * sunset), 255);

        return ScreenMix(color, haze, amount);
    }

    private static SKColor ScreenMix(SKColor baseColor, SKColor haze, double amount)
    {
        static double Screen(double a, double b) => 255.0 - (255.0 - a) * (255.0 - b) / 255.0;
        return new SKColor(
            ToByte(baseColor.Red + (Screen(baseColor.Red, haze.Red) - baseColor.Red) * amount),
            ToByte(baseColor.Green + (Screen(baseColor.Green, haze.Green) - baseColor.Green) * amount),
            ToByte(baseColor.Blue + (Screen(baseColor.Blue, haze.Blue) - baseColor.Blue) * amount), 255);
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
