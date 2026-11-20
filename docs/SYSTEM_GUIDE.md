# MARS SENTINEL — Master System Guide & Single Source of Truth

**Project:** ORBITX · MARS SENTINEL (NASA Space Apps Challenge 2026)  
**Anchor:** Jezero Crater, Mars (`18.42875° N, 77.44215° E` · Octavia E. Butler Landing Site `18.4447° N, 77.4508° E`)

## 1. System Overview
MARS SENTINEL is a multi-engine Martian EVA survival, autonomous pathfinding, and 3D geospatial platform uniting verified NASA and USGS orbital DTMs across six synchronized modules: Blender 5.2 3D Master, WebGL Three.js Digital Twin, Unity 6 Standalone Game, Unreal Engine 5.8 Lumen and Nanite Simulation, Python and JavaScript CTX A* Solver, and a 236-test automated verification suite.

## 2. Canonical Architecture & File Map
- **3D Master**: `MARS_SENTINEL_HERO_PRODUCTION.blend` and `MARS_SENTINEL_NASA_Terrain_Study_SURFACE_FOCUS.blend`
- **Web Digital Twin**: `web/index.html`, `web/styles.css`, `web/app.js`, `web/nav_sun_engine.js`, `web/nasa_data_bundle.js`
- **Game Engines**: `unity_project/` and `unreal_project/MarsSentinelUE.uproject`
- **Tools**: `tools/mars_front_view.py`, `tools/blender_mcp_server.py` (`127.0.0.1:9876`), `nav-module/nav.py`
- **Protected Baselines**: `archive/Mars_backup_before_quality_pass.blend`, `data/JEZ_hirise_soc_006_DTM_1m.tif` (DOI `10.5066/P9REJ9JN`), `data/JEZ_ctx_B_soc_008_DTM_20m.tif` (DOI `10.5066/P906QQT8`), `data/jez_hirise_site_height_4m.png`, `data/jez_hirise_site_nasa_color_1m.png`, and `NASA_HiRISE_DTM_Jezero_FieldSite_4m` (1132096 vertices, SHA-256 `ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd`)

## 3. Scientific Data Provenance & Key Coordinates
AOI covers a 4096 m by 4096 m Jezero delta window with Wright Brothers Field at `18.44486° N, 77.45102° E`. Surface calibration fuses HiRISE ortho-mosaic `JEZ_hirise_soc_006_orthoMosaic_25cm` (DOI `10.5066/P9QJDP48`) with Viking MDIM 2.1 and true RGB from HiRISE `ESP_045994_1985`.

## 4. Evaluated EVA Traverses (A* Optimized)
- **Shortest (`MS_Route_Shortest_HiRISE`)**: `1,113.50 m` distance, `+22.4 m` climb, `24.1°` max slope
- **Safest (`MS_Route_Safest_HiRISE`)**: `1,159.95 m` distance, `+14.2 m` climb, `13.8°` max slope with 48 m safety buffer
- **Science (`MS_Route_Science_HiRISE`)**: `1,203.21 m` distance, `+18.7 m` climb, `14.5°` max slope via Rochette outcrop
- **CTX 20 m Solver**: 8-connected A* over 399 by 378 CTX grid comparing 5367 m Shortest vs 6483 m Safest routes

## 5. Blender 5.2 Hotkeys & Production Scene Controls
Provided by `tools/mars_front_view.py`: `Key 1` Surface Base, `Key 2` Orbital Mars Globe and Starfield, `Key 3` Agriculture and Hydroponics Zone, `Key 4` Habitat and Fleet Close-up, `Key 5` Martian Day and Night Cycle, `Key 6` Mars Exploration Fleet Showcase.

## 6. Base Camp & Agriculture Asset Map
`MS_Agriculture_Zone` contains 513 objects including Hydroponics Greenhouse `MS_GH1`, Aeroponics Dome `MS_GH2` with 98.4 percent water recovery, Habitat Pod, 39-part Perseverance Rover, Heavy Crew Vehicle, Recon Drone, InSight Lander, and 4 EVA Astronauts.

## 7. Multi-Platform Runtime Parity
Synchronized 5-Chapter Story Sol 1042 Echoes of Jezero, Beer-Lambert dust storm attenuation, 3D Hazard Solar and RF terrain heatmaps, RIMFAX subsurface X-Ray scanner, bilingual English and Arabic RTL localization, and 14 CFR 1221 public-data compliance.
