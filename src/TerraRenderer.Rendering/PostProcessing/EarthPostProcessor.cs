using SkiaSharp;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.PostProcessing;

internal static class EarthPostProcessor
{
    public static SKBitmap Finish(HdrFrame source, int width, int height, PostProcessingConfiguration post,
        ToneMappingConfiguration tone, BloomConfiguration bloom, bool enableToneMapping)
    {
        var frame = source.Width == width && source.Height == height ? source : Downsample(source,width,height);
        var r = new float[frame.Pixels.Length];
        var g = new float[frame.Pixels.Length];
        var b = new float[frame.Pixels.Length];
        for(var i=0;i<frame.Pixels.Length;i++){var c=frame.Pixels[i];r[i]=c.R;g[i]=c.G;b[i]=c.B;}
        if(bloom.Enabled && bloom.Intensity>0) ApplyBloom(r,g,b,width,height,bloom);
        var output=ToneMap(r,g,b,width,height,tone,enableToneMapping);
        if(post.SharpenStrength<=0 && post.LocalContrast<=0) return output;
        using(output) return Sharpen(output,post.SharpenStrength,post.LocalContrast);
    }

    private static SKBitmap ToneMap(float[] r,float[] g,float[] b,int width,int height,ToneMappingConfiguration tone,bool enabled)
    {
        var output=new SKBitmap(width,height,SKColorType.Rgba8888,SKAlphaType.Premul);
        var exposure=Math.Max(.001,tone.Exposure); var black=Math.Clamp(tone.BlackLevel,0,.25); var white=Math.Max(.1,tone.WhitePoint);
        var aces=Math.Clamp(tone.AcESStrength,0,1); var invGamma=1.0/Math.Max(1,tone.DisplayGamma);
        for(var y=0;y<height;y++) for(var x=0;x<width;x++)
        {
            var i=y*width+x; var rr=Math.Max(0,r[i]*exposure-black)/white; var gg=Math.Max(0,g[i]*exposure-black)/white; var bb=Math.Max(0,b[i]*exposure-black)/white;
            if(enabled){rr=Lerp(Reinhard(rr),AcesFitted(rr),aces);gg=Lerp(Reinhard(gg),AcesFitted(gg),aces);bb=Lerp(Reinhard(bb),AcesFitted(bb),aces);}
            var lum=.2126*rr+.7152*gg+.0722*bb;
            rr=.5+(rr-.5)*tone.Contrast; gg=.5+(gg-.5)*tone.Contrast; bb=.5+(bb-.5)*tone.Contrast;
            rr=lum+(rr-lum)*tone.Saturation; gg=lum+(gg-lum)*tone.Saturation; bb=lum+(bb-lum)*tone.Saturation;
            rr=Math.Pow(Math.Clamp(rr,0,1),invGamma*2.2); gg=Math.Pow(Math.Clamp(gg,0,1),invGamma*2.2); bb=Math.Pow(Math.Clamp(bb,0,1),invGamma*2.2);
            output.SetPixel(x,y,new SKColor(ToByte(rr*255),ToByte(gg*255),ToByte(bb*255),255));
        }
        return output;
    }

    private static void ApplyBloom(float[] r,float[] g,float[] b,int width,int height,BloomConfiguration config)
    {
        var n=r.Length; var br=new float[n];var bg=new float[n];var bb=new float[n];var threshold=Math.Max(0,config.Threshold);var knee=Math.Max(.001,config.Knee);
        for(var i=0;i<n;i++)
        {
            var lum=.2126*r[i]+.7152*g[i]+.0722*b[i];
            var soft=Math.Clamp((lum-threshold+knee)/(2*knee),0,1);
            var contribution=Math.Max(lum-threshold,0)+soft*soft*knee;
            contribution/=Math.Max(lum,1e-6);
            br[i]=(float)(r[i]*contribution);bg[i]=(float)(g[i]*contribution);bb[i]=(float)(b[i]*contribution);
        }
        var radius=Math.Clamp((int)Math.Round(config.Radius),1,64);BoxBlur(br,width,height,radius,2);BoxBlur(bg,width,height,radius,2);BoxBlur(bb,width,height,radius,2);
        float[]? wr=null,wg=null,wb=null;
        if(config.WideIntensity>0&&config.WideRadius>config.Radius){wr=(float[])br.Clone();wg=(float[])bg.Clone();wb=(float[])bb.Clone();var wide=Math.Clamp((int)Math.Round(config.WideRadius),radius+1,96);BoxBlur(wr,width,height,wide,2);BoxBlur(wg,width,height,wide,2);BoxBlur(wb,width,height,wide,2);}
        var warmth=Math.Clamp(config.Warmth,-.5,.5);
        for(var i=0;i<n;i++)
        {
            var lr=br[i]*config.Intensity+(wr is null?0:wr[i]*config.WideIntensity);var lg=bg[i]*config.Intensity+(wg is null?0:wg[i]*config.WideIntensity);var lb=bb[i]*config.Intensity+(wb is null?0:wb[i]*config.WideIntensity);
            r[i]+=(float)(lr*(1+.55*warmth));g[i]+=(float)(lg*(1+.12*warmth));b[i]+=(float)(lb*(1-.35*warmth));
        }
    }

