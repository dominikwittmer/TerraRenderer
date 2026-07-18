using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

namespace TerraRenderer.AssetBuilder;

internal sealed class AssetPipeline(AssetBuilderConfiguration config, string configDirectory)
{
    private readonly int _width = ValidateDimension(config.Output.Width, nameof(config.Output.Width));
    private readonly int _height = ValidateDimension(config.Output.Height, nameof(config.Output.Height));
    private GeoTiffReadInfo? _elevationInfo;

    public void Run()
    {
        var output = Resolve(config.Output.Directory);
        Directory.CreateDirectory(output);

        using var day = LoadRequiredImage(config.Sources.DayAlbedo, "day albedo");
        using var night = LoadRequiredImage(config.Sources.NightLights, "night lights");

        Console.WriteLine($"Output resolution: {_width} x {_height}");
        Console.WriteLine("Generating albedo...");
        ImageTools.SaveJpeg(day, Path.Combine(output, "earth_albedo.jpg"), config.Output.JpegQuality);

        Console.WriteLine("Generating night emission and bloom...");
        using var intensity = BuildNightIntensity(night);
        using var emission = ColorizeNight(intensity);
        using var bloom = BoxBlur(BuildBloomSeed(intensity), config.Night.BloomRadiusPixels);
        ImageTools.SavePng(intensity, Path.Combine(output, "earth_night_intensity.png"));
        ImageTools.SavePng(emission, Path.Combine(output, "earth_night_emission.png"));
        ImageTools.SavePng(bloom, Path.Combine(output, "earth_night_bloom.png"));

        Raster<float>? elevation = null;
        if (!string.IsNullOrWhiteSpace(config.Sources.ElevationGeoTiff))
        {
            var elevationPath = Resolve(config.Sources.ElevationGeoTiff);
            Console.WriteLine($"Reading elevation: {elevationPath}");
            var elevationResult = GeoTiffElevationReader.ReadResampled(
                elevationPath, _width, _height, config.Height.TileCacheCapacity);
            elevation = elevationResult.Raster;
            _elevationInfo = elevationResult.Info;

            Console.WriteLine("Pre-filtering elevation for stable derived maps...");
            var filteredElevation = GaussianBlurRaster(elevation, config.Height.PreFilterSigmaPixels);
            Console.WriteLine("Generating height, water, normal, slope, curvature and horizon AO maps...");
            using var height = BuildHeightMap(elevation);
            using var water = BuildWaterMask(elevation);
            using var normal = BuildNormalMap(filteredElevation);
            using var slope = BuildSlopeMap(filteredElevation);
            using var curvature = BuildCurvatureMap(filteredElevation);
            using var ambientOcclusion = BuildAmbientOcclusionMap(filteredElevation);
            ImageTools.SavePng(height, Path.Combine(output, "earth_height.png"));
            ImageTools.SavePng(water, Path.Combine(output, "earth_watermask.png"));
            ImageTools.SavePng(normal, Path.Combine(output, "earth_normal.png"));
            ImageTools.SavePng(slope, Path.Combine(output, "earth_slope.png"));
            ImageTools.SavePng(curvature, Path.Combine(output, "earth_curvature.png"));
            ImageTools.SavePng(ambientOcclusion, Path.Combine(output, "earth_ambient_occlusion.png"));
            WriteHeightPgm16(elevation, Path.Combine(output, "earth_height_16bit.pgm"));
        }
        else
        {
            Console.WriteLine("No elevation GeoTIFF configured; height-derived assets are skipped.");
        }

        Console.WriteLine("Generating packed material map...");
        using var material = BuildMaterialMap(day, elevation);
        ImageTools.SavePng(material, Path.Combine(output, "earth_material.png"));
        WritePackedMapDiagnostics(material, output);

        WriteDiagnosticsContactSheet(output);
        WriteManifest(output);
        Console.WriteLine($"Asset build completed: {output}");
    }

