TerraRenderer 2.2 – Sprint 4: Night Light Engine

Basis: eingecheckter Sprint-3-Experimentstand.

Dieser Sprint ersetzt gezielt die Nachtlichtberechnung und korrigiert den
magenta Atmosphaerenrand des Experiments.

Neue/ersetzte Dateien:
- src/TerraRenderer.Rendering/Lighting/NightLightEngine.cs
- src/TerraRenderer.Rendering/Lighting/SurfaceLighting.cs
- src/TerraRenderer.Rendering/Atmosphere/EarthAtmosphere.cs
- config/terrarenderer.cinematic.json
- config/terrarenderer.watchface.json

Akzeptanzkriterien:
- keine roten, gruenen oder grossflaechig weissen Nachtmasken
- helle Stadtkerne sind nahezu weiss
- kleinere Siedlungen bleiben deutlich dunkler
- Bloom entsteht nur bei den hellsten Zentren
- kein magenta Ring um die Erde
- Nachtseite bleibt dunkel und neutral
