namespace TerraRenderer.Rendering.Lighting;

internal interface ILightingStage
{
    void Apply(in LightingContext context, LightingResult result);
}