    private static HdrFrame Downsample(HdrFrame source,int width,int height)
    {
        var result=new HdrFrame(width,height);var sx=source.Width/(double)width;var sy=source.Height/(double)height;
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var x0=(int)Math.Floor(x*sx);var x1=Math.Max(x0+1,(int)Math.Ceiling((x+1)*sx));var y0=(int)Math.Floor(y*sy);var y1=Math.Max(y0+1,(int)Math.Ceiling((y+1)*sy));
            double rr=0,gg=0,bb=0,n=0;for(var yy=y0;yy<Math.Min(y1,source.Height);yy++)for(var xx=x0;xx<Math.Min(x1,source.Width);xx++){var c=source[xx,yy];rr+=c.R;gg+=c.G;bb+=c.B;n++;}
            result[x,y]=new HdrColor((float)(rr/n),(float)(gg/n),(float)(bb/n));
        }return result;
    }

    private static SKBitmap Sharpen(SKBitmap source,double strength,double localContrast)
    {
        var output=new SKBitmap(source.Info);
        for(var y=0;y<source.Height;y++)for(var x=0;x<source.Width;x++)
        {
            var c=source.GetPixel(x,y);double ar=0,ag=0,ab=0,n=0;
            for(var yy=Math.Max(0,y-1);yy<=Math.Min(source.Height-1,y+1);yy++)for(var xx=Math.Max(0,x-1);xx<=Math.Min(source.Width-1,x+1);xx++){var p=source.GetPixel(xx,yy);ar+=p.Red;ag+=p.Green;ab+=p.Blue;n++;}
            ar/=n;ag/=n;ab/=n;var lum=.2126*c.Red+.7152*c.Green+.0722*c.Blue;var avg=.2126*ar+.7152*ag+.0722*ab;var local=(lum-avg)*localContrast;
            output.SetPixel(x,y,new SKColor(ToByte(c.Red+(c.Red-ar)*strength+local),ToByte(c.Green+(c.Green-ag)*strength+local),ToByte(c.Blue+(c.Blue-ab)*strength+local),255));
        }return output;
    }

    private static void BoxBlur(float[] values,int width,int height,int radius,int passes){var temp=new float[values.Length];for(var p=0;p<passes;p++){BlurHorizontal(values,temp,width,height,radius);BlurVertical(temp,values,width,height,radius);}}
    private static void BlurHorizontal(float[] s,float[] t,int w,int h,int r){for(var y=0;y<h;y++){var row=y*w;double sum=0;for(var x=-r;x<=r;x++)sum+=s[row+Math.Clamp(x,0,w-1)];var d=r*2+1;for(var x=0;x<w;x++){t[row+x]=(float)(sum/d);sum-=s[row+Math.Clamp(x-r,0,w-1)];sum+=s[row+Math.Clamp(x+r+1,0,w-1)];}}}
    private static void BlurVertical(float[] s,float[] t,int w,int h,int r){var d=r*2+1;for(var x=0;x<w;x++){double sum=0;for(var y=-r;y<=r;y++)sum+=s[Math.Clamp(y,0,h-1)*w+x];for(var y=0;y<h;y++){t[y*w+x]=(float)(sum/d);sum-=s[Math.Clamp(y-r,0,h-1)*w+x];sum+=s[Math.Clamp(y+r+1,0,h-1)*w+x];}}}
    private static double Reinhard(double x)=>x/(1+x);
    private static double AcesFitted(double x){const double a=2.51,b=.03,c=2.43,d=.59,e=.14;return Math.Clamp((x*(a*x+b))/(x*(c*x+d)+e),0,1);}
    private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
    private static byte ToByte(double v)=>(byte)Math.Clamp(Math.Round(v),0,255);
}
