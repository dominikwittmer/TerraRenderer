using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering;

public sealed record RenderRequest
{
    public required DateTimeOffset TimeUtc { get; init; }
    public required LayoutConfiguration Layout { get; init; }
    public required RenderingConfiguration Rendering { get; init; }
}
