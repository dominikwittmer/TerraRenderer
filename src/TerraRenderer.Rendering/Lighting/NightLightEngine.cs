using SkiaSharp;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Lighting;

/// <summary>
/// Converts the night-light and pre-blurred emission maps into linear HDR radiance.
/// The source maps are treated as light measurements, never as display colours.
/// </summary>
internal static class NightLightEngine
{
    public static HdrColor Evaluate(
        SKColor emission,
        SKColor emissionGlow,
        double geometricIllumination,
        RenderingConfiguration config)
    {
        if (!config.EnableNightLights)
            return HdrColor.Black;

        // Lights are fully visible only on the dark side. The smooth transition avoids
        // a hard terminator while preventing lights from leaking far into daylight.
        var darkness = 1.0 - SmoothStep(
            -config.NightLightFadeWidth,
            config.NightLightFadeWidth,
            geometricIllumination);
        darkness = Math.Pow(Math.Clamp(darkness, 0.0, 1.0), config.NightLightFadePower);
        if (darkness < 0.0001)
            return HdrColor.Black;

        // Use luminance only. Colour in Black Marble style assets is often an artistic
        // tint and must not turn roads or coastlines green/red in the HDR buffer.
        var source = PerceptualLuminance(emission);
        var blurred = PerceptualLuminance(emissionGlow);

        // Reject sensor/background contamination. This is deliberately a soft black
        // point so dim settlements remain visible without turning whole countries bright.
        var coreSignal = SmoothStep(0.028, 0.62, source);
        var glowSignal = SmoothStep(0.020, 0.46, blurred);
        if (coreSignal <= 0.0 && glowSignal <= 0.0)
            return HdrColor.Black;

        // Preserve hierarchy: villages stay dim, cities become bright and only the
        // densest centres exceed the bloom threshold by a large margin.
        var compression = Math.Clamp(config.NightCompression, 0.55, 1.25);
        var core = Math.Pow(coreSignal, 2.4 / compression);
        var mid = Math.Pow(coreSignal, 1.15 / compression);
        var halo = Math.Pow(glowSignal, 1.55) * (1.0 - 0.42 * coreSignal);

        var baseStrength = config.NightLightStrength * darkness;
        var coreRadiance = core * config.NightCoreStrength * baseStrength * 8.0;
        var midRadiance = mid * baseStrength * 0.82;
        var haloRadiance = halo * config.NightLightGlow * config.NightHaloStrength * baseStrength * 0.34;

        // Dense cores approach neutral warm white. Midtones and halos remain amber.
        var white = Math.Clamp(config.NightWhiteCore, 0.0, 1.0);
        var warmth = Math.Clamp(config.NightWarmth, 0.0, 1.0);

        var coreColor = new HdrColor(
            1.00f,
            (float)(0.88 + 0.12 * white),
            (float)(0.67 + 0.30 * white));
        var midColor = new HdrColor(
            1.00f,
            (float)(0.69 + 0.10 * white),
            (float)(0.33 + 0.11 * white));
        var haloColor = new HdrColor(
            1.00f,
            (float)(0.55 + 0.10 * white),
            (float)(0.20 + 0.08 * white));

        // Warmth affects only the broad components, never the white city core.
        midColor *= 0.92 + 0.12 * warmth;
        haloColor *= 0.86 + 0.18 * warmth;

        return (coreColor * coreRadiance + midColor * midRadiance + haloColor * haloRadiance)
            .ClampMinimum();
    }

    private static double PerceptualLuminance(SKColor color)
    {
        // Night-light source assets are display encoded. For thresholding, perceptual
        // luminance is more stable than converting each channel to linear first.
        return (0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / Math.Max(1e-9, edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
