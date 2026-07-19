using SkiaSharp;
using TerraRenderer.Core.Configuration;

namespace TerraRenderer.Rendering.PostProcessing;

internal static class EarthPostProcessor
{
    public static SKBitmap Finish(SKBitmap source, int width, int height, PostProcessingConfiguration config)
    {
        var reduced = source.Width == width && source.Height == height ? source.Copy() : Downsample(source, width, height);
        if (config.SharpenStrength <= 0 && config.LocalContrast <= 0) return reduced;
        var result = Sharpen(reduced, config.SharpenStrength, config.LocalContrast);
        reduced.Dispose();
        return result;
    }

    private static SKBitmap Downsample(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var sx = source.Width / (double)width; var sy = source.Height / (double)height;
        for (var y=0;y<height;y++) for (var x=0;x<width;x++)
        {
            var x0=(int)Math.Floor(x*sx); var x1=Math.Max(x0+1,(int)Math.Ceiling((x+1)*sx));
            var y0=(int)Math.Floor(y*sy); var y1=Math.Max(y0+1,(int)Math.Ceiling((y+1)*sy));
            long r=0,g=0,b=0,a=0,n=0;
            for(var yy=y0;yy<Math.Min(y1,source.Height);yy++) for(var xx=x0;xx<Math.Min(x1,source.Width);xx++) { var c=source.GetPixel(xx,yy); r+=c.Red;g+=c.Green;b+=c.Blue;a+=c.Alpha;n++; }
            result.SetPixel(x,y,new SKColor((byte)(r/n),(byte)(g/n),(byte)(b/n),(byte)(a/n)));
        }
        return result;
    }

    private static SKBitmap Sharpen(SKBitmap source, double strength, double localContrast)
    {
        var output = new SKBitmap(source.Info);
        for(var y=0;y<source.Height;y++) for(var x=0;x<source.Width;x++)
        {
            var c=source.GetPixel(x,y);
            if(c.Alpha==0){output.SetPixel(x,y,c);continue;}
            double ar=0,ag=0,ab=0,n=0;
            for(var yy=Math.Max(0,y-1);yy<=Math.Min(source.Height-1,y+1);yy++) for(var xx=Math.Max(0,x-1);xx<=Math.Min(source.Width-1,x+1);xx++) {var q=source.GetPixel(xx,yy);ar+=q.Red;ag+=q.Green;ab+=q.Blue;n++;}
            ar/=n;ag/=n;ab/=n;
            var lum=(c.Red+c.Green+c.Blue)/3.0;
            var contrast=1.0+localContrast*Math.Abs(lum-127.5)/127.5;
            output.SetPixel(x,y,new SKColor(ToByte(127.5+(c.Red+strength*(c.Red-ar)-127.5)*contrast),ToByte(127.5+(c.Green+strength*(c.Green-ag)-127.5)*contrast),ToByte(127.5+(c.Blue+strength*(c.Blue-ab)-127.5)*contrast),c.Alpha));
        }
        return output;
    }
    private static byte ToByte(double v)=>(byte)Math.Clamp(Math.Round(v),0,255);
}
