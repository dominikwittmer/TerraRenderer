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

        day += NightLightEngine.Evaluate(
            material.Emission,
            material.Bloom.Red > 0 || material.Bloom.Green > 0 || material.Bloom.Blue > 0
                ? material.Bloom
                : emissionGlow,
            geometricIllumination,
            config);

        return day.ClampMinimum();
    }

    private static double SmoothStep(double a, double b, double v) { var t = Math.Clamp((v-a)/(b-a),0,1); return t*t*(3-2*t); }
}
