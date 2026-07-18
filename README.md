# TerraRenderer v1.10

Earth renderer and offline asset pipeline for a 466×466 Huawei-style Earth watchface.

## Build generated assets

Set `sources.elevationGeoTiff` in `config/assetbuilder.json`, then run:

```powershell
dotnet run --project .\src\TerraRenderer.AssetBuilder\TerraRenderer.AssetBuilder.csproj -- --config .\config\assetbuilder.json
```

v1.10 adds geodetically scaled normal maps, Gaussian elevation prefiltering, a slope map, stabilized curvature, horizon-based ambient occlusion and expanded diagnostics for packed material and normal channels.

The NOAA ETOPO Float32/Deflate/256×256 tiled GeoTIFF can be used directly.
