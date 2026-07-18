using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Assets;

public sealed class EarthAssetManager : IDisposable
{
    private readonly List<SKBitmap> _ownedTextures = [];

    public EarthMaterialAtlas Load(string baseDirectory, AssetConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(configuration);

        DisposeTextures();
        var day = LoadRequired(baseDirectory, configuration.DayTexture, "Tagtextur");
        var night = LoadRequired(baseDirectory, configuration.NightTexture, "Nachttextur");
        var elevation = LoadOptional(baseDirectory, configuration.ElevationTexture);
        var water = LoadOptional(baseDirectory, configuration.WaterMaskTexture);
        var ice = LoadOptional(baseDirectory, configuration.IceMaskTexture);
        var normal = LoadOptional(baseDirectory, configuration.NormalTexture);
        var material = LoadOptional(baseDirectory, configuration.MaterialTexture);
        var ao = LoadOptional(baseDirectory, configuration.AmbientOcclusionTexture);
        var bloom = LoadOptional(baseDirectory, configuration.NightBloomTexture);
        return new EarthMaterialAtlas(day, night, elevation, water, ice, normal, material, ao, bloom);
    }

    public void Dispose()
    {
        DisposeTextures();
        GC.SuppressFinalize(this);
    }

    private SKBitmap LoadRequired(string baseDirectory, string path, string label)
    {
        var fullPath = Resolve(baseDirectory, path);
        var bitmap = SKBitmap.Decode(fullPath)
            ?? throw new InvalidOperationException($"{label} konnte nicht geladen werden: {fullPath}");
        _ownedTextures.Add(bitmap);
        return bitmap;
    }

    private SKBitmap? LoadOptional(string baseDirectory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var fullPath = Resolve(baseDirectory, path);
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"Hinweis: Optionales Asset nicht gefunden, Fallback wird verwendet: {fullPath}");
            return null;
        }
        var bitmap = SKBitmap.Decode(fullPath);
        if (bitmap is not null) _ownedTextures.Add(bitmap);
        return bitmap;
    }

    private void DisposeTextures()
    {
        foreach (var texture in _ownedTextures) texture.Dispose();
        _ownedTextures.Clear();
    }

    private static string Resolve(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
}
