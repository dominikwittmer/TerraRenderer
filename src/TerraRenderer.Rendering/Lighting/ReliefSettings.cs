using System.Xml.Linq;

namespace TerraRenderer.Rendering.Lighting;

/// <summary>
/// Controls the optional adaptive relief-lighting pass.
/// The initial sprint keeps the pass disabled so existing renders remain unchanged.
/// </summary>
internal sealed record ReliefSettings
{
    public static ReliefSettings Disabled { get; } = new()
    {
        Enabled = false
    };

    public bool Enabled { get; init; }

    public double RidgeStrength { get; init; } = 0.30;

    public double ValleyStrength { get; init; } = 0.18;

    public double SkyLight { get; init; } = 0.15;

    public double AmbientBounce { get; init; } = 0.05;

    public double AdaptiveStrength { get; init; } = 1.0;

    public void Validate()
    {
        ValidateNonNegative(RidgeStrength, nameof(RidgeStrength));
        ValidateNonNegative(ValleyStrength, nameof(ValleyStrength));
        ValidateNonNegative(SkyLight, nameof(SkyLight));
        ValidateNonNegative(AmbientBounce, nameof(AmbientBounce));
        ValidateNonNegative(AdaptiveStrength, nameof(AdaptiveStrength));
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value< 0.0)
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and non-negative.");
    }
}
