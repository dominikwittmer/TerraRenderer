namespace TerraRenderer.Rendering.Lighting.Stages;

/// <summary>
/// Reserved lighting stage for ridge, valley, skylight and ambient-bounce shaping.
/// In this infrastructure sprint the stage intentionally leaves the result unchanged.
/// </summary>
internal sealed class AdaptiveReliefLightingStage : ILightingStage
{
    private readonly ReliefSettings _settings;

    public AdaptiveReliefLightingStage(ReliefSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
    }

    public void Apply(in LightingContext context, LightingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!_settings.Enabled)
            return;

        // Deliberately an identity pass for Sprint 10, part 1.
        // The next patch will add the adaptive ridge/valley contribution here.
    }
}
