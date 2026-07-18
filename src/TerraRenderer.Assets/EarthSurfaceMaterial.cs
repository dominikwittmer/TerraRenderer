using SkiaSharp;

namespace TerraRenderer.Assets;

public readonly record struct EarthSurfaceMaterial(
    SKColor Albedo,
    SKColor Emission,
    SKColor Bloom,
    double Height,
    double Water,
    double Ice,
    double Vegetation,
    double Desert,
    double AmbientOcclusion,
    double Roughness);
