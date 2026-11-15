# MARS SENTINEL

**Interplanetary Survival Guide: Martian Map**  
*NASA Space Apps Challenge 2026*  
**Target Location:** Jezero Crater, Mars (`18.42875° N, 77.44215° E`)

---

## Overview

MARS SENTINEL is an EVA tactical planning and decision-support platform for crewed Martian exploration. Built upon verified NASA/USGS orbital elevation models and HiRISE imagery, it bridges high-fidelity 3D tactical simulation in Blender 5.2 with a lightweight, client-side Three.js digital twin.

---

## Quick Navigation & Architecture Links

- 📖 **Single Source of Truth (SSOT)**: [`docs/SYSTEM_GUIDE.md`](docs/SYSTEM_GUIDE.md) — Comprehensive technical guide covering architecture, GIS data provenance, coordinates, A* routes, asset maps, and telemetry.
- 🛡️ **Mandatory Agent Rules**: [`docs/AGENTS.md`](docs/AGENTS.md) — In-place editing rules, protected baselines, and operational guardrails for all AI agents.
- 🚀 **Active 3D Master Scene**: `MARS_SENTINEL_HERO_PRODUCTION.blend` (Blender 5.2 production environment).
- 🌐 **Interactive Web Platform**: [`web/index.html`](web/index.html) (Pure client-side Three.js digital twin).
- 🎮 **Unified Startup Controller**: [`tools/mars_front_view.py`](tools/mars_front_view.py) (Blender N-panel dashboard and hotkey bindings).

---

## Core Features

### 1. High-Fidelity 3D Simulation (Blender 5.2)
- **Active Master Scene**: `MARS_SENTINEL_HERO_PRODUCTION.blend`
- **Scientific Terrain**: 4 m resolution USGS HiRISE DTM mesh (`NASA_HiRISE_DTM_Jezero_FieldSite_4m`, 1,132,096 vertices) anchored to verified NASA coordinates.
- **Base Camp & Agriculture**: 513 assets in `MS_Agriculture_Zone` featuring vaulted hydroponics greenhouse, geodesic aeroponics dome, water/nutrient recycling, and perchlorate remediation.
- **Vehicles & Crew**: Perseverance Rover, Exploration Vehicle, Reconnaissance Drone, InSight Lander, and 4 crew members.
- **Viewport Hotkeys (`tools/mars_front_view.py`)**:
  - `Key 1`: Surface Base Camp, Crew, Rovers & Agriculture Zone
  - `Key 2`: Orbital Mars Globe with Jezero Pin & Milky Way Starfield
  - `Key 3`: Mars Agriculture, Hydroponics & Aeroponics Dome
  - `Key 4`: Habitat Base Pod & Rover Fleet Close-up
  - `Key 5`: Live Martian Day/Night Cycle (Day → Sunset → Twilight → Night → Sunrise)
  - `Key 6`: Mars Exploration Fleet Showcase (`MS_Mars_Fleet_Surface`)

### 2. Interactive Web Platform (`web/`)
- Pure client-side Three.js application with zero backend dependencies.
- Open `web/index.html` directly in any modern browser or serve via `python -m http.server 8000`.
- Real-time A* route evaluation (Shortest, Safest, Science).
- Dynamic solar cycle slider matching atmospheric Mie scattering.
- Real-time life-support and agriculture telemetry HUD.

### 3. Game Engine Simulation (Unity 6 — `6000.6.9f1` & `Unity CLI 1.0.0`)
- **Primary Game Engine**: Unity Engine (`6000.6.9f1`) managed via `Unity CLI 1.0.0`.
- **3D & Data Pipeline**: Imports the 1:1 Blender master scene (`MARS_SENTINEL_HERO_PRODUCTION.blend`) via FBX/GLB alongside NASA Space Apps datasets (`data/routes_traverses/`, `data/geology/`, `data/elevation_dtm/`, `data/imagery/`).
- **Target Builds**: Unity WebGL, Windows Standalone (URP/HDRP), and OpenXR.

---

## Protected Baselines

The following scientific baselines are permanently protected and immutable:
- `archive/Mars_backup_before_quality_pass.blend` (Historical reference backup)
- `data/JEZ_hirise_soc_006_DTM_1m.tif` (USGS HiRISE DTM GeoTIFF, DOI `10.5066/P9REJ9JN`)
- `data/JEZ_ctx_B_soc_008_DTM_20m.tif` (USGS Mars 2020 CTX DTM GeoTIFF, DOI `10.5066/P906QQT8`)
- HiRISE mesh coordinate hash: `ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd`


---

## 4. Playable Exploration, NASA Science & Facilities (`Unity 6` + `Web`)

- **Unity Controls**: `[F]` Explore/Showcase, `WASD`/Arrows walk/drive (`3.72 m/s^2`, `Shift` sprint, `Space` jump), `[E]` inspect/board 6x6 Pressurized Rover, `[V]` visor 1st/3rd-person, `[B]` headlamp/lights, `[Tab]` cursor, `[K]` audio mute.
- **NASA Science & Hardware**: Click/scan **50 targets** (12 Jezero outcrops, 22 sample tubes, 10 spacecraft, facilities) for Mastcam-Z photos, coordinates, mineralogy, geochemistry, and MEDA/RAD/Mount Sharp data alongside walkable **Hydroponics Dome**, **Medical Bay**, **NASA HDU + Mark III Suit**, **EDL Craft**, and **Ascent Rocket Pad** (`Saturn V`, `Ares 1`, `Mobile Launcher`). Public NASA data used under `14 CFR 1221` (educational entry, not NASA-endorsed).
- **Web Digital Twin (`web/index.html`)**: Mirrored Medical Bay, EDL craft, Ascent Pad, clickable outcrops, MEDA/RAD readouts, WASD pan, and WebAudio toggle.
