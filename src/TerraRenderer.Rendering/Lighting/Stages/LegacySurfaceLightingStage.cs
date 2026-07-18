namespace TerraRenderer.Rendering.Lighting.Stages;

/// <summary>
/// Temporary adapter that keeps the current rendering output unchanged while
/// the monolithic SurfaceLighting implementation is split into dedicated stages.
/// </summary>
internal sealed class LegacySurfaceLightingStage : ILightingStage
{
    public void Apply(in LightingContext context, LightingResult result)
    {
        result.Color = SurfaceLighting.Shade(
            context.Material,
            context.EmissionGlow,
            context.SurfaceNormal,
            context.GeometricNormal,
            context.SunDirection,
            context.ViewDirection,
            context.Configuration);
    }
}
