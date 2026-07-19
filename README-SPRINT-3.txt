TerraRenderer 2.1 - Sprint 3: Physically Based Lighting
Basis: Commit 1476220

Kernänderungen
- Echter linearer HDR-Framebuffer (float RGB) bis zum finalen Export.
- Keine 8-Bit-Begrenzung mehr zwischen Surface, Relief, Wolken, Atmosphäre und Nachtlichtern.
- Nachttextur wird als Radiance interpretiert; helle Stadtzentren behalten einen deutlich größeren Dynamikumfang.
- Bloom extrahiert ausschließlich HDR-Werte vor ACES und Display-Konvertierung.
- Sonnenabhängige HDR-Atmosphäre mit stärkerem Rayleigh-/Mie-Kontrast am Terminator.
- Material-Grading arbeitet nun ebenfalls im linearen HDR-Raum.
- Supersampling wird vor Bloom und Tone Mapping im HDR-Raum reduziert.

Neue Dateien
- src/TerraRenderer.Rendering/Hdr/HdrColor.cs
- src/TerraRenderer.Rendering/Hdr/HdrFrame.cs

Ersetzte Dateien
- EarthRenderer.cs
- LightingResult.cs
- LightingPipeline.cs
- AdaptiveReliefLightingStage.cs
- SurfaceLighting.cs
- EarthAtmosphere.cs
- ProceduralCloudLayer.cs
- EarthToneMapper.cs
- EarthPostProcessor.cs
- config/terrarenderer.cinematic.json
- config/terrarenderer.watchface.json

Test
  dotnet build
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.cinematic.json

Aussagekräftige Frames: 00:00, 06:00, 12:00 und 18:00 UTC.
