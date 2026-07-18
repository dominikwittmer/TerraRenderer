using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Astronomy;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;
using TerraRenderer.Core.Projection;
using TerraRenderer.Rendering.Atmosphere;
using TerraRenderer.Rendering.Lighting;
using TerraRenderer.Rendering.Materials;
using TerraRenderer.Rendering.ToneMapping;

namespace TerraRenderer.Rendering;

public sealed class EarthRenderer
{
    public void Render(EarthMaterialAtlas atlas, string outputPath, RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(request);

        using var output = new SKBitmap(request.Layout.Width, request.Layout.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        output.Erase(SKColors.Black);

        var sunPosition = SunCalculator.Calculate(request.TimeUtc);
        var sun = ToCartesian(sunPosition.SubsolarLatitudeDegrees, sunPosition.SubsolarLongitudeDegrees);

        if (request.Layout.Projection.Equals("orthographic", StringComparison.OrdinalIgnoreCase))
            RenderOrthographic(output, atlas, request.Layout, request.Rendering, sun);
        else
            RenderPerspective(output, atlas, request.Layout, request.Rendering, sun);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static void RenderPerspective(SKBitmap output, EarthMaterialAtlas atlas, LayoutConfiguration layout,
        RenderingConfiguration config, Vector3d sun)
    {
        var projection = new PerspectiveEarthProjection(layout.Width, layout.Height,
            layout.CenterLatitude, layout.CenterLongitude, layout.CameraDistance,
            layout.FieldOfViewDegrees, layout.CameraRollDegrees);

        var centerX = (layout.Width - 1) / 2.0 + layout.GlobeOffsetX * layout.Width;
        var centerY = (layout.Height - 1) / 2.0 + layout.GlobeOffsetY * layout.Height;
        var halfWidth = layout.Width / 2.0;
        var halfHeight = layout.Height / 2.0;
        var atmosphereRadius = 1.0 + config.Atmosphere.Thickness;

        for (var y = 0; y < layout.Height; y++)
        {
            for (var x = 0; x < layout.Width; x++)
            {
                var normalizedX = (x - centerX) / halfWidth;
                var normalizedY = (centerY - y) / halfHeight;
                var ray = projection.CreateRay(normalizedX, normalizedY);

                if (PerspectiveEarthProjection.TryIntersect(ray, 1.0, out var earthHit))
                {
                    var coordinate = PerspectiveEarthProjection.ToGeoCoordinate(earthHit.Normal);
                    var view = (ray.Origin - earthHit.Point).Normalize();
                    var limbCosine = Math.Clamp(Vector3d.Dot(earthHit.Normal, view), 0.0, 1.0);
                    var color = ShadeSurface(atlas, coordinate, earthHit.Normal, view, sun, limbCosine, config);
                    output.SetPixel(x, y, color);
                    continue;
                }

                if (!config.EnableAtmosphere ||
                    !PerspectiveEarthProjection.TryIntersect(ray, atmosphereRadius, out _))
                    continue;

                // For a shell-only pixel, the closest point of the ray to the planet centre
                // gives a stable radial altitude and the correct local lighting direction.
                var closestDistance = Math.Max(0.0, -Vector3d.Dot(ray.Origin, ray.Direction));
                var closestPoint = ray.Origin + ray.Direction * closestDistance;
                var radialDistance = closestPoint.Length;
                var altitude = Math.Clamp((radialDistance - 1.0) / config.Atmosphere.Thickness, 0.0, 1.0);
                var edgeNormal = closestPoint.Normalize();
                var edgeLight = Vector3d.Dot(edgeNormal, sun);
                output.SetPixel(x, y, EarthAtmosphere.OuterGlow(altitude, edgeLight, config.Atmosphere));
            }
        }
    }

    private static void RenderOrthographic(SKBitmap output, EarthMaterialAtlas atlas, LayoutConfiguration layout,
        RenderingConfiguration config, Vector3d sun)
    {
        var projection = new OrthographicProjection(layout.CenterLatitude, layout.CenterLongitude);
        var view = ToCartesian(layout.CenterLatitude, layout.CenterLongitude);
        var centerX = (layout.Width - 1) / 2.0 + layout.GlobeOffsetX * layout.Width;
        var centerY = (layout.Height - 1) / 2.0 + layout.GlobeOffsetY * layout.Height;
        var radius = Math.Min(layout.Width, layout.Height) * layout.GlobeFill / 2.0;
        var outerRadius = radius * (1.0 + config.Atmosphere.Thickness);

        for (var y = 0; y < layout.Height; y++)
        {
            for (var x = 0; x < layout.Width; x++)
            {
                var dx = x - centerX;
                var dy = centerY - y;
                var distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > radius)
                {
                    if (config.EnableAtmosphere && distance <= outerRadius && distance > 1e-9)
                    {
                        var edgeX = dx / distance * 0.9999;
                        var edgeY = dy / distance * 0.9999;
                        if (projection.TryUnproject(edgeX, edgeY, out var edgeCoordinate))
                        {
                            var edgeNormal = ToCartesian(edgeCoordinate.LatitudeDegrees, edgeCoordinate.LongitudeDegrees);
                            var edgeLight = Vector3d.Dot(edgeNormal, sun);
                            var altitude = (distance - radius) / (outerRadius - radius);
                            output.SetPixel(x, y, EarthAtmosphere.OuterGlow(altitude, edgeLight, config.Atmosphere));
                        }
                    }
                    continue;
                }

                var nx = dx / radius;
                var ny = dy / radius;
                if (!projection.TryUnproject(nx, ny, out var coordinate)) continue;

                var geometricNormal = ToCartesian(coordinate.LatitudeDegrees, coordinate.LongitudeDegrees);
                var sphereZ = Math.Sqrt(Math.Max(0.0, 1.0 - nx * nx - ny * ny));
                var color = ShadeSurface(atlas, coordinate, geometricNormal, view, sun, sphereZ, config);
                output.SetPixel(x, y, color);
            }
        }
    }

    private static SKColor ShadeSurface(EarthMaterialAtlas atlas, GeoCoordinate coordinate,
        Vector3d geometricNormal, Vector3d view, Vector3d sun, double limbCosine,
        RenderingConfiguration config)
    {
        var material = atlas.Sample(coordinate.LatitudeDegrees, coordinate.LongitudeDegrees);
        var normal = config.EnableRelief
            ? SurfaceNormalCalculator.Calculate(atlas, coordinate.LatitudeDegrees, coordinate.LongitudeDegrees,
                geometricNormal, config.ReliefSampleDegrees, config.ReliefStrength, config.NormalMapStrength)
            : geometricNormal;

        var emissionGlow = config.EnableNightLights
            ? atlas.SampleEmissionGlow(coordinate.LatitudeDegrees, coordinate.LongitudeDegrees,
                config.NightLightBlurDegrees)
            : SKColors.Black;

        var color = SurfaceLighting.Shade(material, emissionGlow, normal, geometricNormal, sun, view, config);
        var limbShade = 0.80 + 0.20 * Math.Pow(limbCosine, 0.62);
        color = Scale(color, limbShade);

        if (config.EnableAtmosphere)
        {
            var surfaceLight = Vector3d.Dot(geometricNormal, sun);
            color = EarthAtmosphere.ApplySurfaceHaze(color, limbCosine, surfaceLight, config.Atmosphere);
        }

        return config.EnableToneMapping
            ? EarthToneMapper.Apply(color, material, config.ToneMapping)
            : color;
    }

    private static Vector3d ToCartesian(double latitudeDegrees, double longitudeDegrees)
    {
        var lat = latitudeDegrees * Math.PI / 180.0;
        var lon = longitudeDegrees * Math.PI / 180.0;
        var cosLat = Math.Cos(lat);
        return new Vector3d(cosLat * Math.Cos(lon), cosLat * Math.Sin(lon), Math.Sin(lat));
    }

    private static SKColor Scale(SKColor color, double factor) => new(
        (byte)Math.Clamp(Math.Round(color.Red * factor), 0, 255),
        (byte)Math.Clamp(Math.Round(color.Green * factor), 0, 255),
        (byte)Math.Clamp(Math.Round(color.Blue * factor), 0, 255), 255);
}
