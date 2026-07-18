using SkiaSharp;

namespace TerraRenderer.Assets;

public sealed class EarthMaterialAtlas
{
    private readonly EquirectangularSampler _day;
    private readonly EquirectangularSampler _night;
    private readonly EquirectangularSampler? _elevation;
    private readonly EquirectangularSampler? _waterMask;
    private readonly EquirectangularSampler? _iceMask;
    private readonly EquirectangularSampler? _normal;
    private readonly EquirectangularSampler? _material;
    private readonly EquirectangularSampler? _ambientOcclusion;
    private readonly EquirectangularSampler? _nightBloom;

    internal EarthMaterialAtlas(SKBitmap dayTexture, SKBitmap nightTexture, SKBitmap? elevationTexture,
        SKBitmap? waterMaskTexture, SKBitmap? iceMaskTexture, SKBitmap? normalTexture,
        SKBitmap? materialTexture, SKBitmap? ambientOcclusionTexture, SKBitmap? nightBloomTexture)
    {
        _day = new(dayTexture);
        _night = new(nightTexture);
        _elevation = elevationTexture is null ? null : new(elevationTexture);
        _waterMask = waterMaskTexture is null ? null : new(waterMaskTexture);
        _iceMask = iceMaskTexture is null ? null : new(iceMaskTexture);
        _normal = normalTexture is null ? null : new(normalTexture);
        _material = materialTexture is null ? null : new(materialTexture);
        _ambientOcclusion = ambientOcclusionTexture is null ? null : new(ambientOcclusionTexture);
        _nightBloom = nightBloomTexture is null ? null : new(nightBloomTexture);
    }

    public bool UsesElevationTexture => _elevation is not null;
    public bool UsesNormalTexture => _normal is not null;
    public bool UsesMaterialTexture => _material is not null;
    public bool UsesWaterMaskTexture => _waterMask is not null;
    public bool UsesIceMaskTexture => _iceMask is not null;
    public bool UsesAmbientOcclusionTexture => _ambientOcclusion is not null;

    public EarthSurfaceMaterial Sample(double latitudeDegrees, double longitudeDegrees)
    {
        var albedo = _day.Sample(latitudeDegrees, longitudeDegrees);
        var emission = _night.Sample(latitudeDegrees, longitudeDegrees);
        var bloom = _nightBloom?.Sample(latitudeDegrees, longitudeDegrees) ?? SKColors.Black;
        var packed = _material?.Sample(latitudeDegrees, longitudeDegrees);

        var water = packed is null
            ? (_waterMask is null ? DetectWater(albedo) : MaskValue(_waterMask.Sample(latitudeDegrees, longitudeDegrees)))
            : packed.Value.Red / 255.0;
        var vegetation = packed?.Green / 255.0 ?? DetectVegetation(albedo, water);
        var desert = packed?.Blue / 255.0 ?? DetectDesert(albedo, water, vegetation);
        var ice = packed is null
            ? (_iceMask is null ? DetectIce(albedo, latitudeDegrees) : MaskValue(_iceMask.Sample(latitudeDegrees, longitudeDegrees)))
            : packed.Value.Alpha / 255.0;
        var height = _elevation is null
            ? EstimateHeight(albedo, water, ice)
            : DecodeElevation(_elevation.Sample(latitudeDegrees, longitudeDegrees), water);
        var ao = _ambientOcclusion is null ? 1.0 : MaskValue(_ambientOcclusion.Sample(latitudeDegrees, longitudeDegrees));
        var roughness = water > 0.5 ? 0.055 : ice > 0.35 ? 0.34 : 0.80;
        return new(albedo, emission, bloom, height, water, ice, vegetation, desert, ao, roughness);
    }

    public double SampleHeight(double latitudeDegrees, double longitudeDegrees) =>
        Sample(latitudeDegrees, longitudeDegrees).Height;

    public SKColor? SampleNormal(double latitudeDegrees, double longitudeDegrees) =>
        _normal?.Sample(latitudeDegrees, longitudeDegrees);

