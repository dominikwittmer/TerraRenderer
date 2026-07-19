using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.PostProcessing;

internal static class EarthPostProcessor
{
    public static SKBitmap Finish(
        SKBitmap source,
        int width,
        int height,
        PostProcessingConfiguration post,
        ToneMappingConfiguration tone,
        BloomConfiguration bloom,
        bool enableToneMapping)
    {
        using var reduced = source.Width == width && source.Height == height
            ? source.Copy()
            : Downsample(source, width, height);

        using var cinematic = ProcessLinearFrame(reduced, tone, bloom, enableToneMapping);

        if (post.SharpenStrength <= 0 && post.LocalContrast <= 0)
            return cinematic.Copy();

        return Sharpen(cinematic, post.SharpenStrength, post.LocalContrast);
    }

    private static SKBitmap ProcessLinearFrame(
        SKBitmap source,
        ToneMappingConfiguration tone,
        BloomConfiguration bloom,
        bool enableToneMapping)
    {
        var width = source.Width;
        var height = source.Height;
        var count = width * height;
        var r = new float[count];
        var g = new float[count];
        var b = new float[count];
        var active = new bool[count];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * width + x;
            var c = source.GetPixel(x, y);
            active[i] = c.Alpha > 0 && (c.Red > 0 || c.Green > 0 || c.Blue > 0);
            r[i] = (float)ToLinear(c.Red / 255.0);
            g[i] = (float)ToLinear(c.Green / 255.0);
            b[i] = (float)ToLinear(c.Blue / 255.0);
        }

        if (bloom.Enabled && bloom.Intensity > 0)
            ApplyBloom(r, g, b, active, width, height, bloom);

        var output = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var exposure = Math.Max(0.001, tone.Exposure);
        var black = Math.Clamp(tone.BlackLevel, 0.0, 0.25);
        var white = Math.Max(0.1, tone.WhitePoint);
        var acesStrength = Math.Clamp(tone.AcESStrength, 0.0, 1.0);
        var invGamma = 1.0 / Math.Max(1.0, tone.DisplayGamma);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * width + x;
            if (!active[i] && r[i] < 1e-7f && g[i] < 1e-7f && b[i] < 1e-7f)
            {
                output.SetPixel(x, y, SKColors.Black);
                continue;
            }

            var rr = Math.Max(0.0, r[i] * exposure - black) / white;
            var gg = Math.Max(0.0, g[i] * exposure - black) / white;
            var bb = Math.Max(0.0, b[i] * exposure - black) / white;

            if (enableToneMapping)
            {
                rr = Lerp(Reinhard(rr), AcesFitted(rr), acesStrength);
                gg = Lerp(Reinhard(gg), AcesFitted(gg), acesStrength);
                bb = Lerp(Reinhard(bb), AcesFitted(bb), acesStrength);
            }

            var luminance = 0.2126 * rr + 0.7152 * gg + 0.0722 * bb;
            rr = 0.5 + (rr - 0.5) * tone.Contrast;
            gg = 0.5 + (gg - 0.5) * tone.Contrast;
            bb = 0.5 + (bb - 0.5) * tone.Contrast;
            rr = luminance + (rr - luminance) * tone.Saturation;
            gg = luminance + (gg - luminance) * tone.Saturation;
            bb = luminance + (bb - luminance) * tone.Saturation;

            rr = Math.Pow(Math.Clamp(rr, 0.0, 1.0), invGamma * 2.2);
            gg = Math.Pow(Math.Clamp(gg, 0.0, 1.0), invGamma * 2.2);
            bb = Math.Pow(Math.Clamp(bb, 0.0, 1.0), invGamma * 2.2);

