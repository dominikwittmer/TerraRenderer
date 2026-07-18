using SkiaSharp;

namespace TerraRenderer.AssetBuilder;

internal static class ImageTools
{
    public static SKBitmap LoadAndResize(string path, int width, int height)
    {
        using var source = SKBitmap.Decode(path) ?? throw new InvalidOperationException($"Cannot decode image: {path}");
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);
        using var paint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), sampling, paint);
        return result;
    }

    public static void SavePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public static void SaveJpeg(SKBitmap bitmap, string path, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 1, 100));
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public static double Luminance(SKColor c) =>
        (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255.0;

    public static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
}
