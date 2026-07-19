TerraRenderer – Watchface & Cinematic Upgrade
Basis: Commit 3428251 plus Visual Upgrades A-C

Enthalten:
- prozedurale, zeitabhängige Wolkenschicht
- Wolkenschatten und Silver Lining
- stärkeres Sonnenlicht und kräftigerer Ozean-Glanz
- sonnenabhängiges Atmosphärenglühen
- Supersampling und Schärfung für das Watchface
- separate Profile für 466x466 und 1200x1200

Dateien in das Repository kopieren und vorhandene Dateien ersetzen.

Build:
  dotnet build

Watchface-Test:
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.watchface.json

Cinematic-Test:
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.cinematic.json

Alle Frames:
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.watchface.json --all

Wolken deaktivieren:
  "enableClouds": false

Hinweis:
Die Wolken sind deterministisch und bewegen sich mit der UTC-Zeit. Sie basieren noch nicht
auf realen Wetterdaten. Das ist absichtlich die erste visuelle Wolkenstufe ohne zusätzliche Assets.
