# ORBITX: Mars Sentinel — Unreal Engine 5.8 Module (`MarsSentinelUE`)

A complete **Unreal Engine 5.8** Lumen + Nanite planetary survival & exploration module synchronized with our NASA Jezero Crater datasets (`18.4447°N, 77.4508°E`), shared `nav-module` CTX A\* routes, and the 5-Chapter **"Sol 1042: Echoes of Jezero"** campaign.

## Highlights
- **Unreal Engine 5.8 Rendering:** DirectX 12 SM6, **Lumen Global Illumination & Reflections**, **Nanite Virtualized Geometry**, **Virtual Shadow Maps**, **Volumetric Martian Dust Fog**, and physically calibrated CO₂/dust `SkyAtmosphere`.
- **Authentic Martian Physics:** World gravity set to `-372.076 cm/s²` (`3.72076 m/s²`, `0.379g`) in `Config/DefaultEngine.ini`.
- **Authentic NASA 3D Assets:** Automated UE 5.8 Interchange pipeline imports `Perseverance`, `Ingenuity`, `NASA HDU Habitat`, `AgriDome_Hero`, `MedBay_Hero`, `EDL_Lander_Hero`, `LaunchPad_Hero`, `Astronaut`, `Curiosity`, `InSight`, and `Viking Lander` onto the 800 m NASA HiRISE-colored Jezero Crater terrain (`/Game/Maps/JezeroCrater_Hero.umap`).
- **C++ Gameplay & Story Source (`Source/MarsSentinelUE/`):**
  - `MarsAstronautCharacter.h/.cpp` — 0.38g EVA locomotion, survival O₂/Power/Radiation/Beer-Lambert dust storm ticks, RIMFAX `[X]` scanner trigger, and bilingual EN/AR 5-Chapter Story dialogue.
  - `MarsCtxNavSubsystem.h/.cpp` — Loads shared `nav-module/routes.json` A\* Shortest vs. Safest waypoints into Unreal centimeter space.

## Quick Start (Unreal Engine 5.8)
1. **Open directly:** Double-click `unreal_project/MarsSentinelUE.uproject` in Unreal Engine 5.8 (opens `/Game/Maps/JezeroCrater_Hero` automatically).
2. **Rebuild assets/level from scratch anytime:**
   ```powershell
   & "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --python unreal_project\Scripts\build_jezero_ue5_terrain_glb.py
   & "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe" "$PWD\unreal_project\MarsSentinelUE.uproject" -ExecutePythonScript="$PWD\unreal_project\Scripts\build_mars_sentinel_ue5.py" -unattended -nop4 -nosplash
   ```
