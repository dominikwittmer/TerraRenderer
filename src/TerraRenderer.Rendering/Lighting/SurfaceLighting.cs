using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;
using TerraRenderer.Rendering.Hdr;
using TerraRenderer.Rendering.Materials;

namespace TerraRenderer.Rendering.Lighting;

internal static class SurfaceLighting
{
    public static HdrColor Shade(EarthSurfaceMaterial material, TerrainMaterial terrain, SKColor emissionGlow,
        Vector3d normal, Vector3d geometricNormal, Vector3d sun, Vector3d view, RenderingConfiguration config)
    {
        var illumination = Vector3d.Dot(normal, sun);
        var geometricIllumination = Vector3d.Dot(geometricNormal, sun);
        var ndotv = Math.Max(0.0, Vector3d.Dot(normal, view));
        var dayMix = SmoothStep(-config.TwilightSoftness, config.TwilightSoftness, geometricIllumination);
        var direct = Math.Pow(Math.Max(0.0, illumination), config.DiffusePower) * config.SunLightStrength;
        var hemisphere = config.HemisphereLight * (0.62 + 0.38 * (0.5 + 0.5 * geometricNormal.Z));
        var baseLight = config.AmbientLight + hemisphere;
        var materialDiffuse = 1.0
            + 0.055 * terrain.Vegetation
            - 0.035 * terrain.Desert
            - 0.060 * terrain.Rock
            + 0.075 * terrain.Snow
            + 0.045 * terrain.Ice;
        var diffuse = baseLight + direct * materialDiffuse;
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
            day += new HdrColor(
                (float)(0.004 * shelf * config.OceanShelfTint * dayMix),
                (float)(0.024 * shelf * config.OceanShelfTint * dayMix),
                (float)(0.052 * shelf * config.OceanShelfTint * dayMix));
            day *= 1.0 - config.OceanLimbDarkening * limb * dayMix;
        }

        if (config.EnableOceanSpecular && material.Water > 0.05 && illumination > 0.0)
        {
            var halfVector = (sun + view).Normalize();
            var ndoth = Math.Max(0.0, Vector3d.Dot(normal, halfVector));
            var roughness = Math.Clamp(material.Roughness, 0.025, 0.30);
            var roughnessScale = Math.Clamp(0.075 / roughness, 0.45, 1.85);
            var specularPower = Math.Clamp(config.OceanSpecularPower * roughnessScale, 28.0, 240.0);
            var specular = Math.Pow(ndoth, specularPower);
            var fresnelBase = 0.018 + 0.010 * roughness;
            var fresnel = fresnelBase + config.OceanFresnelStrength * Math.Pow(1.0 - ndotv, 5.0);
            var strength = (specular * config.OceanSpecularStrength * config.SunLightStrength + fresnel * 0.11)
                           * dayMix * material.Water * Math.Max(0.0, illumination);
            day += new HdrColor((float)(1.2 * strength), (float)(1.55 * strength), (float)(2.1 * strength));
        }

        // Sprint 5: daylight recovery. This is deliberately applied before night emission,
        // so the improved day exposure and neutral white balance do not alter city lights.
        day = ApplyDaylightRecovery(day, material, dayMix, config);

        day += NightLightEngine.Evaluate(
            material.Emission,
            material.Bloom.Red > 0 || material.Bloom.Green > 0 || material.Bloom.Blue > 0
                ? material.Bloom
                : emissionGlow,
            geometricIllumination,
            config);

        return day.ClampMinimum();
    }

    private static HdrColor ApplyDaylightRecovery(
        HdrColor color,
        EarthSurfaceMaterial material,
        double dayMix,
        RenderingConfiguration config)
    {
        if (dayMix <= 0.001)
            return color;

        var exposure = Lerp(1.0, Math.Max(0.1, config.DaylightExposure), dayMix);
        var redBalance = Lerp(1.0, config.DaylightRedBalance, dayMix);
        var greenBalance = Lerp(1.0, config.DaylightGreenBalance, dayMix);
        var blueBalance = Lerp(1.0, config.DaylightBlueBalance, dayMix);

        var r = color.R * exposure * redBalance;
        var g = color.G * exposure * greenBalance;
        var b = color.B * exposure * blueBalance;

        // Keep oceans deep blue instead of cyan. Land receives a gentle luminance lift,
        // while snow and ice remain neutral and retain highlight detail.
        var water = Math.Clamp(material.Water, 0.0, 1.0);
        var ice = Math.Clamp(material.Ice, 0.0, 1.0);
        if (water > 0.05)
        {
            var neutralize = config.DaylightOceanNeutralization * water * dayMix;
            r *= 1.0 + 0.20 * neutralize;
            g *= 1.0 - 0.18 * neutralize;
            b *= 1.0 - 0.10 * neutralize;
        }
        else
        {
            var landLift = config.DaylightLandLift * dayMix * (1.0 - 0.65 * ice);
            r += (float)(landLift * 0.94);
            g += (float)(landLift * 0.97);
            b += (float)(landLift * 0.90);
        }

        if (ice > 0.02)
        {
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            var neutral = luminance * (1.0 + 0.08 * ice * dayMix);
            var blend = 0.30 * ice * dayMix;
            r = (float)Lerp(r, neutral * 1.01, blend);
            g = (float)Lerp(g, neutral, blend);
            b = (float)Lerp(b, neutral * 0.99, blend);
        }

        return new HdrColor((float)r, (float)g, (float)b);
    }

    private static double SmoothStep(double a, double b, double v)
    {
        var t = Math.Clamp((v - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
