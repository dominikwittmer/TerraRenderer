TerraRenderer 2.0 – Sprint 1: HDR & ACES
Basis: Commit 38a17e8

Enthalten:
- frameweites lineares Post-Processing
- ACES-Filmic Highlight-Rolloff
- zweistufiges Bloom fuer Sonne, Atmosphaere und Stadtlichter
- staerkeres Night-Bloom mit tiefem OLED-Schwarz
- getrennte Cinematic- und Watchface-Abstimmung
- Material-Farbgrading bleibt erhalten, Tonemapping erfolgt erst nach dem kompletten Frame

Dateien in das Repository kopieren und vorhandene Dateien ersetzen.

Build:
  dotnet build

Cinematic:
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.cinematic.json

Watchface:
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.watchface.json

Wichtige Regler:
  rendering.toneMapping.exposure
  rendering.toneMapping.blackLevel
  rendering.toneMapping.acESStrength
  rendering.bloom.threshold
  rendering.bloom.intensity
  rendering.bloom.nightBoost
