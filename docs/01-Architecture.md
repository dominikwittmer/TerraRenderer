# Architektur

TerraRenderer v0.9 trennt vier Verantwortungsbereiche:

- `TerraRenderer.Core`: Astronomie, Projektion, Geometrie und Konfigurationsmodelle.
- `TerraRenderer.Assets`: Laden und Abtasten der Erdtexturen sowie Ableitung eines Materialdatensatzes.
- `TerraRenderer.Rendering`: Beleuchtung, Atmosphäre, Reliefnormalen, Tonemapping und PNG-Ausgabe.
- `TerraRenderer.Cli`: Konfiguration, Layoutauswahl und Batch-Export.

Die Renderfolge lautet:

1. Orthografische Projektion
2. Materialabtastung
3. Relief-Normale
4. Sonnen- und Nachtbeleuchtung
5. Ozeanreflexion
6. Atmosphäre
7. Tonemapping und Nordpol-Highlight-Kompression
8. PNG-Export
