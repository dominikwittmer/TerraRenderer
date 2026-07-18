namespace TerraRenderer.Rendering.Materials;

internal readonly record struct TerrainMaterial(
    TerrainMaterialType Type,
    double Height,
    double Slope,
    double Water,
    double Ice,
    double Snow,
    double Vegetation,
    double Desert,
    double Rock,
    double Roughness);
