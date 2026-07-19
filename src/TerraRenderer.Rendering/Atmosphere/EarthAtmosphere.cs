using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Atmosphere;

internal static class EarthAtmosphere
{
    public static HdrColor OuterGlow(double normalizedAltitude, double edgeLight, AtmosphereConfiguration config)
    {
        var altitude = Math.Clamp(normalizedAltitude, 0.0, 1.0);
        var shell = Math.Pow(1.0 - altitude, Math.Max(1.0, config.RadialFalloff));

        // Keep the limb predominantly blue. Warm scattering is restricted to a narrow
        // sunlit terminator and never wraps around the complete disc.
        var daylight = SmoothStep(-0.18, 0.48, edgeLight);
        var terminator = Math.Exp(-Math.Pow((edgeLight + 0.025) / Math.Max(0.055, config.TerminatorWidth * 0.42), 2.0));
        var sunward = SmoothStep(0.0, 0.88, edgeLight);
        var night = 1.0 - daylight;

        var rayleigh = shell * config.RayleighStrength * (0.025 + 0.975 * daylight);
        var horizon = shell * config.HorizonGlowStrength * (0.04 + 0.96 * daylight);
        var mie = Math.Pow(shell, 2.8) * Math.Pow(sunward, Math.Max(2.0, config.ForwardScatterPower))
            * config.MieStrength * config.ForwardScatterStrength;
        var warm = Math.Pow(shell, 2.2) * terminator * config.SunsetGlowStrength
            * config.GoldenHourStrength * config.SunsetWarmth;
        var nightRim = Math.Pow(shell, 3.0) * night * config.NightLimbStrength;

        return new HdrColor(
            (float)(0.055 * rayleigh + 0.04 * horizon + 1.20 * mie + 0.92 * warm + 0.004 * nightRim),
            (float)(0.24 * rayleigh + 0.16 * horizon + 1.05 * mie + 0.38 * warm + 0.012 * nightRim),
            (float)(1.05 * rayleigh + 0.54 * horizon + 0.88 * mie + 0.055 * warm + 0.055 * nightRim));
    }

    public static HdrColor ApplySurfaceHaze(HdrColor color, double sphereZ, double surfaceLight,
        AtmosphereConfiguration config)
    {
        var mu = Math.Clamp(sphereZ, 0.0, 1.0);
        var limb = Math.Pow(1.0 - mu, 3.4);
        var daylight = SmoothStep(-0.18, 0.48, surfaceLight);
        var terminator = Math.Exp(-Math.Pow((surfaceLight + 0.02) /
            Math.Max(0.06, config.TerminatorWidth * 0.48), 2.0));
        var night = 1.0 - daylight;

        var blue = limb * daylight * (config.SurfaceHazeStrength + 0.55 * config.LimbStrength);
        var warm = limb * terminator * config.SunsetGlowStrength * config.GoldenHourStrength
            * config.SunsetWarmth;
        var nightBlue = limb * night * config.NightLimbStrength;

        // Small extinction gives depth without washing out the surface.
        var extinction = Math.Clamp(blue * 0.075 + warm * 0.025, 0.0, 0.12);
        color *= 1.0 - extinction;
        color += new HdrColor(
            (float)(0.035 * blue + 0.62 * warm + 0.003 * nightBlue),
            (float)(0.16 * blue + 0.24 * warm + 0.009 * nightBlue),
            (float)(0.66 * blue + 0.035 * warm + 0.040 * nightBlue));
        return color;
    }

    private static double SmoothStep(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / Math.Max(1e-9, b - a), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
