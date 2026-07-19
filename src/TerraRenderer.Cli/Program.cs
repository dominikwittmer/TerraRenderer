using System.Globalization;
using System.Text.Json;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering;

try
{
    var appBase = AppContext.BaseDirectory;
    var projectRoot = FindProjectRoot(appBase);
    var configPath = GetArgument(args, "--config") ?? Path.Combine(projectRoot, "config", "terrarenderer.json");
    var configuration = LoadConfiguration(configPath);
    var date = DateTimeOffset.ParseExact(configuration.DateUtc, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    var requestedLayout = GetArgument(args, "--layout") ?? configuration.DefaultLayout;
    var renderAll = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));
    var outputRoot = Path.Combine(projectRoot, "output");

    using var assets = new EarthAssetManager();
    var atlas = assets.Load(projectRoot, configuration.Assets);
    var renderer = new EarthRenderer();

    Console.WriteLine("TerraRenderer v0.98 – Camera & Composition");
    Console.WriteLine($"Konfiguration: {configPath}");
    Console.WriteLine($"Datum: {date:yyyy-MM-dd} UTC");
    Console.WriteLine($"Echtes Höhenmodell: {(atlas.UsesElevationTexture ? "ja" : "nein (Fallback)")}");
    Console.WriteLine(
        $"Wasserdaten: {(atlas.UsesMaterialTexture
            ? "ja (Materialtextur)"
            : atlas.UsesWaterMaskTexture
                ? "ja (separate Maske)"
                : "nein (Farberkennung/Fallback)")}");

    Console.WriteLine(
        $"Eisdaten: {(atlas.UsesMaterialTexture
            ? "ja (Materialtextur)"
            : atlas.UsesIceMaskTexture
                ? "ja (separate Maske)"
                : "nein (Farberkennung/Fallback)")}");

    foreach (var layout in SelectLayouts(configuration, requestedLayout))
    {
        if (renderAll)
        {
            var directory = Path.Combine(outputRoot, "frames", layout.Key);
            for (var minute = 0; minute < 24 * 60; minute += configuration.FrameIntervalMinutes)
            {
                var time = date.AddMinutes(minute);
                Render(renderer, atlas, configuration, layout, time, Path.Combine(directory, $"{time:HHmm}.png"));
                Console.WriteLine($"{layout.Key}: {time:HH:mm} UTC");
            }
        }
        else
        {
            var directory = Path.Combine(outputRoot, "tests", layout.Key);
            foreach (var hour in new[] { 0, 6, 12, 18 })
            {
                var time = date.AddHours(hour);
                var file = Path.Combine(directory, $"earth_{time:HHmm}.png");
                Render(renderer, atlas, configuration, layout, time, file);
                Console.WriteLine($"{layout.Key}: {time:HH:mm} UTC -> {file}");
            }
        }
    }

    Console.WriteLine(renderAll ? "Frame-Export abgeschlossen." : "Vier Testzeiten pro Layout abgeschlossen.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Fehler: {exception.Message}");
    return 1;
}

static void Render(EarthRenderer renderer, EarthMaterialAtlas atlas, TerraRendererConfiguration configuration,
    KeyValuePair<string, LayoutConfiguration> layout, DateTimeOffset time, string outputPath)
{
    renderer.Render(atlas, outputPath, new RenderRequest
    {
        TimeUtc = time,
        Layout = layout.Value,
        Rendering = configuration.Rendering
    });
}

static TerraRendererConfiguration LoadConfiguration(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("Konfigurationsdatei nicht gefunden.", path);
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<TerraRendererConfiguration>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    }) ?? throw new InvalidOperationException("Konfiguration konnte nicht gelesen werden.");
}

static IEnumerable<KeyValuePair<string, LayoutConfiguration>> SelectLayouts(TerraRendererConfiguration configuration, string requested)
{
    if (requested.Equals("both", StringComparison.OrdinalIgnoreCase)) return configuration.Layouts;
    if (!configuration.Layouts.TryGetValue(requested, out var layout))
        throw new ArgumentException($"Unbekanntes Layout '{requested}'. Verfügbar: {string.Join(", ", configuration.Layouts.Keys)}");
    return [new KeyValuePair<string, LayoutConfiguration>(requested, layout)];
}

static string? GetArgument(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
        if (values[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return values[i + 1];
    return null;
}

static string FindProjectRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "TerraRenderer.slnx"))) return current.FullName;
        current = current.Parent;
    }
    return Path.GetFullPath(Path.Combine(start, "..", "..", "..", "..", ".."));
}
