using TerraRenderer.Assets;
using TerraRenderer.Core.Configuration;
using TerraRenderer.Rendering.Hdr;

namespace TerraRenderer.Rendering.ToneMapping;

internal static class EarthToneMapper
{
    public static HdrColor ApplySurfaceGrade(HdrColor color, EarthSurfaceMaterial material, ToneMappingConfiguration config)
    {
        var r = (double)color.R;
        var g = (double)color.G;
        var b = (double)color.B;
        var luminance = Luminance(r,g,b);
        var shadowLift = config.ShadowLift * Math.Pow(1.0 - Math.Clamp(luminance,0,1),2.2);
        r += shadowLift * .78; g += shadowLift * .90; b += shadowLift * 1.12;
        luminance = Luminance(r,g,b);
        r = luminance + (r-luminance)*config.Saturation;
        g = luminance + (g-luminance)*config.Saturation;
        b = luminance + (b-luminance)*config.Saturation;

        if(material.Water > .20)
        {
            var water=material.Water; var boost=config.OceanBlueBoost*water; var dark=config.OceanDarkening*water;
            r*=1-dark-.52*boost; g*=1-.26*dark-.10*boost; b*=1-.05*dark+.82*boost;
        }
        else
        {
            var a=material.Albedo;
            var green=Math.Max(0,(a.Green-(a.Red+a.Blue)*.5)/255.0);
            var desert=Math.Max(0,(a.Red-a.Blue)/255.0)*Math.Max(0,(a.Green-a.Blue)/255.0);
            var vegetation=config.VegetationBoost*green;
            r*=1-.20*vegetation; g*=1+vegetation; b*=1-.28*vegetation;
            var warm=config.DesertWarmth*desert;
            r*=1+warm; g*=1+.34*warm; b*=1-.50*warm;
        }

        if(material.Ice>.05)
        {
            var ice=material.Ice; var highlight=Math.Max(r,Math.Max(g,b));
            var compression=1.0/(1.0+config.IceHighlightCompression*ice*Math.Max(0,highlight-.40)*4.2);
            var suppression=1.0-config.PolarSuppression*ice;
            r*=compression*suppression*.985; g*=compression*suppression;
            b=b*compression*suppression+config.IceBlueShadow*ice*(1-luminance);
        }
        return new HdrColor((float)Math.Max(0,r),(float)Math.Max(0,g),(float)Math.Max(0,b));
    }

    private static double Luminance(double r,double g,double b)=>.2126*r+.7152*g+.0722*b;
}
