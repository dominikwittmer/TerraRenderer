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
        var ndotv = Math.Max(0.0, Vector3d.Dot(normal, view));

        var dayMix = SmoothStep(-config.TwilightSoftness, config.TwilightSoftness, geometricIllumination);
        var directLight = Math.Pow(Math.Max(0.0, illumination), config.DiffusePower) * config.SunLightStrength;
        var skyHemisphere = 0.5 + 0.5 * geometricNormal.Z;
        var hemisphere = config.HemisphereLight * (0.62 + 0.38 * skyHemisphere);
        var baseLight = config.AmbientLight + hemisphere;
        var diffuse = baseLight + (1.0 - baseLight) * directLight;
        diffuse = Math.Min(diffuse, 1.42);

        var twilightBand = Math.Exp(-Math.Pow(geometricIllumination / config.TwilightBandWidth, 2.0));
        var twilightLift = config.TwilightSurfaceLift * twilightBand * (1.0 - 0.72 * dayMix);
        var ao = 1.0 - config.AmbientOcclusionStrength * (1.0 - material.AmbientOcclusion);
        var day = Scale(material.Albedo, diffuse * (0.18 + 0.82 * dayMix) * ao);
        day = Add(day,
            24.0 * twilightLift * (1.0 - material.Water),
            37.0 * twilightLift,
            58.0 * twilightLift);

        if (material.Water > 0.01)
        {
            // The source albedo already contains bathymetric colour. These restrained terms
            // add depth, continental-shelf colour and a darker grazing-angle ocean without
            // creating a glossy game-engine appearance.
            var water = Math.Clamp(material.Water, 0.0, 1.0);
            var luminance = Luminance(material.Albedo);
            var deepWater = water * SmoothStep(0.05, 0.42, 0.38 - luminance);
            var shelf = water * SmoothStep(0.30, 0.62, luminance) * (1.0 - material.Ice);
            var limb = water * Math.Pow(1.0 - ndotv, 2.2);

            day = Multiply(day, 1.0 - config.OceanDepthStrength * deepWater * dayMix);
            day = Add(day,
                4.0 * shelf * config.OceanShelfTint * dayMix,
                24.0 * shelf * config.OceanShelfTint * dayMix,
                36.0 * shelf * config.OceanShelfTint * dayMix);
            day = Multiply(day, 1.0 - config.OceanLimbDarkening * limb * dayMix);
        }

        if (config.EnableOceanSpecular && material.Water > 0.05 && illumination > 0.0)
        {
            var halfVector = (sun + view).Normalize();
            var ndoth = Math.Max(0.0, Vector3d.Dot(normal, halfVector));
            var specular = Math.Pow(ndoth, config.OceanSpecularPower);
            var fresnel = 0.014 + config.OceanFresnelStrength * Math.Pow(1.0 - ndotv, 5.0);
            var strength = (specular * config.OceanSpecularStrength * config.SunLightStrength + fresnel * 0.11) *
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
            config.NightLightStrength * config.NightCoreStrength * nightMask,
            config.NightLightGlow, config.NightBloomSoftness);

        // A tiny blue atmospheric floor keeps the night hemisphere from becoming an empty disk,
        // while still remaining OLED-friendly and much darker than twilight.
        var nightAtmosphere = config.NightAtmosphereStrength * nightMask *
                              (0.35 + 0.65 * Math.Pow(1.0 - ndotv, 1.8));
        day = Add(day, 3.0 * nightAtmosphere, 7.0 * nightAtmosphere, 18.0 * nightAtmosphere);

        return Add(day, night);
    }

    private static SKColor WarmEmission(SKColor coreColor, SKColor glowColor, double factor, double glow,
        double softness)
    {
        var coreLuminance = Luminance(coreColor);
        var glowLuminance = Luminance(glowColor);
        var coreExponent = 1.85 - 0.45 * Math.Clamp(softness, 0.0, 1.0);
        var haloExponent = 0.86 - 0.28 * Math.Clamp(softness, 0.0, 1.0);

        var core = Math.Pow(coreLuminance, coreExponent) * factor;
        var mid = Math.Pow(coreLuminance, 0.94) * factor * 0.22;
        var halo = Math.Pow(glowLuminance, haloExponent) * factor * glow * 0.34;

        return new SKColor(
            ToByte(255.0 * core + 232.0 * mid + 244.0 * halo),
            ToByte(142.0 * core + 101.0 * mid + 105.0 * halo),
            ToByte(34.0 * core + 24.0 * mid + 22.0 * halo), 255);
    }

    private static double Luminance(SKColor color) =>
        (0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0;

    private static SKColor Scale(SKColor color, double factor) => Multiply(color, factor);

    private static SKColor Multiply(SKColor color, double factor) =>
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