    public SKColor SampleEmissionGlow(double latitudeDegrees, double longitudeDegrees, double radiusDegrees)
    {
        if (_nightBloom is not null)
            return _nightBloom.Sample(latitudeDegrees, longitudeDegrees);
        if (radiusDegrees <= 0.0) return _night.Sample(latitudeDegrees, longitudeDegrees);

        var cosLatitude = Math.Max(0.25, Math.Cos(latitudeDegrees * Math.PI / 180.0));
        var lon = radiusDegrees / cosLatitude;
        var lat = radiusDegrees;
        var samples = new (double Lat, double Lon, double Weight)[]
        {
            (latitudeDegrees, longitudeDegrees, 4.0),
            (latitudeDegrees + lat, longitudeDegrees, 2.0),
            (latitudeDegrees - lat, longitudeDegrees, 2.0),
            (latitudeDegrees, longitudeDegrees + lon, 2.0),
            (latitudeDegrees, longitudeDegrees - lon, 2.0),
            (latitudeDegrees + lat * 0.72, longitudeDegrees + lon * 0.72, 1.0),
            (latitudeDegrees + lat * 0.72, longitudeDegrees - lon * 0.72, 1.0),
            (latitudeDegrees - lat * 0.72, longitudeDegrees + lon * 0.72, 1.0),
            (latitudeDegrees - lat * 0.72, longitudeDegrees - lon * 0.72, 1.0)
        };

        double r = 0, g = 0, b = 0, weight = 0;
        foreach (var sample in samples)
        {
            var color = _night.Sample(Math.Clamp(sample.Lat, -90.0, 90.0), sample.Lon);
            r += color.Red * sample.Weight;
            g += color.Green * sample.Weight;
            b += color.Blue * sample.Weight;
            weight += sample.Weight;
        }
        return new SKColor(ToByte(r / weight), ToByte(g / weight), ToByte(b / weight), 255);
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    private static double MaskValue(SKColor color) =>
        Math.Clamp((0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0, 0.0, 1.0);
    private static double DecodeElevation(SKColor color, double water) =>
        water > 0.55 ? 0.0 : Math.Clamp(MaskValue(color), 0.0, 1.0);

    private static double DetectWater(SKColor color)
    {
        var blueDominance = color.Blue - Math.Max(color.Red, color.Green);
        var value = (blueDominance - 2.0) / 34.0;
        if (color.Blue < 50 || color.Green < color.Red * 0.76) value *= 0.22;
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static double DetectIce(SKColor color, double latitude)
    {
        var brightness = (color.Red + color.Green + color.Blue) / (3.0 * 255.0);
        var chroma = Math.Max(color.Red, Math.Max(color.Green, color.Blue)) - Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        var neutral = 1.0 - Math.Min(1.0, chroma / 62.0);
        var latitudeWeight = SmoothStep(47.0, 70.0, Math.Abs(latitude));
        return Math.Clamp(Math.Pow(brightness, 1.12) * neutral * latitudeWeight, 0.0, 1.0);
    }

    private static double DetectVegetation(SKColor color, double water)
    {
        if (water > 0.5) return 0.0;
        return Math.Clamp(((color.Green - Math.Max(color.Red, color.Blue)) / 255.0 - 0.02) / 0.18, 0.0, 1.0);
    }

    private static double DetectDesert(SKColor color, double water, double vegetation)
    {
        if (water > 0.5) return 0.0;
        return Math.Clamp(((color.Red - color.Blue) / 255.0 - 0.07) / 0.30, 0.0, 1.0) * (1.0 - vegetation);
    }

    private static double EstimateHeight(SKColor color, double water, double ice)
    {
        if (water > 0.55) return 0.0;
        var luminance = (0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue) / 255.0;
        var warmRock = Math.Max(0.0, (color.Red - color.Blue) / 255.0);
        return Math.Clamp(0.48 * luminance + 0.38 * warmRock + 0.14 * ice, 0.0, 1.0);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
