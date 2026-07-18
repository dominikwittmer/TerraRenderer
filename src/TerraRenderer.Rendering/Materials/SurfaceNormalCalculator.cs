using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Materials;

internal static class SurfaceNormalCalculator
{
    public static Vector3d Calculate(EarthMaterialAtlas atlas, double latitude, double longitude,
        Vector3d geometricNormal, double sampleDegrees, double strength, double normalMapStrength)
    {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        var east = new Vector3d(-Math.Sin(lonRad), Math.Cos(lonRad), 0.0).Normalize();
        var north = new Vector3d(-Math.Sin(latRad) * Math.Cos(lonRad),
            -Math.Sin(latRad) * Math.Sin(lonRad), Math.Cos(latRad)).Normalize();

        var sampled = atlas.SampleNormal(latitude, longitude);
        if (sampled is SKColor normalColor)
        {
            var tangentX = normalColor.Red / 255.0 * 2.0 - 1.0;
            var tangentY = normalColor.Green / 255.0 * 2.0 - 1.0;
            var tangentZ = normalColor.Blue / 255.0 * 2.0 - 1.0;
            var mapped = (east * tangentX + north * -tangentY + geometricNormal * tangentZ).Normalize();
            return (geometricNormal * (1.0 - normalMapStrength) + mapped * normalMapStrength).Normalize();
        }

        var eastHeight = atlas.SampleHeight(latitude, longitude + sampleDegrees);
        var westHeight = atlas.SampleHeight(latitude, longitude - sampleDegrees);
        var northHeight = atlas.SampleHeight(latitude + sampleDegrees, longitude);
        var southHeight = atlas.SampleHeight(latitude - sampleDegrees, longitude);
        var dx = eastHeight - westHeight;
        var dy = northHeight - southHeight;
        return (geometricNormal - east * (dx * strength) - north * (dy * strength)).Normalize();
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
}
