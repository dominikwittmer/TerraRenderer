using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.Lighting;

internal sealed class LightingResult
{
    public HdrColor Color { get; set; } = HdrColor.Black;
}
