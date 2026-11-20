# MARS SENTINEL Master System Guide and Single Source of Truth

Project ORBITX MARS SENTINEL for NASA Space Apps Challenge 2026
Anchor Jezero Crater Mars 18.42875° N 77.44215° E and Octavia E Butler Landing Site 18.4447° N 77.4508° E

## 1. System Overview
MARS SENTINEL is a multi-engine Martian EVA survival autonomous pathfinding and 3D geospatial simulation platform uniting verified NASA and USGS orbital elevation models across six synchronized modules including Blender 5.2 3D Master WebGL Three.js Digital Twin Unity 6 Standalone Game Unreal Engine 5.8 Lumen and Nanite Simulation Python and JavaScript CTX A* Solver and a comprehensive automated verification suite.

## 2. Canonical Architecture and File Map
Primary files include `MARS_SENTINEL_HERO_PRODUCTION.blend` `MARS_SENTINEL_NASA_Terrain_Study_SURFACE_FOCUS.blend` `web/index.html` `web/styles.css` `web/app.js` `web/nav_sun_engine.js` `tools/mars_front_view.py` `tools/blender_mcp_server.py` `nav-module/nav.py` and `archive/Mars_backup_before_quality_pass.blend`. Protected datasets include `data/JEZ_hirise_soc_006_DTM_1m.tif` with DOI 10.5066/P9REJ9JN `data/JEZ_ctx_B_soc_008_DTM_20m.tif` with DOI 10.5066/P906QQT8 `data/jez_hirise_site_height_4m.png` and `data/jez_hirise_site_nasa_color_1m.png` driving mesh `NASA_HiRISE_DTM_Jezero_FieldSite_4m` with 1132096 vertices and SHA256 digest ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd.

## 3. Scientific Data Provenance and Key Coordinates
The 4096 meter Jezero delta field site centers near Wright Brothers Field at 18.44486° N 77.45102° E and fuses HiRISE ortho-mosaic JEZ_hirise_soc_006_orthoMosaic_25cm with DOI 10.5066/P9QJDP48 Viking MDIM 2.1 global mosaic and true RGB color from HiRISE observation ESP_045994_1985.

## 4. Evaluated EVA Traverses
Algorithmic A* routes over the 4m HiRISE mesh compare Shortest route MS_Route_Shortest_HiRISE at 1113.50 meters and 24.1 degree peak slope Safest route MS_Route_Safest_HiRISE at 1159.95 meters and 13.8 degree peak slope and Science route MS_Route_Science_HiRISE at 1203.21 meters while the 20m CTX solver evaluates regional 5367 meter versus 6483 meter traverses.

## 5. Blender 5.2 Hotkeys and Production Scene Controls
Interactive viewport operator `tools/mars_front_view.py` binds Key 1 Surface Base Key 2 Orbital Mars Globe and Starfield Key 3 Agriculture and Hydroponics Zone Key 4 Habitat and Fleet Close-up Key 5 Martian Day and Night Cycle and Key 6 Mars Exploration Fleet Showcase.

## 6. Base Camp and Agriculture Asset Map
Collection MS_Agriculture_Zone houses 513 objects including Hydroponics Greenhouse MS_GH1 Geodesic Aeroponics Dome MS_GH2 with 98.4 percent water recovery Habitat Pod 39-part Perseverance Rover Heavy Crew Vehicle Recon Drone InSight Lander and 4 EVA Astronauts.

## 7. Multi-Platform Runtime Parity
Synchronized 5-Chapter Story Sol 1042 Echoes of Jezero Beer-Lambert dust storm attenuation 3D Hazard Solar and RF terrain heatmaps RIMFAX subsurface X-Ray scanner bilingual English and Arabic RTL localization and 14 CFR 1221 public data compliance.
