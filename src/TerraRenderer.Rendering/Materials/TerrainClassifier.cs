using TerraRenderer.Assets;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Materials;

internal static class TerrainClassifier
{
    public static TerrainMaterial Classify(
        EarthSurfaceMaterial material,
        Vector3d surfaceNormal,
        Vector3d geometricNormal,
        double latitudeDegrees)
    {
        var slope = CalculateSlope(surfaceNormal, geometricNormal);
        var water = Math.Clamp(material.Water, 0.0, 1.0);
        var ice = Math.Clamp(material.Ice, 0.0, 1.0);
        var vegetation = Math.Clamp(material.Vegetation, 0.0, 1.0) * (1.0 - water);
        var desert = Math.Clamp(material.Desert, 0.0, 1.0) * (1.0 - water);

        var latitudeSnow = SmoothStep(48.0, 78.0, Math.Abs(latitudeDegrees));
        var elevationSnow = SmoothStep(0.58, 0.88, material.Height);
        var snow = Math.Clamp(
            Math.Max(latitudeSnow * 0.72, elevationSnow) *
            (1.0 - water) *
            (1.0 - ice * 0.75),
            0.0,
            1.0);

        var rock = Math.Clamp(
            Math.Max(
                SmoothStep(0.42, 0.78, slope),
                SmoothStep(0.62, 0.92, material.Height)) *
            (1.0 - water) *
            (1.0 - ice) *
            (1.0 - snow * 0.70),
            0.0,
            1.0);

        var type = SelectType(water, ice, snow, vegetation, desert, rock);

        return new TerrainMaterial(
            type,
            material.Height,
            slope,
            water,
            ice,
            snow,
            vegetation,
            desert,
            rock,
            material.Roughness);
    }

    private static TerrainMaterialType SelectType(
        double water,
        double ice,
        double snow,
        double vegetation,
        double desert,
        double rock)
    {
        if (water >= 0.55) return TerrainMaterialType.Water;
        if (ice >= 0.42) return TerrainMaterialType.Ice;
        if (snow >= 0.58) return TerrainMaterialType.Snow;
        if (rock >= 0.62) return TerrainMaterialType.Rock;
        if (vegetation >= 0.42 && vegetation >= desert) return TerrainMaterialType.Vegetation;
        if (desert >= 0.42) return TerrainMaterialType.Desert;
        return TerrainMaterialType.Land;
    }

    private static double CalculateSlope(Vector3d surfaceNormal, Vector3d geometricNormal)
        => 1.0 - Math.Clamp(Vector3d.Dot(surfaceNormal, geometricNormal), 0.0, 1.0);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
