using SkiaSharp;
using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Rendering.Clouds;

internal static class ProceduralCloudLayer
{
    public static SKColor Apply(SKColor surface, EarthSurfaceMaterial material, double latitudeDegrees,
        double longitudeDegrees, DateTimeOffset timeUtc, Vector3d geometricNormal, Vector3d sun,
        CloudConfiguration config)
    {
        var drift = timeUtc.ToUnixTimeSeconds() / 3600.0 * config.SpeedDegreesPerHour;
        var lon = Wrap(longitudeDegrees + drift);
        var lat = latitudeDegrees;

        var broad = Fractal(lat / 180.0 * config.Scale, lon / 360.0 * config.Scale, 4);
        var detail = Fractal(lat / 180.0 * config.DetailScale, lon / 360.0 * config.DetailScale, 3);
        var latitudeBand = 0.70 + 0.30 * Math.Pow(Math.Cos(lat * Math.PI / 180.0 * 1.7), 2.0);
        var weather = broad * 0.76 + detail * 0.24;
        var threshold = 1.0 - config.Coverage;
        var cloud = SmoothStep(threshold, Math.Min(0.98, threshold + 0.24), weather * latitudeBand);
        cloud = Math.Pow(cloud, 1.15) * config.Density;
        if (cloud < 0.002) return surface;

        var illumination = Vector3d.Dot(geometricNormal, sun);
        var daylight = SmoothStep(-0.18, 0.30, illumination);
        var edge = Math.Pow(Math.Clamp(detail - broad + 0.48, 0.0, 1.0), 2.2);
        var lining = config.SilverLining * edge * Math.Pow(Math.Max(0.0, illumination), 0.45);
        var brightness = config.NightVisibility + daylight * (config.DayBrightness + lining);

        var shadow = cloud * config.ShadowStrength * daylight * (0.70 + 0.30 * material.Water);
        var darkened = Scale(surface, 1.0 - shadow);
        var cloudColor = new SKColor(
            ToByte(205 + 45 * brightness),
            ToByte(214 + 38 * brightness),
            ToByte(224 + 30 * brightness), 255);
        var opacity = Math.Clamp(cloud * brightness, 0.0, 0.78);
        return Mix(darkened, cloudColor, opacity);
    }

    private static double Fractal(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 0.5, total = 0;
        for (var i = 0; i < octaves; i++)
        {
            sum += ValueNoise(x, y) * amplitude;
            total += amplitude;
            x = x * 2.03 + 17.1;
            y = y * 2.01 - 9.7;
            amplitude *= 0.5;
        }
        return sum / total;
    }

    private static double ValueNoise(double x, double y)
    {
        var ix = (int)Math.Floor(x); var iy = (int)Math.Floor(y);
        var fx = x - ix; var fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        var a = Hash(ix, iy); var b = Hash(ix + 1, iy);
        var c = Hash(ix, iy + 1); var d = Hash(ix + 1, iy + 1);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    private static double Hash(int x, int y)
    {
        unchecked
        {
            var n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (double)int.MaxValue;
        }
    }

    private static double Wrap(double value) => ((value + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double SmoothStep(double a, double b, double v) { var t = Math.Clamp((v-a)/(b-a),0,1); return t*t*(3-2*t); }
    private static SKColor Scale(SKColor c, double f) => new(ToByte(c.Red*f),ToByte(c.Green*f),ToByte(c.Blue*f),255);
    private static SKColor Mix(SKColor a, SKColor b, double t) => new(ToByte(a.Red+(b.Red-a.Red)*t),ToByte(a.Green+(b.Green-a.Green)*t),ToByte(a.Blue+(b.Blue-a.Blue)*t),255);
    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v),0,255);
}