    private SKBitmap LoadRequiredImage(string? configuredPath, string label)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException($"No {label} source configured.");
        var path = Resolve(configuredPath);
        Console.WriteLine($"Loading {label}: {path}");
        return ImageTools.LoadAndResize(path, _width, _height);
    }

    private SKBitmap BuildNightIntensity(SKBitmap night)
    {
        var result = NewBitmap();
        var black = config.Night.BlackPoint;
        var span = Math.Max(1e-6, config.Night.WhitePoint - black);
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var value = Math.Clamp((ImageTools.Luminance(night.GetPixel(x, y)) - black) / span, 0.0, 1.0);
            value = Math.Pow(value, config.Night.Gamma);
            var b = ImageTools.ToByte(value);
            result.SetPixel(x, y, new SKColor(b, b, b));
        }
        return result;
    }

    private SKBitmap ColorizeNight(SKBitmap intensity)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var value = intensity.GetPixel(x, y).Red / 255.0;
            result.SetPixel(x, y, new SKColor(
                (byte)Math.Round(config.Night.WarmRed * value),
                (byte)Math.Round(config.Night.WarmGreen * value),
                (byte)Math.Round(config.Night.WarmBlue * value)));
        }
        return result;
    }

    private SKBitmap BuildBloomSeed(SKBitmap intensity)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var value = intensity.GetPixel(x, y).Red / 255.0;
            value = Math.Clamp((value - config.Night.BloomThreshold) / Math.Max(1e-6, 1.0 - config.Night.BloomThreshold), 0.0, 1.0);
            var b = ImageTools.ToByte(value);
            result.SetPixel(x, y, new SKColor(b, b, b));
        }
        return result;
    }

    private SKBitmap BoxBlur(SKBitmap source, int radius)
    {
        if (radius <= 0) return source.Copy();
        var temp = NewBitmap();
        var result = NewBitmap();
        BlurPass(source, temp, radius, horizontal: true);
        BlurPass(temp, result, radius, horizontal: false);
        temp.Dispose();
        return result;
    }

    private void BlurPass(SKBitmap source, SKBitmap target, int radius, bool horizontal)
    {
        var count = radius * 2 + 1;
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var sum = 0;
            for (var i = -radius; i <= radius; i++)
            {
                var sx = horizontal ? (x + i + _width) % _width : x;
                var sy = horizontal ? y : Math.Clamp(y + i, 0, _height - 1);
                sum += source.GetPixel(sx, sy).Red;
            }
            var b = (byte)(sum / count);
            target.SetPixel(x, y, new SKColor(b, b, b));
        }
    }

    private SKBitmap BuildHeightMap(Raster<float> source)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var meters = SampleElevation(source, x, y);
            var normalized = NormalizeHeight(meters);
            var b = ImageTools.ToByte(normalized);
            result.SetPixel(x, y, new SKColor(b, b, b));
        }
        return result;
    }

    private SKBitmap BuildWaterMask(Raster<float> source)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var water = SampleElevation(source, x, y) <= 0.0f ? (byte)255 : (byte)0;
            result.SetPixel(x, y, new SKColor(water, water, water));
        }
        return result;
    }

    private Raster<float> GaussianBlurRaster(Raster<float> source, double sigma)
    {
        if (sigma <= 0.01) return source;
        var radius = Math.Clamp((int)Math.Ceiling(sigma * 3.0), 1, 12);
        var kernel = new double[radius * 2 + 1];
        double sum = 0;
        for (var i = -radius; i <= radius; i++)
        {
            var w = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
            kernel[i + radius] = w;
            sum += w;
        }
        for (var i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        var temp = new Raster<float>(source.Width, source.Height);
        var result = new Raster<float>(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            double value = 0;
            for (var k = -radius; k <= radius; k++)
            {
                var sx = (x + k + source.Width) % source.Width;
                value += source[sx, y] * kernel[k + radius];
            }
            temp[x, y] = (float)value;
        }
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            double value = 0;
            for (var k = -radius; k <= radius; k++)
            {
                var sy = Math.Clamp(y + k, 0, source.Height - 1);
                value += temp[x, sy] * kernel[k + radius];
            }
            result[x, y] = (float)value;
        }
        return result;
    }

    private (double Dx, double Dy, double MetersPerPixelX, double MetersPerPixelY) HeightGradient(Raster<float> source, int x, int y)
    {
        const double earthRadiusMeters = 6371008.8;
        var latitude = 90.0 - (y + 0.5) / _height * 180.0;
        var latRadians = latitude * Math.PI / 180.0;
        var metersPerPixelY = Math.PI * earthRadiusMeters / _height;
        var metersPerPixelX = 2.0 * Math.PI * earthRadiusMeters * Math.Max(0.02, Math.Cos(latRadians)) / _width;
        var left = EffectiveHeight(SampleDerivedElevation(source, x - 1, y));
        var right = EffectiveHeight(SampleDerivedElevation(source, x + 1, y));
        var up = EffectiveHeight(SampleDerivedElevation(source, x, y - 1));
        var down = EffectiveHeight(SampleDerivedElevation(source, x, y + 1));
        var dx = (right - left) / (2.0 * metersPerPixelX);
        var dy = (down - up) / (2.0 * metersPerPixelY);
        return (dx, dy, metersPerPixelX, metersPerPixelY);
    }

    private SKBitmap BuildNormalMap(Raster<float> source)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var gradient = HeightGradient(source, x, y);
            var nx = -gradient.Dx * config.Height.NormalStrength;
            var ny = -gradient.Dy * config.Height.NormalStrength;
            const double nz = 1.0;
            var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            result.SetPixel(x, y, new SKColor(
                ImageTools.ToByte(nx / length * 0.5 + 0.5),
                ImageTools.ToByte(ny / length * 0.5 + 0.5),
                ImageTools.ToByte(nz / length * 0.5 + 0.5)));
        }
        return result;
    }

    private SKBitmap BuildSlopeMap(Raster<float> source)
    {
        var result = NewBitmap();
        var reference = Math.Max(1.0, config.Height.SlopeReferenceDegrees);
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var g = HeightGradient(source, x, y);
            var slopeDegrees = Math.Atan(Math.Sqrt(g.Dx * g.Dx + g.Dy * g.Dy)) * 180.0 / Math.PI;
            var value = Math.Clamp(slopeDegrees / reference, 0.0, 1.0);
            var b = ImageTools.ToByte(value);
            result.SetPixel(x, y, new SKColor(b, b, b));
        }
        return result;
    }

    private SKBitmap BuildCurvatureMap(Raster<float> source)
    {
        var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var center = EffectiveHeight(SampleDerivedElevation(source, x, y));
            var left = EffectiveHeight(SampleDerivedElevation(source, x - 1, y));
            var right = EffectiveHeight(SampleDerivedElevation(source, x + 1, y));
            var up = EffectiveHeight(SampleDerivedElevation(source, x, y - 1));
            var down = EffectiveHeight(SampleDerivedElevation(source, x, y + 1));
            var laplacian = left + right + up + down - 4.0 * center;
            var curvature = Math.Tanh(laplacian / 900.0 * config.Height.CurvatureStrength);
            var value = 0.5 + 0.5 * curvature;
            var b = ImageTools.ToByte(value);
            result.SetPixel(x, y, new SKColor(b, b, b));
        }
        return result;
    }

    private SKBitmap BuildAmbientOcclusionMap(Raster<float> source)
    {
        const double earthCircumferenceKm = 40075.017;
        var result = NewBitmap();
        var directions = Math.Clamp(config.Height.AmbientOcclusionDirections, 4, 32);
        var steps = Math.Clamp(config.Height.AmbientOcclusionSteps, 2, 16);
        var radiusKm = Math.Max(1.0, config.Height.AmbientOcclusionRadiusKm);
        for (var y = 0; y < _height; y++)
        {
            var latitude = 90.0 - (y + 0.5) / _height * 180.0;
            var cosLatitude = Math.Max(0.08, Math.Cos(latitude * Math.PI / 180.0));
            var kmPerPixelY = earthCircumferenceKm / (2.0 * _height);
            var kmPerPixelX = earthCircumferenceKm * cosLatitude / _width;
            for (var x = 0; x < _width; x++)
            {
                var center = EffectiveHeight(SampleDerivedElevation(source, x, y));
                double horizonSum = 0.0;
                for (var d = 0; d < directions; d++)
                {
                    var angle = d * Math.PI * 2.0 / directions;
                    double maxAngle = 0.0;
                    for (var step = 1; step <= steps; step++)
                    {
                        var distanceKm = radiusKm * step / steps;
                        var ox = Math.Cos(angle) * distanceKm / kmPerPixelX;
                        var oy = Math.Sin(angle) * distanceKm / kmPerPixelY;
                        var neighbour = EffectiveHeight(SampleDerivedElevation(source, (int)Math.Round(x + ox), (int)Math.Round(y + oy)));
                        var elevationAngle = Math.Atan2(neighbour - center, distanceKm * 1000.0);
                        if (elevationAngle > maxAngle) maxAngle = elevationAngle;
                    }
                    horizonSum += Math.Sin(Math.Max(0.0, maxAngle));
                }
                var obstruction = horizonSum / directions;
                var ao = Math.Exp(-obstruction * 5.0 * config.Height.AmbientOcclusionStrength);
                ao = Math.Clamp(ao, 0.35, 1.0);
                var b = ImageTools.ToByte(ao);
                result.SetPixel(x, y, new SKColor(b, b, b));
            }
            if ((y + 1) % Math.Max(1, _height / 10) == 0 || y + 1 == _height)
                Console.WriteLine($"Ambient occlusion: {(y + 1) * 100 / _height}%");
        }
        return result;
    }

    private void WritePackedMapDiagnostics(SKBitmap material, string output)
    {
        SaveChannel(material, Path.Combine(output, "diagnostics_material_water.png"), c => c.Red);
        SaveChannel(material, Path.Combine(output, "diagnostics_material_vegetation.png"), c => c.Green);
        SaveChannel(material, Path.Combine(output, "diagnostics_material_desert.png"), c => c.Blue);
        SaveChannel(material, Path.Combine(output, "diagnostics_material_ice.png"), c => c.Alpha);
        var normalPath = Path.Combine(output, "earth_normal.png");
        if (File.Exists(normalPath))
        {
            using var normal = SKBitmap.Decode(normalPath);
            if (normal is not null)
            {
                SaveChannel(normal, Path.Combine(output, "diagnostics_normal_x.png"), c => c.Red);
                SaveChannel(normal, Path.Combine(output, "diagnostics_normal_y.png"), c => c.Green);
                SaveChannel(normal, Path.Combine(output, "diagnostics_normal_z.png"), c => c.Blue);
            }
        }
    }

    private void SaveChannel(SKBitmap source, string path, Func<SKColor, byte> selector)
    {
        using var result = NewBitmap();
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var value = selector(source.GetPixel(x, y));
            result.SetPixel(x, y, new SKColor(value, value, value));
        }
        ImageTools.SavePng(result, path);
    }

    private void WriteDiagnosticsContactSheet(string output)
    {
        var names = new[]
        {
            "earth_albedo.jpg", "earth_night_intensity.png", "earth_night_emission.png",
            "earth_night_bloom.png", "earth_material.png", "earth_height.png",
            "earth_normal.png", "earth_slope.png", "earth_curvature.png", "earth_ambient_occlusion.png",
            "earth_watermask.png", "diagnostics_material_water.png", "diagnostics_material_vegetation.png",
            "diagnostics_material_desert.png", "diagnostics_material_ice.png", "diagnostics_normal_x.png",
            "diagnostics_normal_y.png", "diagnostics_normal_z.png"
        };
        var files = names.Select(name => Path.Combine(output, name)).Where(File.Exists).ToArray();
        if (files.Length == 0) return;

        const int cellWidth = 480;
        const int cellHeight = 270;
        const int labelHeight = 34;
        const int columns = 2;
        var rows = (files.Length + columns - 1) / columns;
        using var sheet = new SKBitmap(columns * cellWidth, rows * (cellHeight + labelHeight), SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(18, 18, 18));
        using var imagePaint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var textFont = new SKFont(SKTypeface.Default, 20);
        for (var i = 0; i < files.Length; i++)
        {
            using var bitmap = SKBitmap.Decode(files[i]);
            if (bitmap is null) continue;
            var col = i % columns;
            var row = i / columns;
            var left = col * cellWidth;
            var top = row * (cellHeight + labelHeight);
            canvas.DrawBitmap(
                bitmap,
                new SKRect(left, top, left + cellWidth, top + cellHeight),
                sampling,
                imagePaint);
            canvas.DrawText(
                Path.GetFileName(files[i]),
                left + 10,
                top + cellHeight + 24,
                SKTextAlign.Left,
                textFont,
                textPaint);
        }
        ImageTools.SavePng(sheet, Path.Combine(output, "diagnostics_contact_sheet.png"));
    }

    private SKBitmap BuildMaterialMap(SKBitmap day, Raster<float>? elevation)
    {
        var result = new SKBitmap(_width, _height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var c = day.GetPixel(x, y);
            var lat = 90.0 - (y + 0.5) / _height * 180.0;
            var ocean = elevation is not null ? (SampleElevation(elevation, x, y) <= 0 ? 1.0 : 0.0) : DetectOcean(c);
            var brightness = ImageTools.Luminance(c);
            var chroma = (Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue))) / 255.0;
            var ice = Math.Clamp((Math.Abs(lat) - config.Materials.IceLatitudeStart) / 25.0, 0.0, 1.0)
                * Math.Clamp((brightness - config.Materials.IceBrightnessThreshold) / 0.25, 0.0, 1.0)
                * (1.0 - chroma);
            var vegetation = ocean > 0.5 ? 0.0 : Math.Clamp(((c.Green - Math.Max(c.Red, c.Blue)) / 255.0 - config.Materials.VegetationGreenThreshold) / 0.18, 0.0, 1.0);
            var desert = ocean > 0.5 ? 0.0 : Math.Clamp(((c.Red - c.Blue) / 255.0 - config.Materials.DesertWarmthThreshold) / 0.30, 0.0, 1.0) * (1.0 - vegetation);
            result.SetPixel(x, y, new SKColor(ImageTools.ToByte(ocean), ImageTools.ToByte(vegetation), ImageTools.ToByte(desert), ImageTools.ToByte(ice)));
        }
        return result;
    }

    private static double DetectOcean(SKColor c)
    {
        var blue = (c.Blue - Math.Max(c.Red, c.Green)) / 255.0;
        return Math.Clamp((blue + 0.02) / 0.18, 0.0, 1.0);
    }

    private static float SampleDerivedElevation(Raster<float> source, int x, int y)
    {
        var sx = x % source.Width;
        if (sx < 0) sx += source.Width;
        var sy = Math.Clamp(y, 0, source.Height - 1);
        return source[sx, sy];
    }

    private float SampleElevation(Raster<float> source, int x, int y)
    {
        var sx = ((x + 0.5) / _width * source.Width) - 0.5;
        var sy = ((Math.Clamp(y, 0, _height - 1) + 0.5) / _height * source.Height) - 0.5;
        return source.SampleWrappedBilinear(sx, sy);
    }

    private double EffectiveHeight(float meters) =>
        config.Height.IncludeBathymetryInNormals ? meters : Math.Max(0.0, meters);

    private double NormalizeHeight(float meters) => Math.Clamp(
        (meters - config.Height.MinimumMeters) / Math.Max(1.0, config.Height.MaximumMeters - config.Height.MinimumMeters), 0.0, 1.0);

    private void WriteHeightPgm16(Raster<float> source, string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{_width} {_height}\n65535\n"));
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var value = (ushort)Math.Clamp(Math.Round(NormalizeHeight(SampleElevation(source, x, y)) * 65535.0), 0, 65535);
            writer.Write((byte)(value >> 8));
            writer.Write((byte)(value & 0xff));
        }
    }

    private void WriteManifest(string output)
    {
        var files = Directory.GetFiles(output).OrderBy(Path.GetFileName).Select(path => new
        {
            file = Path.GetFileName(path),
            bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
        });
        var manifest = new
        {
            formatVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            width = _width,
            height = _height,
            heightRangeMeters = new[] { config.Height.MinimumMeters, config.Height.MaximumMeters },
            elevationSource = _elevationInfo is null ? null : new
            {
                width = _elevationInfo.SourceWidth,
                height = _elevationInfo.SourceHeight,
                tiled = _elevationInfo.IsTiled,
                blockWidth = _elevationInfo.BlockWidth,
                blockHeight = _elevationInfo.BlockHeight,
                bitsPerSample = _elevationInfo.BitsPerSample,
                sampleFormat = _elevationInfo.SampleFormat.ToString(),
                noData = _elevationInfo.NoDataValue
            },
            files
        };
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private SKBitmap NewBitmap() => new(_width, _height, SKColorType.Rgba8888, SKAlphaType.Opaque);

    private string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(configDirectory, path));

    private static int ValidateDimension(int value, string name) => value is >= 64 and <= 32768
        ? value
        : throw new ArgumentOutOfRangeException(name, "Dimension must be between 64 and 32768 pixels.");
}
