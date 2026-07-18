using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Lighting;

internal readonly record struct LightingContext(
    EarthSurfaceMaterial Material,
    SKColor EmissionGlow,
    Vector3d SurfaceNormal,
    Vector3d GeometricNormal,
    Vector3d SunDirection,
    Vector3d ViewDirection,
    RenderingConfiguration Configuration);
