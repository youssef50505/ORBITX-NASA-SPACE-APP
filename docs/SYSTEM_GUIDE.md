# MARS SENTINEL — Master System Guide & Single Source of Truth

**Project:** MARS SENTINEL  
**Event:** NASA Space Apps Challenge 2026 — *Interplanetary Survival Guide: Martian Map*  
**Location:** Jezero Crater, Mars (`18.42875° N, 77.44215° E`)  
**Status:** Production (Consolidated)

---

## 1. System Overview
MARS SENTINEL is a Martian Extravehicular Activity (EVA) tactical planning and decision-support platform. It unites verified NASA/USGS orbital elevation data, real-time 3D visualization, interactive A* traverse optimization, and closed-loop life-support agriculture telemetry across a unified Blender 5.2 production environment and a lightweight client-side Three.js web application.

---

## 2. Canonical Architecture & File Map

### 2.1 Active Production Files
- **3D Production Scene**: `MARS_SENTINEL_HERO_PRODUCTION.blend` (288 MB / 552 objects / 0 loose objects).
- **Web Application**: `web/index.html`, `web/styles.css`, `web/app.js` (Pure client-side Three.js digital twin; zero external backend dependencies).
- **Startup & Hotkey Controller**: `tools/mars_front_view.py` (Unified add-on providing Hotkeys `1`–`5` and the N-Panel dashboard).
- **Blender MCP Integration**: `tools/blender_mcp_server.py` (Antigravity bridge listening on `127.0.0.1:9876`).

### 2.2 Protected Scientific Baselines (NEVER Delete or Modify)
- `archive/Mars_backup_before_quality_pass.blend` (Untouched source backup).
- `data/JEZ_hirise_soc_006_DTM_1m.tif` (1.84 GB USGS HiRISE DTM GeoTIFF, DOI `10.5066/P9REJ9JN`).
- `data/JEZ_ctx_B_soc_008_DTM_20m.tif` (USGS Mars 2020 CTX DTM GeoTIFF, DOI `10.5066/P906QQT8`).
- `NASA_HiRISE_DTM_Jezero_FieldSite_4m` (1,132,096 vertices, SHA-256 `ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd`).

---

## 3. Scientific Data Provenance & Key Coordinates
- **Area of Interest (AOI)**: 4,096 m × 4,096 m window centered on Jezero Crater.
- **Octavia E. Butler Landing Site**: `18.4447° N, 77.4508° E`
- **Wright Brothers Field (Ingenuity)**: `18.44486° N, 77.45102° E`
- **Surface Elevation Baseline**: `Mars_Terrain` underlay flat at `z = -54.84 m`.
- **Surface Color Calibration**: HiRISE red-filter brightness (`JEZ_hirise_soc_006_orthoMosaic_25cm`, DOI `10.5066/P9QJDP48`) blended with Viking MDIM 2.1 bright-terrain calibration and localized true RGB from HiRISE observation `ESP_045994_1985`.

---

## 4. Evaluated EVA Traverses (A* Optimized)
All three routes start at Base Camp (`(-0.45, 13.8)`) and traverse toward Octavia E. Butler Landing Site:
1. **Shortest Route (`MS_Route_Shortest_HiRISE`)**:
   - Distance: `1,113.50 m` | Elevation change: `+22.4 m` | Max slope: `24.1°`
   - Optimization: Geometric directness; traverses moderate slope.
2. **Safest Route (`MS_Route_Safest_HiRISE`)**:
   - Distance: `1,159.95 m` | Elevation change: `+14.2 m` | Max slope: `13.8°`
   - Optimization: Mandatory 48 m lateral safety buffer avoiding any cell with slope $\ge 15^\circ$.
3. **Science Route (`MS_Route_Science_HiRISE`)**:
   - Distance: `1,203.21 m` | Elevation change: `+18.7 m` | Max slope: `14.5°`
   - Optimization: Traverses via the *Rochette* rock sample waypoint for scientific sampling.

---

## 5. Blender 5.2 Hotkeys & Production Scene Controls
Installed via `tools/mars_front_view.py`:
- `Key 1`: Surface Base Camp, Astronauts, Perseverance Rover, Exploration Vehicle, Habitat Pod, and Agriculture Zone.
- `Key 2`: Orbital Mars Globe with Jezero Pin, Reconnaissance Satellite, and Procedural Milky Way Starfield.
- `Key 3`: Mars Agriculture, Hydroponics & Geodesic Aeroponics Sustainability Zone.
- `Key 4`: Habitat Base Pod & Rover Fleet Close-up.
- `Key 5`: Live Martian Day/Night Cycle (`Day -> Sunset -> Twilight -> Night -> Sunrise`).
- `Key 6`: Mars Exploration Fleet Showcase (`MS_Mars_Fleet_Surface` — Perseverance, Ingenuity, Curiosity, Spirit, Opportunity, Viking, InSight).

---

## 6. Base Camp & Agriculture Asset Map

