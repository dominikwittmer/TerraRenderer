namespace TerraRenderer.Core.Configuration;

public sealed class TerraRendererConfiguration
{
    public string DateUtc { get; set; } = "2026-07-18";
    public int FrameIntervalMinutes { get; set; } = 10;
    public string DefaultLayout { get; set; } = "both";
    public AssetConfiguration Assets { get; set; } = new();
    public Dictionary<string, LayoutConfiguration> Layouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RenderingConfiguration Rendering { get; set; } = new();
}

public sealed class AssetConfiguration
{
    public string DayTexture { get; set; } = "assets/textures/earth_day.jpg";
    public string NightTexture { get; set; } = "assets/textures/earth_night.jpg";
    public string? ElevationTexture { get; set; }
    public string? WaterMaskTexture { get; set; }
    public string? IceMaskTexture { get; set; }
    public string? NormalTexture { get; set; }
    public string? MaterialTexture { get; set; }
    public string? AmbientOcclusionTexture { get; set; }
    public string? NightBloomTexture { get; set; }
}

public sealed class LayoutConfiguration
{
    public int Width { get; set; } = 466;
    public int Height { get; set; } = 466;

    // The geographic point facing the camera. This is the visual composition target,
    // not necessarily the user's physical location.
    public double CenterLatitude { get; set; } = 37.0;
    public double CenterLongitude { get; set; } = 15.0;

    public string Projection { get; set; } = "perspective";
    public double CameraDistance { get; set; } = 6.0;
    public double FieldOfViewDegrees { get; set; } = 20.0;
    public double CameraRollDegrees { get; set; }

    // Retained for backwards-compatible orthographic layouts.
    public double GlobeFill { get; set; } = 0.94;
    public double GlobeOffsetX { get; set; }
    public double GlobeOffsetY { get; set; }
}

public sealed class RenderingConfiguration
{
    public bool EnableRelief { get; set; } = true;
    public bool EnableNightLights { get; set; } = true;
    public bool EnableAtmosphere { get; set; } = true;
    public bool EnableOceanSpecular { get; set; } = true;
    public bool EnableToneMapping { get; set; } = true;
    public bool EnableClouds { get; set; } = true;
    public double SunLightStrength { get; set; } = 1.18;
    public double TwilightSoftness { get; set; } = 0.38;
    public double TwilightBandWidth { get; set; } = 0.34;
    public double TwilightSurfaceLift { get; set; } = 0.18;
    public double DiffusePower { get; set; } = 0.78;
    public double AmbientLight { get; set; } = 0.095;
    public double HemisphereLight { get; set; } = 0.055;
    public double ReliefStrength { get; set; } = 1.25;
    public double ReliefSampleDegrees { get; set; } = 0.085;
    public double NormalMapStrength { get; set; } = 0.72;
    public double AmbientOcclusionStrength { get; set; } = 0.20;
    public double NightLightStrength { get; set; } = 0.62;
    public double NightLightGlow { get; set; } = 0.42;
    public double NightLightBlurDegrees { get; set; } = 0.42;
    public double NightLightFadeWidth { get; set; } = 0.18;
    public double NightLightFadePower { get; set; } = 1.35;
    public double OceanSpecularStrength { get; set; } = 0.24;
    public double OceanSpecularPower { get; set; } = 82.0;
    public double OceanFresnelStrength { get; set; } = 0.20;
    public double OceanDepthStrength { get; set; } = 0.16;
    public double OceanShelfTint { get; set; } = 0.08;
    public double OceanLimbDarkening { get; set; } = 0.10;
    public double NightCoreStrength { get; set; } = 1.0;
    public double NightBloomSoftness { get; set; } = 0.55;
    public double NightAtmosphereStrength { get; set; } = 0.035;
    public double NightWhiteCore { get; set; } = 0.82;
    public double NightWarmth { get; set; } = 0.34;
    public double NightHaloStrength { get; set; } = 0.72;
    public double NightCompression { get; set; } = 0.64;

    // Sprint 5 - daylight recovery. These controls are applied only to reflected
    // daylight, before night emission is accumulated.
    public double DaylightExposure { get; set; } = 1.18;
    public double DaylightRedBalance { get; set; } = 1.035;
    public double DaylightGreenBalance { get; set; } = 1.01;
    public double DaylightBlueBalance { get; set; } = 0.94;
    public double DaylightLandLift { get; set; } = 0.012;
    public double DaylightOceanNeutralization { get; set; } = 0.30;
    public AdaptiveReliefConfiguration AdaptiveRelief { get; set; } = new();
    public CloudConfiguration Clouds { get; set; } = new();
    public PostProcessingConfiguration PostProcessing { get; set; } = new();
    public AtmosphereConfiguration Atmosphere { get; set; } = new();
    public ToneMappingConfiguration ToneMapping { get; set; } = new();
    public BloomConfiguration Bloom { get; set; } = new();
}

