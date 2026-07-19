TerraRenderer Visual Upgrades A-C
Basis: 3428251 (Visual Upgrade 1)

Enthalten:
A Atmosphere
- staerkerer, schmaler Earth Limb
- Rayleigh/Mie-aehnlicher Horizontsaum
- waermeres Terminator- und Sonnenuntergangsgluehen
- sehr schwacher Nacht-Limb

B Terrain and Ocean
- Macro-Relief fuer Gebirgssysteme bei Watchface-Aufloesung
- zusaetzlicher Felskontrast
- Tiefseeabdunklung, Schelf-Tint und dezente Randabdunklung

C Night
- kontrolliertere Stadtlicht-Kerne
- weicherer Bloom
- sehr schwache blaue Nachtatmosphaere

Anwendung:
ZIP im Repository-Root entpacken und Dateien ueberschreiben.
Danach:
  dotnet build
  dotnet run --project src/TerraRenderer.Cli -- --config config/terrarenderer.generated.json

Hinweis:
Der Code wurde strukturell gegen den bereitgestellten Stand aufgebaut. In der Erstellungsumgebung
war kein .NET SDK vorhanden; der abschliessende Build muss deshalb lokal erfolgen.
