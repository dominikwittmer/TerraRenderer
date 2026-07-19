using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Lighting;

internal static class SurfaceLighting
{
    public static HdrColor Shade(EarthSurfaceMaterial material, SKColor emissionGlow, Vector3d normal,
        Vector3d geometricNormal, Vector3d sun, Vector3d view, RenderingConfiguration config)
    {
        var illumination = Vector3d.Dot(normal, sun);
        var geometricIllumination = Vector3d.Dot(geometricNormal, sun);
        var ndotv = Math.Max(0.0, Vector3d.Dot(normal, view));
        var dayMix = SmoothStep(-config.TwilightSoftness, config.TwilightSoftness, geometricIllumination);
        var direct = Math.Pow(Math.Max(0.0, illumination), config.DiffusePower) * config.SunLightStrength;
        var hemisphere = config.HemisphereLight * (0.62 + 0.38 * (0.5 + 0.5 * geometricNormal.Z));
        var baseLight = config.AmbientLight + hemisphere;
        var diffuse = baseLight + direct;
        var twilightBand = Math.Exp(-Math.Pow(geometricIllumination / config.TwilightBandWidth, 2.0));
        var twilightLift = config.TwilightSurfaceLift * twilightBand * (1.0 - 0.72 * dayMix);
        var ao = 1.0 - config.AmbientOcclusionStrength * (1.0 - material.AmbientOcclusion);

        var day = HdrColor.FromSrgb(material.Albedo) * (diffuse * (0.18 + 0.82 * dayMix) * ao);
        day += new HdrColor((float)(0.012 * twilightLift), (float)(0.022 * twilightLift), (float)(0.040 * twilightLift));

        if (material.Water > 0.01)
        {
            var water = Math.Clamp(material.Water, 0.0, 1.0);
            var lum = HdrColor.FromSrgb(material.Albedo).Luminance;
            var deepWater = water * SmoothStep(0.02, 0.16, 0.12 - lum);
            var shelf = water * SmoothStep(0.08, 0.30, lum) * (1.0 - material.Ice);
            var limb = water * Math.Pow(1.0 - ndotv, 2.2);
            day *= 1.0 - config.OceanDepthStrength * deepWater * dayMix;
            day += new HdrColor((float)(0.004 * shelf * config.OceanShelfTint * dayMix), (float)(0.024 * shelf * config.OceanShelfTint * dayMix), (float)(0.052 * shelf * config.OceanShelfTint * dayMix));
            day *= 1.0 - config.OceanLimbDarkening * limb * dayMix;
        }

        if (config.EnableOceanSpecular && material.Water > 0.05 && illumination > 0.0)
        {
            var halfVector = (sun + view).Normalize();
            var ndoth = Math.Max(0.0, Vector3d.Dot(normal, halfVector));
            var specular = Math.Pow(ndoth, config.OceanSpecularPower);
            var fresnel = 0.014 + config.OceanFresnelStrength * Math.Pow(1.0 - ndotv, 5.0);
            var strength = (specular * config.OceanSpecularStrength * config.SunLightStrength + fresnel * 0.11) * dayMix * material.Water * Math.Max(0.0, illumination);
            day += new HdrColor((float)(1.2 * strength), (float)(1.55 * strength), (float)(2.1 * strength));
        }

        var nightMask = config.EnableNightLights
            ? Math.Pow(1.0 - SmoothStep(-config.NightLightFadeWidth, config.NightLightFadeWidth, geometricIllumination), config.NightLightFadePower)
            : 0.0;
        var glow = material.Bloom.Red > 0 || material.Bloom.Green > 0 || material.Bloom.Blue > 0 ? material.Bloom : emissionGlow;
        day += NightRadiance(material.Emission, glow, nightMask, config);

        var nightAtmosphere = config.NightAtmosphereStrength * nightMask * (0.30 + 0.70 * Math.Pow(1.0 - ndotv, 1.8));
        day += new HdrColor((float)(0.004 * nightAtmosphere), (float)(0.014 * nightAtmosphere), (float)(0.055 * nightAtmosphere));
        return day.ClampMinimum();
    }

    private static HdrColor NightRadiance(SKColor coreColor, SKColor glowColor, double nightMask, RenderingConfiguration config)
    {
        var coreLinear = HdrColor.FromSrgb(coreColor);
        var glowLinear = HdrColor.FromSrgb(glowColor);
        var source = Math.Max(coreLinear.Luminance, 0.92 * glowLinear.Luminance);
        if (source <= 1e-8 || nightMask <= 1e-8) return HdrColor.Black;

        // Preserve the source dynamic range instead of flattening it to a mask.
        var compressed = Math.Pow(source, Math.Clamp(config.NightCompression, 0.45, 1.35));
        var factor = config.NightLightStrength * config.NightCoreStrength * nightMask;
        var core = Math.Pow(compressed, 1.65) * factor * 8.5;
        var mid = Math.Pow(compressed, 0.82) * factor * 1.8;
        var halo = Math.Pow(Math.Max(glowLinear.Luminance, source), 0.58) * factor * config.NightLightGlow * config.NightHaloStrength * 0.72;
        var white = Math.Clamp(config.NightWhiteCore, 0, 1);
        var warmth = Math.Clamp(config.NightWarmth, 0, 1);

        return new HdrColor(
            (float)(core * (0.92 + 0.08 * white) + mid * (1.05 + 0.12 * warmth) + halo * (1.08 + 0.18 * warmth)),
            (float)(core * (0.78 + 0.22 * white) + mid * (0.78 + 0.17 * white) + halo * (0.61 + 0.20 * white)),
            (float)(core * (0.48 + 0.52 * white) + mid * (0.32 + 0.38 * white) + halo * (0.16 + 0.24 * white)));
    }

    private static double SmoothStep(double a, double b, double v) { var t = Math.Clamp((v-a)/(b-a),0,1); return t*t*(3-2*t); }
}