public sealed class AdaptiveReliefConfiguration
{
    public bool Enabled { get; set; } = true;

    // Brightens sun-facing micro-relief. Kept deliberately subtle for a 466 px watchface.
    public double RidgeStrength { get; set; } = 0.18;

    // Deepens lee slopes and terrain with low ambient openness.
    public double ValleyStrength { get; set; } = 0.12;

    // Adds cool diffuse light to relief that is turned away from the sun.
    public double SkyLight { get; set; } = 0.035;

    // Small neutral lift that prevents mountain shadows from becoming blocked.
    public double AmbientBounce { get; set; } = 0.012;

    // Global multiplier for the complete adaptive pass.
    public double Strength { get; set; } = 1.0;

    // Uses elevation, AO and slope together to keep large mountain systems readable
    // at watchface scale without amplifying flat regions.
    public double MacroReliefStrength { get; set; } = 0.10;
    public double RockContrast { get; set; } = 0.12;
}


public sealed class AtmosphereConfiguration
{
    public double Thickness { get; set; } = 0.030;
    public double RayleighStrength { get; set; } = 0.70;
    public double MieStrength { get; set; } = 0.16;
    public double NightSideStrength { get; set; } = 0.012;
    public double TerminatorBoost { get; set; } = 0.72;
    public double SurfaceHazeStrength { get; set; } = 0.34;
    public double SunsetWarmth { get; set; } = 0.12;
    public double RadialFalloff { get; set; } = 4.4;
    public double TerminatorWidth { get; set; } = 0.34;
    public double LimbStrength { get; set; } = 0.24;
    public double NightLimbStrength { get; set; } = 0.020;
    public double HorizonGlowStrength { get; set; } = 0.30;
    public double SunsetGlowStrength { get; set; } = 0.16;
    public double ForwardScatterStrength { get; set; } = 0.36;
    public double ForwardScatterPower { get; set; } = 8.0;
    public double TwilightPurpleStrength { get; set; } = 0.10;
    public double GoldenHourStrength { get; set; } = 0.34;
}


public sealed class ToneMappingConfiguration
{
    public double Exposure { get; set; } = 1.04;
    public double Contrast { get; set; } = 1.06;
    public double Saturation { get; set; } = 1.055;
    public double IceHighlightCompression { get; set; } = 0.34;
    public double IceBlueShadow { get; set; } = 0.115;
    public double OceanBlueBoost { get; set; } = 0.085;
    public double OceanDarkening { get; set; } = 0.075;
    public double VegetationBoost { get; set; } = 0.055;
    public double DesertWarmth { get; set; } = 0.045;
    public double PolarSuppression { get; set; } = 0.16;
    public double ShadowLift { get; set; } = 0.018;
    public double HighlightShoulder { get; set; } = 0.20;
    public double BlackLevel { get; set; } = 0.006;
    public double WhitePoint { get; set; } = 1.0;
    public double AcESStrength { get; set; } = 1.0;
    public double DisplayGamma { get; set; } = 2.2;
}

public sealed class BloomConfiguration
{
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 0.72;
    public double Knee { get; set; } = 0.28;
    public double Intensity { get; set; } = 0.32;
    public double Radius { get; set; } = 7.0;
    public double WideRadius { get; set; } = 20.0;
    public double WideIntensity { get; set; } = 0.11;
    public double NightBoost { get; set; } = 0.65;
    public double Warmth { get; set; } = 0.12;
}

public sealed class CloudConfiguration
{
    public double Coverage { get; set; } = 0.42;
    public double Density { get; set; } = 0.58;
    public double Scale { get; set; } = 2.8;
    public double DetailScale { get; set; } = 8.5;
    public double SpeedDegreesPerHour { get; set; } = 0.45;
    public double DayBrightness { get; set; } = 0.72;
    public double SilverLining { get; set; } = 0.34;
    public double ShadowStrength { get; set; } = 0.16;
    public double ShadowOffsetDegrees { get; set; } = 0.42;
    public double ForwardScattering { get; set; } = 0.22;
    public double NightVisibility { get; set; } = 0.055;
}

public sealed class PostProcessingConfiguration
{
    public int Supersampling { get; set; } = 1;
    public double SharpenStrength { get; set; } = 0.34;
    public double LocalContrast { get; set; } = 0.08;
    public double DaylightStructure { get; set; } = 0.14;
    public int DaylightStructureRadius { get; set; } = 5;
    public double FineDetailStrength { get; set; } = 0.28;
    public int FineDetailRadius { get; set; } = 2;
    public double EdgeProtection { get; set; } = 0.62;
}
