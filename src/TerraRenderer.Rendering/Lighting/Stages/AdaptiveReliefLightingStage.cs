namespace TerraRenderer.Rendering.Lighting.Stages;

internal sealed class AdaptiveReliefLightingStage : ILightingStage
{
    public void Apply(in LightingContext context, LightingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var settings = context.Configuration.AdaptiveRelief;
        if (!settings.Enabled || settings.Strength <= 0.0) return;

        var terrain = context.Terrain;
        var landWeight = 1.0 - SmoothStep(0.04, 0.32, terrain.Water);
        var slopeWeight = SmoothStep(0.006, 0.20, terrain.Slope);
        if (landWeight <= 0.001 || slopeWeight <= 0.001) return;

        var geometricLight = DotClamped(context.GeometricNormal, context.SunDirection);
        var terrainLight = DotClamped(context.SurfaceNormal, context.SunDirection);
        var daylightWeight = SmoothStep(-0.24, 0.16, geometricLight);
        var directionalRelief = terrainLight - geometricLight;
        var sunward = Math.Max(0.0, directionalRelief);
        var lee = Math.Max(0.0, -directionalRelief);
        var openness = Math.Clamp(context.Material.AmbientOcclusion, 0.0, 1.0);
        var ridgeHint = SmoothStep(0.70, 0.98, openness);
        var valleyHint = 1.0 - SmoothStep(0.56, 0.92, openness);
        var shadowAmount = 1.0 - SmoothStep(-0.02, 0.38, terrainLight);
        var ridge = settings.RidgeStrength * sunward * (0.55 + 0.45 * ridgeHint);
        var valley = settings.ValleyStrength * (lee * 0.72 + valleyHint * 0.28 * slopeWeight);
        var skyFill = settings.SkyLight * shadowAmount * (0.35 + 0.65 * slopeWeight);
        var bounce = settings.AmbientBounce * slopeWeight;
        var materialWeight = Math.Clamp(1.0 + terrain.Rock * 0.22 - terrain.Snow * 0.34 - terrain.Ice * 0.48 - terrain.Desert * 0.18 - terrain.Vegetation * 0.08, 0.45, 1.25);
        var adaptiveWeight = settings.Strength * landWeight * slopeWeight * daylightWeight * materialWeight;
        var normalizedHeight = SmoothStep(0.08, 0.72, terrain.Height);
        var macro = settings.MacroReliefStrength * normalizedHeight * (0.40 + 0.60 * terrain.Rock) * daylightWeight * landWeight;
        var rockContrast = settings.RockContrast * terrain.Rock * slopeWeight * directionalRelief;
        var darkening = valley * adaptiveWeight + Math.Max(0.0, -rockContrast);
        var brightening = (ridge + skyFill + bounce) * adaptiveWeight + macro * (0.45 + 0.55 * Math.Max(0.0, geometricLight)) + Math.Max(0.0, rockContrast);
        var factor = Math.Clamp(1.0 + brightening - darkening, 0.80, 1.20);
        var coolFill = skyFill * adaptiveWeight;
        var color = result.Color;
        result.Color = new(
            (float)(color.R * factor + 0.012 * coolFill),
            (float)(color.G * factor + 0.028 * coolFill),
            (float)(color.B * factor + 0.060 * coolFill));
    }

    private static double DotClamped(TerraRenderer.Core.Geometry.Vector3d a, TerraRenderer.Core.Geometry.Vector3d b) => Math.Clamp(TerraRenderer.Core.Geometry.Vector3d.Dot(a, b), -1.0, 1.0);
    private static double SmoothStep(double a, double b, double v) { var t = Math.Clamp((v-a)/(b-a),0,1); return t*t*(3-2*t); }
}
