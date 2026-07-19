TerraRenderer - Visual Upgrade 1
Basis: Commit 0575445 (Adaptive Relief)

Enthaltene Ersatzdateien:
- src/TerraRenderer.Core/Configuration/TerraRendererConfiguration.cs
- src/TerraRenderer.Rendering/Lighting/Stages/AdaptiveReliefLightingStage.cs
- src/TerraRenderer.Rendering/EarthRenderer.cs
- config/terrarenderer.json

Anwendung:
1. ZIP im Root-Verzeichnis des TerraRenderer-Repositories entpacken.
2. Bestehende Dateien überschreiben.
3. dotnet build
4. Normal rendern.

Neue Konfiguration unter rendering.adaptiveRelief:
- enabled
- ridgeStrength
- valleyStrength
- skyLight
- ambientBounce
- strength

Für einen A/B-Vergleich einfach enabled auf false setzen.
