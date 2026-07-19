using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Lighting;

internal sealed class LightingPipeline
{
    private readonly IReadOnlyList<ILightingStage> _stages;

    public LightingPipeline(params ILightingStage[] stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        _stages = stages;
    }

    public HdrColor Shade(in LightingContext context)
    {
        var result = new LightingResult();
        foreach (var stage in _stages)
            stage.Apply(context, result);
        return result.Color;
    }
}