            output.SetPixel(x, y, new SKColor(ToByte(rr * 255.0), ToByte(gg * 255.0), ToByte(bb * 255.0), 255));
        }

        return output;
    }

    private static void ApplyBloom(
        float[] r,
        float[] g,
        float[] b,
        bool[] active,
        int width,
        int height,
        BloomConfiguration config)
    {
        var count = width * height;
        var br = new float[count];
        var bg = new float[count];
        var bb = new float[count];
        var threshold = Math.Max(0.0, config.Threshold);
        var knee = Math.Max(0.001, config.Knee);

        for (var i = 0; i < count; i++)
        {
            if (!active[i]) continue;
            var luminance = 0.2126 * r[i] + 0.7152 * g[i] + 0.0722 * b[i];
            var soft = Math.Clamp((luminance - threshold + knee) / (2.0 * knee), 0.0, 1.0);
            var contribution = Math.Max(luminance - threshold, 0.0) + soft * soft * knee;
            contribution /= Math.Max(luminance, 1e-5);

            var nightBias = 1.0 + config.NightBoost * Math.Clamp((0.34 - luminance) / 0.34, 0.0, 1.0);
            br[i] = (float)(r[i] * contribution * nightBias);
            bg[i] = (float)(g[i] * contribution * nightBias);
            bb[i] = (float)(b[i] * contribution * nightBias);
        }

        var radius = Math.Clamp((int)Math.Round(config.Radius), 1, 64);
        BoxBlur(br, width, height, radius, 2);
        BoxBlur(bg, width, height, radius, 2);
        BoxBlur(bb, width, height, radius, 2);

        float[]? wr = null;
        float[]? wg = null;
        float[]? wb = null;
        if (config.WideIntensity > 0 && config.WideRadius > config.Radius)
        {
            wr = (float[])br.Clone();
            wg = (float[])bg.Clone();
            wb = (float[])bb.Clone();
            var wideRadius = Math.Clamp((int)Math.Round(config.WideRadius), radius + 1, 96);
            BoxBlur(wr, width, height, wideRadius, 2);
            BoxBlur(wg, width, height, wideRadius, 2);
            BoxBlur(wb, width, height, wideRadius, 2);
        }

        var warmth = Math.Clamp(config.Warmth, -0.5, 0.5);
        for (var i = 0; i < count; i++)
        {
            var wideR = wr is null ? 0.0 : wr[i] * config.WideIntensity;
            var wideG = wg is null ? 0.0 : wg[i] * config.WideIntensity;
            var wideB = wb is null ? 0.0 : wb[i] * config.WideIntensity;
            var bloomR = br[i] * config.Intensity + wideR;
            var bloomG = bg[i] * config.Intensity + wideG;
            var bloomB = bb[i] * config.Intensity + wideB;

            r[i] += (float)(bloomR * (1.0 + 0.55 * warmth));
            g[i] += (float)(bloomG * (1.0 + 0.12 * warmth));
            b[i] += (float)(bloomB * (1.0 - 0.35 * warmth));
        }
    }

    private static void BoxBlur(float[] values, int width, int height, int radius, int passes)
    {
        var temp = new float[values.Length];
        for (var pass = 0; pass < passes; pass++)
        {
            BlurHorizontal(values, temp, width, height, radius);
            BlurVertical(temp, values, width, height, radius);
        }
    }

    private static void BlurHorizontal(float[] source, float[] target, int width, int height, int radius)
    {
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            double sum = 0;
            for (var x = -radius; x <= radius; x++)
                sum += source[row + Math.Clamp(x, 0, width - 1)];

            var diameter = radius * 2 + 1;
            for (var x = 0; x < width; x++)
            {
                target[row + x] = (float)(sum / diameter);
                sum -= source[row + Math.Clamp(x - radius, 0, width - 1)];
                sum += source[row + Math.Clamp(x + radius + 1, 0, width - 1)];
            }
        }
    }

    private static void BlurVertical(float[] source, float[] target, int width, int height, int radius)
    {
        var diameter = radius * 2 + 1;
        for (var x = 0; x < width; x++)
        {
            double sum = 0;
            for (var y = -radius; y <= radius; y++)
                sum += source[Math.Clamp(y, 0, height - 1) * width + x];

            for (var y = 0; y < height; y++)
            {
                target[y * width + x] = (float)(sum / diameter);
                sum -= source[Math.Clamp(y - radius, 0, height - 1) * width + x];
                sum += source[Math.Clamp(y + radius + 1, 0, height - 1) * width + x];
            }
        }
    }

    private static SKBitmap Downsample(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var sx = source.Width / (double)width;
        var sy = source.Height / (double)height;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var x0 = (int)Math.Floor(x * sx);
            var x1 = Math.Max(x0 + 1, (int)Math.Ceiling((x + 1) * sx));
            var y0 = (int)Math.Floor(y * sy);
            var y1 = Math.Max(y0 + 1, (int)Math.Ceiling((y + 1) * sy));
            long rr = 0, gg = 0, bb = 0, aa = 0, n = 0;
            for (var yy = y0; yy < Math.Min(y1, source.Height); yy++)
            for (var xx = x0; xx < Math.Min(x1, source.Width); xx++)
            {
                var c = source.GetPixel(xx, yy);
                rr += c.Red;
                gg += c.Green;
                bb += c.Blue;
                aa += c.Alpha;
                n++;
            }

            result.SetPixel(x, y, new SKColor((byte)(rr / n), (byte)(gg / n), (byte)(bb / n), (byte)(aa / n)));
        }

        return result;
    }

    private static SKBitmap Sharpen(SKBitmap source, double strength, double localContrast)
    {
        var output = new SKBitmap(source.Info);
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var c = source.GetPixel(x, y);
            if (c.Alpha == 0)
            {
                output.SetPixel(x, y, c);
                continue;
            }

            double ar = 0, ag = 0, ab = 0, n = 0;
            for (var yy = Math.Max(0, y - 1); yy <= Math.Min(source.Height - 1, y + 1); yy++)
            for (var xx = Math.Max(0, x - 1); xx <= Math.Min(source.Width - 1, x + 1); xx++)
            {
                var q = source.GetPixel(xx, yy);
                ar += q.Red;
                ag += q.Green;
                ab += q.Blue;
                n++;
            }

            ar /= n;
            ag /= n;
            ab /= n;
            var lum = (c.Red + c.Green + c.Blue) / 3.0;
            var contrast = 1.0 + localContrast * Math.Abs(lum - 127.5) / 127.5;
            output.SetPixel(x, y, new SKColor(
                ToByte(127.5 + (c.Red + strength * (c.Red - ar) - 127.5) * contrast),
                ToByte(127.5 + (c.Green + strength * (c.Green - ag) - 127.5) * contrast),
                ToByte(127.5 + (c.Blue + strength * (c.Blue - ab) - 127.5) * contrast),
                c.Alpha));
        }

        return output;
    }

    private static double AcesFitted(double x)
    {
        const double a = 2.51;
        const double b = 0.03;
        const double c = 2.43;
        const double d = 0.59;
        const double e = 0.14;
        return Math.Clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
    }

    private static double Reinhard(double x) => x / (1.0 + x);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double ToLinear(double x) => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
