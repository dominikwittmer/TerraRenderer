using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Lighting;

internal static class SurfaceLighting
{
    public static SKColor Shade(
        EarthSurfaceMaterial material,
        SKColor emissionGlow,
        Vector3d normal,
        Vector3d geometricNormal,
        Vector3d sun,
        Vector3d view,
        RenderingConfiguration config)
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
        diffuse = Math.Min(diffuse, 1.48);

        var twilightBand = Math.Exp(-Math.Pow(geometricIllumination / config.TwilightBandWidth, 2.0));
        var twilightLift = config.TwilightSurfaceLift * twilightBand * (1.0 - 0.72 * dayMix);
        var ao = 1.0 - config.AmbientOcclusionStrength * (1.0 - material.AmbientOcclusion);

        var day = Scale(material.Albedo, diffuse * (0.18 + 0.82 * dayMix) * ao);
        day = Add(day,
            28.0 * twilightLift * (1.0 - material.Water),
            37.0 * twilightLift,
            56.0 * twilightLift);

        if (material.Water > 0.01)
        {
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
            var strength = (specular * config.OceanSpecularStrength * config.SunLightStrength + fresnel * 0.11)
                * dayMix * material.Water * Math.Max(0.0, illumination);
            day = Add(day, 148 * strength, 184 * strength, 234 * strength);
        }

        var nightMask = config.EnableNightLights
            ? Math.Pow(1.0 - SmoothStep(-config.NightLightFadeWidth, config.NightLightFadeWidth, geometricIllumination), config.NightLightFadePower)
            : 0.0;

        var glowSource = material.Bloom.Red > 0 || material.Bloom.Green > 0 || material.Bloom.Blue > 0
            ? material.Bloom
            : emissionGlow;

        var night = CinematicEmission(material.Emission, glowSource, nightMask, config);

        var nightAtmosphere = config.NightAtmosphereStrength * nightMask
            * (0.30 + 0.70 * Math.Pow(1.0 - ndotv, 1.8));
        day = Add(day, 2.0 * nightAtmosphere, 6.0 * nightAtmosphere, 18.0 * nightAtmosphere);

        return Add(day, night);
    }

    private static SKColor CinematicEmission(
        SKColor coreColor,
        SKColor glowColor,
        double nightMask,
        RenderingConfiguration config)
    {
        var source = Math.Max(Luminance(coreColor), 0.92 * Luminance(glowColor));
        if (source <= 0.00001 || nightMask <= 0.00001)
            return SKColors.Black;

        var compression = Math.Clamp(config.NightCompression, 0.15, 1.5);
        var normalized = 1.0 - Math.Exp(-source * (3.4 + 4.2 * compression));
        var factor = config.NightLightStrength * config.NightCoreStrength * nightMask;

        var core = Math.Pow(normalized, 2.15 - 0.55 * config.NightBloomSoftness) * factor;
        var mid = Math.Pow(normalized, 0.92) * factor * 0.52;
        var halo = Math.Pow(Math.Max(Luminance(glowColor), source), 0.58) * factor
            * config.NightLightGlow * config.NightHaloStrength;

        var whiteCore = Math.Clamp(config.NightWhiteCore, 0.0, 1.0);
        var warmth = Math.Clamp(config.NightWarmth, 0.0, 1.0);

        // Dense city centres approach neutral white. Their surrounding halo stays warm.
        var r = 255.0 * core + (236.0 + 19.0 * warmth) * mid + (238.0 + 17.0 * warmth) * halo;
        var g = (190.0 + 65.0 * whiteCore) * core + (184.0 + 52.0 * whiteCore) * mid + (154.0 + 62.0 * whiteCore) * halo;
        var b = (116.0 + 139.0 * whiteCore) * core + (92.0 + 124.0 * whiteCore) * mid + (58.0 + 92.0 * whiteCore) * halo;

        return new SKColor(ToByte(r), ToByte(g), ToByte(b), 255);
    }

    private static double Luminance(SKColor color) =>
        (0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0;

    private static SKColor Scale(SKColor color, double factor) => Multiply(color, factor);

    private static SKColor Multiply(SKColor color, double factor) =>
        new(ToByte(color.Red * factor), ToByte(color.Green * factor), ToByte(color.Blue * factor), 255);

    private static SKColor Add(SKColor a, SKColor b) =>
        new((byte)Math.Min(255, a.Red + b.Red),
            (byte)Math.Min(255, a.Green + b.Green),
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
