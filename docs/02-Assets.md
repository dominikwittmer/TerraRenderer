# Assets

v0.9 enthält kompakte wolkenfreie Tag- und Nachttexturen, damit das Projekt sofort startet.

Die Architektur ist bereits auf zusätzliche Karten vorbereitet. Für v0.95 werden die derzeit aus der Farbtextur abgeleiteten Werte durch feste Daten ersetzt:

- globales Höhenmodell
- Land-/Wassermaske
- Schnee-/Eismaske

Die öffentliche API des Renderers muss dafür nicht verändert werden; nur `EarthMaterialAtlas` erhält zusätzliche Quellen.
