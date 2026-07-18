using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Lighting;

internal static class SurfaceLighting
{
    public static SKColor Shade(EarthSurfaceMaterial material, SKColor emissionGlow, Vector3d normal,
        Vector3d geometricNormal, Vector3d sun, Vector3d view, RenderingConfiguration config)
    {
        var illumination = Vector3d.Dot(normal, sun);
        var geometricIllumination = Vector3d.Dot(geometricNormal, sun);

        // A broad perceptual terminator. The cubic smoothstep avoids the visibly straight,
        // high-contrast boundary produced by multiplying the whole day image by a narrow mask.
        var dayMix = SmoothStep(-config.TwilightSoftness, config.TwilightSoftness, geometricIllumination);
        var directLight = Math.Pow(Math.Max(0.0, illumination), config.DiffusePower);
        var skyHemisphere = 0.5 + 0.5 * geometricNormal.Z;
        var hemisphere = config.HemisphereLight * (0.62 + 0.38 * skyHemisphere);
        var baseLight = config.AmbientLight + hemisphere;
        var diffuse = baseLight + (1.0 - baseLight) * directLight;

        // Keep a small amount of blue-grey surface information inside the twilight band.
        // This is deliberately perceptual: on a 466 px OLED a fully black transition looks hard.
        var twilightBand = Math.Exp(-Math.Pow(geometricIllumination / config.TwilightBandWidth, 2.0));
        var twilightLift = config.TwilightSurfaceLift * twilightBand * (1.0 - 0.72 * dayMix);
        var ao = 1.0 - config.AmbientOcclusionStrength * (1.0 - material.AmbientOcclusion);
        var day = Scale(material.Albedo, diffuse * (0.18 + 0.82 * dayMix) * ao);
        day = Add(day,
            24.0 * twilightLift * (1.0 - material.Water),
            37.0 * twilightLift,
            58.0 * twilightLift);

        if (config.EnableOceanSpecular && material.Water > 0.05 && illumination > 0.0)
        {
            var halfVector = (sun + view).Normalize();
            var ndoth = Math.Max(0.0, Vector3d.Dot(normal, halfVector));
            var specular = Math.Pow(ndoth, config.OceanSpecularPower);
            var ndotv = Math.Max(0.0, Vector3d.Dot(normal, view));
            var fresnel = 0.014 + config.OceanFresnelStrength * Math.Pow(1.0 - ndotv, 5.0);
            var strength = (specular * config.OceanSpecularStrength + fresnel * 0.11) *
                           dayMix * material.Water * Math.Max(0.0, illumination);
            day = Add(day, 136 * strength, 178 * strength, 232 * strength);
        }

        var nightMask = config.EnableNightLights
            ? Math.Pow(1.0 - SmoothStep(-config.NightLightFadeWidth, config.NightLightFadeWidth,
                geometricIllumination), config.NightLightFadePower)
            : 0.0;
        var glowSource = material.Bloom.Red > 0 || material.Bloom.Green > 0 || material.Bloom.Blue > 0
            ? material.Bloom
            : emissionGlow;
        var night = WarmEmission(material.Emission, glowSource,
            config.NightLightStrength * nightMask, config.NightLightGlow);

        return Add(day, night);
    }

    private static SKColor WarmEmission(SKColor coreColor, SKColor glowColor, double factor, double glow)
    {
        var coreLuminance = Luminance(coreColor);
        var glowLuminance = Luminance(glowColor);

        // Urban cores have a wider dynamic range than the source texture while the blurred
        // component creates a subtle, non-uniform halo around metropolitan regions.
        var core = Math.Pow(coreLuminance, 1.65) * factor;
        var mid = Math.Pow(coreLuminance, 0.92) * factor * 0.24;
        var halo = Math.Pow(glowLuminance, 0.72) * factor * glow * 0.31;

        return new SKColor(
            ToByte(255.0 * core + 232.0 * mid + 244.0 * halo),
            ToByte(142.0 * core + 101.0 * mid + 105.0 * halo),
            ToByte(34.0 * core + 24.0 * mid + 22.0 * halo), 255);
    }

    private static double Luminance(SKColor color) =>
        (0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0;

    private static SKColor Scale(SKColor color, double factor) =>
        new(ToByte(color.Red * factor), ToByte(color.Green * factor), ToByte(color.Blue * factor), 255);

    private static SKColor Add(SKColor a, SKColor b) =>
        new((byte)Math.Min(255, a.Red + b.Red), (byte)Math.Min(255, a.Green + b.Green),
            (byte)Math.Min(255, a.Blue + b.Blue), 255);

    private static SKColor Add(SKColor color, double r, double g, double b) =>
        new(ToByte(color.Red + r), ToByte(color.Green + g), ToByte(color.Blue + b), 255);

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
