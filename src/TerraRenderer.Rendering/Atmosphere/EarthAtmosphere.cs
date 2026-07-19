using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.Atmosphere;

internal static class EarthAtmosphere
{
    public static SKColor OuterGlow(double normalizedAltitude, double edgeLight, AtmosphereConfiguration config)
    {
        var altitude = Math.Clamp(normalizedAltitude, 0.0, 1.0);
        var shell = Math.Pow(1.0 - altitude, config.RadialFalloff);

        var day = SmoothStep(-0.34, 0.56, edgeLight);
        var terminator = Math.Exp(-Math.Pow(edgeLight / Math.Max(0.04, config.TerminatorWidth), 2.0));
        var sunward = SmoothStep(-0.12, 0.90, edgeLight);
        var forward = Math.Pow(sunward, Math.Max(1.0, config.ForwardScatterPower));
        var night = 1.0 - day;

        var rayleigh = shell * (config.NightSideStrength + day)
            * (1.0 + config.TerminatorBoost * terminator) * config.RayleighStrength;
        var mie = Math.Pow(shell, 3.7) * forward
            * config.MieStrength * (1.0 + config.ForwardScatterStrength);
        var horizon = shell * config.HorizonGlowStrength * (0.24 + 0.76 * day);
        var sunset = shell * terminator * SmoothStep(-0.24, 0.18, edgeLight)
            * config.SunsetWarmth * config.SunsetGlowStrength * config.GoldenHourStrength;
        var purple = shell * terminator * night * config.TwilightPurpleStrength;
        var nightRim = Math.Pow(shell, 2.3) * night * config.NightLimbStrength;

        return new SKColor(
            ToByte(104 * rayleigh + 244 * mie + 66 * horizon + 255 * sunset + 96 * purple + 24 * nightRim),
            ToByte(168 * rayleigh + 214 * mie + 112 * horizon + 132 * sunset + 64 * purple + 46 * nightRim),
            ToByte(255 * rayleigh + 190 * mie + 206 * horizon + 28 * sunset + 138 * purple + 104 * nightRim),
            255);
    }

    public static SKColor ApplySurfaceHaze(
        SKColor color,
        double sphereZ,
        double surfaceLight,
        AtmosphereConfiguration config)
    {
        var mu = Math.Clamp(sphereZ, 0.0, 1.0);
        var limb = Math.Pow(1.0 - mu, 3.0);
        var day = SmoothStep(-0.30, 0.54, surfaceLight);
        var night = 1.0 - day;
        var terminator = Math.Exp(-Math.Pow(surfaceLight / Math.Max(0.04, config.TerminatorWidth), 2.0));
        var forward = Math.Pow(SmoothStep(-0.08, 0.92, surfaceLight), Math.Max(1.0, config.ForwardScatterPower));

        var blueAmount = limb * (0.28 + 0.72 * day)
            * (config.SurfaceHazeStrength + config.LimbStrength);
        var warmAmount = limb * terminator * config.SunsetGlowStrength
            * config.GoldenHourStrength * (0.55 + 0.45 * SmoothStep(-0.25, 0.15, surfaceLight));
        var forwardAmount = limb * forward * config.MieStrength * config.ForwardScatterStrength;
        var purpleAmount = limb * terminator * night * config.TwilightPurpleStrength;
        var nightAmount = limb * night * config.NightLimbStrength;

        var blueHaze = new SKColor(116, 184, 255, 255);
        var warmHaze = new SKColor(255, 151, 52, 255);
        var forwardHaze = new SKColor(255, 222, 176, 255);
        var purpleHaze = new SKColor(116, 74, 170, 255);
        var nightHaze = new SKColor(34, 78, 146, 255);

        var result = ScreenMix(color, blueHaze, Math.Clamp(blueAmount, 0.0, 0.24));
        result = ScreenMix(result, warmHaze, Math.Clamp(warmAmount, 0.0, 0.30));
        result = ScreenMix(result, forwardHaze, Math.Clamp(forwardAmount, 0.0, 0.18));
        result = ScreenMix(result, purpleHaze, Math.Clamp(purpleAmount, 0.0, 0.12));
        return ScreenMix(result, nightHaze, Math.Clamp(nightAmount, 0.0, 0.08));
    }

    private static SKColor ScreenMix(SKColor baseColor, SKColor haze, double amount)
    {
        static double Screen(double a, double b) => 255.0 - (255.0 - a) * (255.0 - b) / 255.0;
        return new SKColor(
            ToByte(baseColor.Red + (Screen(baseColor.Red, haze.Red) - baseColor.Red) * amount),
            ToByte(baseColor.Green + (Screen(baseColor.Green, haze.Green) - baseColor.Green) * amount),
            ToByte(baseColor.Blue + (Screen(baseColor.Blue, haze.Blue) - baseColor.Blue) * amount),
            255);
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
