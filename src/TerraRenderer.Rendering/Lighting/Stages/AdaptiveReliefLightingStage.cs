using SkiaSharp;

namespace TerraRenderer.Rendering.Lighting.Stages;

/// <summary>
/// Enhances terrain readability after the legacy surface-lighting pass.
/// The effect is adaptive: flat land, oceans and the night side remain nearly unchanged,
/// while mountain slopes receive restrained directional contrast and diffuse fill light.
/// </summary>
internal sealed class AdaptiveReliefLightingStage : ILightingStage
{
    public void Apply(in LightingContext context, LightingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var settings = context.Configuration.AdaptiveRelief;
        if (!settings.Enabled || settings.Strength <= 0.0)
            return;

        var terrain = context.Terrain;

        // Relief enhancement over water produces noisy coastlines and false wave structure.
        var landWeight = 1.0 - SmoothStep(0.04, 0.32, terrain.Water);
        if (landWeight <= 0.001)
            return;

        // TerrainClassifier.Slope is 1 - dot(surfaceNormal, geometricNormal).
        // Useful global terrain normally occupies the lower part of that range.
        var slopeWeight = SmoothStep(0.006, 0.20, terrain.Slope);
        if (slopeWeight <= 0.001)
            return;

        var geometricLight = DotClamped(context.GeometricNormal, context.SunDirection);
        var terrainLight = DotClamped(context.SurfaceNormal, context.SunDirection);

        // Do not reshape city lights or the deep night side. Keep a soft contribution in twilight.
        var daylightWeight = SmoothStep(-0.24, 0.16, geometricLight);

        // Difference between the relief normal and the globe normal isolates local terrain lighting.
        var directionalRelief = terrainLight - geometricLight;
        var sunward = Math.Max(0.0, directionalRelief);
        var lee = Math.Max(0.0, -directionalRelief);

        // AO acts as an inexpensive openness/valley hint. Open ridges tend towards 1,
        // enclosed valleys towards 0. This is not geometric curvature, but is stable globally.
        var openness = Math.Clamp(context.Material.AmbientOcclusion, 0.0, 1.0);
        var ridgeHint = SmoothStep(0.70, 0.98, openness);
        var valleyHint = 1.0 - SmoothStep(0.56, 0.92, openness);

        var shadowAmount = 1.0 - SmoothStep(-0.02, 0.38, terrainLight);

        var ridge = settings.RidgeStrength * sunward * (0.55 + 0.45 * ridgeHint);
        var valley = settings.ValleyStrength *
                     (lee * 0.72 + valleyHint * 0.28 * slopeWeight);
        var skyFill = settings.SkyLight * shadowAmount * (0.35 + 0.65 * slopeWeight);
        var bounce = settings.AmbientBounce * slopeWeight;

        // Snow and ice already have strong tonal separation. Rock benefits most; desert and
        // vegetation are kept calmer so large regions do not become visually noisy.
        var materialWeight =
            1.0 +
            terrain.Rock * 0.22 -
            terrain.Snow * 0.34 -
            terrain.Ice * 0.48 -
            terrain.Desert * 0.18 -
            terrain.Vegetation * 0.08;
        materialWeight = Math.Clamp(materialWeight, 0.45, 1.25);

        var adaptiveWeight = settings.Strength * landWeight * slopeWeight *
                             daylightWeight * materialWeight;

        // Macro relief survives the downscale to 466 px. Elevation provides the broad mountain
        // mass, AO supplies valley enclosure and Rock limits the effect to structurally useful land.
        var normalizedHeight = SmoothStep(0.08, 0.72, terrain.Height);
        var macro = settings.MacroReliefStrength * normalizedHeight *
                    (0.40 + 0.60 * terrain.Rock) * daylightWeight * landWeight;
        var rockContrast = settings.RockContrast * terrain.Rock * slopeWeight * directionalRelief;

        var darkening = valley * adaptiveWeight + Math.Max(0.0, -rockContrast);
        var brightening = (ridge + skyFill + bounce) * adaptiveWeight +
                          macro * (0.45 + 0.55 * Math.Max(0.0, geometricLight)) +
                          Math.Max(0.0, rockContrast);
        var factor = Math.Clamp(1.0 + brightening - darkening, 0.80, 1.20);

        // A very small blue bias in shadow fill suggests skylight without changing the
        // established colour palette. Direct ridge light remains neutral.
        var coolFill = skyFill * adaptiveWeight;
        result.Color = Apply(result.Color, factor, coolFill);
    }

    private static SKColor Apply(SKColor color, double factor, double coolFill)
    {
        var red = color.Red * factor + 4.0 * coolFill;
        var green = color.Green * factor + 9.0 * coolFill;
        var blue = color.Blue * factor + 17.0 * coolFill;

        return new SKColor(ToByte(red), ToByte(green), ToByte(blue), color.Alpha);
    }

    private static double DotClamped(
        TerraRenderer.Core.Geometry.Vector3d first,
        TerraRenderer.Core.Geometry.Vector3d second)
        => Math.Clamp(TerraRenderer.Core.Geometry.Vector3d.Dot(first, second), -1.0, 1.0);

    private static byte ToByte(double value)
        => (byte)Math.Clamp(Math.Round(value), 0.0, 255.0);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
