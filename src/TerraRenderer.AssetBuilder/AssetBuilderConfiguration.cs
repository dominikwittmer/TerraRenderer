using System.Text.Json.Serialization;

namespace TerraRenderer.AssetBuilder;

public sealed class AssetBuilderConfiguration
{
    public SourceConfiguration Sources { get; init; } = new();
    public OutputConfiguration Output { get; init; } = new();
    public HeightConfiguration Height { get; init; } = new();
    public NightConfiguration Night { get; init; } = new();
    public MaterialConfiguration Materials { get; init; } = new();
}

public sealed class SourceConfiguration
{
    public string? DayAlbedo { get; init; }
    public string? NightLights { get; init; }
    public string? ElevationGeoTiff { get; init; }
}

public sealed class OutputConfiguration
{
    public string Directory { get; init; } = "assets/generated";
    public int Width { get; init; } = 4096;
    public int Height { get; init; } = 2048;
    public int JpegQuality { get; init; } = 94;
}

public sealed class HeightConfiguration
{
    public double MinimumMeters { get; init; } = -11000.0;
    public double MaximumMeters { get; init; } = 9000.0;
    public double NormalStrength { get; init; } = 6.0;
    public bool IncludeBathymetryInNormals { get; init; }
    public double PreFilterSigmaPixels { get; init; } = 1.15;
    public double CurvatureStrength { get; init; } = 0.9;
    public double SlopeReferenceDegrees { get; init; } = 35.0;
    public double AmbientOcclusionStrength { get; init; } = 0.75;
    public double AmbientOcclusionRadiusKm { get; init; } = 120.0;
    public int AmbientOcclusionDirections { get; init; } = 8;
    public int AmbientOcclusionSteps { get; init; } = 6;
    public int TileCacheCapacity { get; init; } = 384;
}

public sealed class NightConfiguration
{
    public double BlackPoint { get; init; } = 0.025;
    public double WhitePoint { get; init; } = 0.72;
    public double Gamma { get; init; } = 1.35;
    public double BloomThreshold { get; init; } = 0.12;
    public int BloomRadiusPixels { get; init; } = 5;
    public byte WarmRed { get; init; } = 255;
    public byte WarmGreen { get; init; } = 224;
    public byte WarmBlue { get; init; } = 168;
}

public sealed class MaterialConfiguration
{
    public double IceLatitudeStart { get; init; } = 55.0;
    public double IceBrightnessThreshold { get; init; } = 0.72;
    public double DesertWarmthThreshold { get; init; } = 0.08;
    public double VegetationGreenThreshold { get; init; } = 0.025;
}