| Collection / Asset | Coordinates `(X, Y)` | Role |
|---|---|---|
| `MS_Agriculture_Zone` (513 objs) | `(-29.5 .. -14.2, 16.0 .. 28.5)` | Vaulted Hydroponics Greenhouse (`MS_GH1`), Aeroponics Dome (`MS_GH2`), O₂/Water recycling tanks, perchlorate remediation unit. |
| `Sketchfab_model.012` | `(2.0, 24.0)` | Spherical 4-Legged Habitat Base Pod. |
| Perseverance Rover (39 parts) | `(8.68, 9.73)` | Rover body, wheels, Mastcam-Z, robotic arm, and RTG grounded flush. |
| `Sketchfab_model.002` | `(15.5, 21.0)` | Heavy Mars Crew Exploration Vehicle. |
| `Sketchfab_model.014` | `(-11.5, 11.5)` | Reconnaissance Quadcopter Drone. |
| `Sketchfab_model.005` | `(21.5, 8.5)` | NASA InSight Stationary Lander (Base Camp Telemetry Relay). |
| Astronaut Crew (4 astronauts) | `(-11.4 .. +4.3, 3.7 .. 12.4)` | `Joe_Astronaut`, `Sketchfab_model` (walking Mixamo cycle), `Sketchfab_model.015`, and `ludovisions_FC1_rig`. |

---

## 7. Interactive Web Platform (`web/`)
- Pure client-side Three.js implementation.
- Load via any local web server or directly open `web/index.html`.
- Hotkeys `1`–`6` mirrored in the browser viewport.
- Dynamic Day/Night solar cycle slider matching Blender Mie scattering and twilight starfield transition.
- Deterministic Safety Score calculation and EVA oxygen/energy expenditure cards.

---

## 8. Target Game Engine (`Unity 6 — 6000.6.9f1` & `Unity CLI 1.0.0`)
- **Primary Game Engine**: **Unity Engine** (`Unity 6000.6.9f1` installed locally, managed via `Unity CLI 1.0.0`).
- **Asset & Scene Pipeline**:
  1. **Authoring in Blender 5.2**: `MARS_SENTINEL_HERO_PRODUCTION.blend` serves as the 3D source of truth for Jezero Crater terrain, Base Camp, Agriculture Zone, and the NASA Mars Exploration Fleet (`MS_Mars_Fleet_Surface` and `MS_Mars_Orbital_Fleet`).
  2. **Export to Unity**: Export terrain, habitats, and rover/orbiter hierarchies as `.fbx` / `.glb` (1 unit = 1 meter, packed PBR textures) into the Unity project Assets folder.
  3. **NASA Datasets Integration**: Feed `data/routes_traverses/perseverance_traverse_m20.geojson`, `data/routes_traverses/curiosity_traverse_msl.geojson`, `data/routes_traverses/ingenuity_flights_m20.geojson`, `data/geology/usgs_sim3292_units.json`, `data/elevation_dtm/`, and `data/imagery/` directly into Unity C# runtime scripts and Terrain/Shader Graph materials.
  4. **Target Builds**: Unity WebGL (for browser evaluation), Windows Standalone (high-fidelity URP/HDRP), and OpenXR.


---

## 9. Playable Unity 6 Campus, NASA Science Integration & Web Parity

- **Exploration (`MarsAstronautController.cs`, `MarsRoverVehicleController.cs`, `MarsInteractionSystem.cs`)**: Playable EVA Astronaut + 6x6 Pressurized Rover (`[F]` Explore/Showcase, `WASD`/Arrows move/drive, `Shift` sprint, `Space` jump/brake, `[E]` inspect/board rover, `[V]` visor, `[B]` lamp, `[Tab]` cursor, `[K]` mute). Raycast scan covers 50 NASA targets loading Mastcam-Z imagery (`unity_project/Assets/StreamingAssets/NASA_Data/imagery/`), `jezero_outcrops_and_rocks.json`, `mars_environmental_science.json`, and `curiosity_mount_sharp_waypoints.json`.
- **Facilities & NASA GLBs (`MarsBaseFacilitiesBuilder.cs`, `unity_project/Assets/Scripts/NASA/Editor/MarsBaseCampusBuilder.cs`)**: Walkable Hydroponics Dome, First Aid/Medical Bay, EDL Lifting-Body Craft, Mars Ascent Pad, `Habitat Demonstration Unit`, `Mark III Spacesuit`, `Saturn V`, `Ares 1`, and `Mobile Launcher`, with procedural spatial/UI audio (`MarsAudioManager.cs`).
- **Blender & Web Sync**: `scripts/cleanup_blender_unused_assets.py` hid 401 unused/judge-flagged character & prop nodes (276 packed textures, 513 agri objects intact). `web/index.html` mirrors facilities, clickable outcrops, MEDA/RAD telemetry, WASD pan, WebAudio, and `14 CFR 1221` public-data credit.
