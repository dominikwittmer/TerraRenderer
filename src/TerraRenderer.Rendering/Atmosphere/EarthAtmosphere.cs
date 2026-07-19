using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Atmosphere;

internal static class EarthAtmosphere
{
    public static HdrColor OuterGlow(double normalizedAltitude, double edgeLight, AtmosphereConfiguration config)
    {
        var altitude = Math.Clamp(normalizedAltitude, 0.0, 1.0);
        var shell = Math.Pow(1.0 - altitude, config.RadialFalloff);
        var day = SmoothStep(-0.26, 0.48, edgeLight);
        var terminator = Math.Exp(-Math.Pow(edgeLight / Math.Max(0.035, config.TerminatorWidth), 2.0));
        var sunward = SmoothStep(-0.08, 0.96, edgeLight);
        var forward = Math.Pow(sunward, Math.Max(1.0, config.ForwardScatterPower));
        var night = 1.0 - day;

        var rayleigh = shell * (config.NightSideStrength + day) * (1.0 + config.TerminatorBoost * terminator) * config.RayleighStrength;
        var mie = Math.Pow(shell, 3.2) * forward * config.MieStrength * (1.0 + config.ForwardScatterStrength);
        var horizon = shell * config.HorizonGlowStrength * (0.12 + 0.88 * day);
        var sunset = shell * terminator * SmoothStep(-0.28, 0.14, edgeLight) * config.SunsetWarmth * config.SunsetGlowStrength * config.GoldenHourStrength;
        var purple = shell * terminator * night * config.TwilightPurpleStrength;
        var nightRim = Math.Pow(shell, 2.6) * night * config.NightLimbStrength;

        return new HdrColor(
            (float)(0.18 * rayleigh + 2.9 * mie + 0.08 * horizon + 3.4 * sunset + 0.24 * purple + 0.015 * nightRim),
            (float)(0.48 * rayleigh + 2.2 * mie + 0.22 * horizon + 1.25 * sunset + 0.12 * purple + 0.035 * nightRim),
            (float)(1.45 * rayleigh + 1.4 * mie + 0.65 * horizon + 0.09 * sunset + 0.42 * purple + 0.12 * nightRim));
    }

    public static HdrColor ApplySurfaceHaze(HdrColor color, double sphereZ, double surfaceLight, AtmosphereConfiguration config)
    {
        var mu = Math.Clamp(sphereZ, 0.0, 1.0);
        var limb = Math.Pow(1.0 - mu, 3.0);
        var day = SmoothStep(-0.28, 0.52, surfaceLight);
        var night = 1.0 - day;
        var terminator = Math.Exp(-Math.Pow(surfaceLight / Math.Max(0.04, config.TerminatorWidth), 2.0));
        var forward = Math.Pow(SmoothStep(-0.06, 0.94, surfaceLight), Math.Max(1.0, config.ForwardScatterPower));

        var blue = limb * (0.18 + 0.82 * day) * (config.SurfaceHazeStrength + config.LimbStrength);
        var warm = limb * terminator * config.SunsetGlowStrength * config.GoldenHourStrength * (0.55 + 0.45 * SmoothStep(-0.25, 0.15, surfaceLight));
        var mie = limb * forward * config.MieStrength * config.ForwardScatterStrength;
        var purple = limb * terminator * night * config.TwilightPurpleStrength;
        var nightBlue = limb * night * config.NightLimbStrength;

        var extinction = Math.Clamp(blue * 0.10 + warm * 0.05, 0.0, 0.22);
        color *= 1.0 - extinction;
        color += new HdrColor(
            (float)(0.08 * blue + 1.85 * warm + 1.05 * mie + 0.20 * purple + 0.010 * nightBlue),
            (float)(0.24 * blue + 0.72 * warm + 0.88 * mie + 0.12 * purple + 0.025 * nightBlue),
            (float)(0.82 * blue + 0.08 * warm + 0.58 * mie + 0.38 * purple + 0.085 * nightBlue));
        return color;
    }

    private static double SmoothStep(double a, double b, double v) { var t = Math.Clamp((v-a)/(b-a),0,1); return t*t*(3-2*t); }
}
