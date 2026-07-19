TerraRenderer - Sprint 5: Material-Aware Surface Shading

Visible changes
- Material-aware colour reconstruction before lighting
- Deeper, more neutral oceans with shelf-water separation
- Natural vegetation, desert and high-rock colour response
- Cooler, neutral snow and ice
- Material-dependent diffuse lighting
- Roughness-aware ocean highlights and Fresnel response
- Height-aware atmospheric haze for more depth near the limb

Changed files
- src/TerraRenderer.Rendering/Materials/SurfaceMaterialShading.cs (new)
- src/TerraRenderer.Rendering/EarthRenderer.cs
- src/TerraRenderer.Rendering/Lighting/SurfaceLighting.cs
- src/TerraRenderer.Rendering/Lighting/Stages/LegacySurfaceLightingStage.cs
- src/TerraRenderer.Rendering/Atmosphere/EarthAtmosphere.cs

Suggested commit
Sprint 5 Material-aware surface shading

Note
The supplied execution environment did not contain the .NET SDK, so the final build must be run locally with Visual Studio / dotnet build.
