# ORBITX · MARS SENTINEL

**Interplanetary Survival Guide: Martian Map**  
*NASA Space Apps Challenge 2026 — Team ORBITX (`orbitX00`)*  
**Target Location:** Jezero Crater, Mars (`18.4447° N, 77.4508° E`)

---

## Vision: Toward "Google Earth for Mars" (Web + 3D + Game)

MARS SENTINEL is an EVA tactical planning and survival platform scaling toward a unified **"Google Earth for Mars"** across three synchronized surfaces backed by authentic NASA & USGS datasets:
1. **Web Digital Twin** ([`web/index.html`](web/index.html)) — Modern frosted-glass 3D-first HUD, dynamic 8-connected CTX A\* planner ([`web/nav_sun_engine.js`](web/nav_sun_engine.js)), Mars orbital solar/shadow optics (`Ls 0°–355°`), RF Line-of-Sight viewshed, Outpost Site Check, **Omni-Probe Planetary X-Ray**, 7-Slide Deck, and **`● Rec 28s`** WebM capture.
2. **Playable Survival Game (Unity 6 `6000.6.9f1`)** — Standalone `MarsSentinel.exe` with authentic NASA GLB hardware (`mars-2020-perseverance-rover.glb`, `Astronaut.glb`, `HDU`, fleet), 6-point Rocker-Bogie suspension & `[R]` A\* Autopilot, procedural astronaut motion & camera collision, live Survival Loop (`O2`, `PWR`, `RAD`, `RF`, 3 core samples, MedEvac), URP ACES Post-Processing + pooled dust VFX, FBM terrain shader, Clean/Full `[Y]` HUD, and C# `nav-module` integration (`MarsCtxNavSolarEngine`).
3. **3D Master Pipeline (Blender 5.2)** — `MARS_SENTINEL_HERO_PRODUCTION.blend` on 4 m HiRISE DTM (`1,132,096` vertices, SHA-256 `ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd`).

---

## Quick Links
- 📖 **Single Source of Truth:** [`docs/SYSTEM_GUIDE.md`](docs/SYSTEM_GUIDE.md)
- 🛡️ **Agent Rules:** [`docs/AGENTS.md`](docs/AGENTS.md)
- 🌐 **Web Platform:** [`web/index.html`](web/index.html)
- 🎮 **Blender Controller:** [`tools/mars_front_view.py`](tools/mars_front_view.py)
- 🧭 **Navigation Module:** [`nav-module/nav.py`](nav-module/nav.py)
- 📊 **Presentation:** [`presentation/ORBITX_Deck.pdf`](presentation/ORBITX_Deck.pdf) · [`presentation/Mars_Sentinel_OrbitX00.pptx`](presentation/Mars_Sentinel_OrbitX00.pptx)

## Repository Layout
`web/` WebGL digital twin · `unity_project/` Unity 6 playable survival game · `nav-module/` CTX A\*/Solar/RF engine · `data/` NASA/USGS datasets & 3D GLBs · `tools/` & `scripts/` Blender & integrity automation · `presentation/` 7-slide deck & shots · `tests/` 231-test suite · `docs/` system & agent docs.

---

## Viewport & Game Hotkeys
- **Blender (`tools/mars_front_view.py`):** `Key 1` Surface Base & Crew · `Key 2` Orbital Mars Globe · `Key 3` Agriculture Dome · `Key 4` Habitat & Rovers · `Key 5` Martian Day/Night Cycle · `Key 6` Exploration Fleet.
- **Unity 6 (`MarsSentinel.exe`):** `[F]` Explore, `WASD`/Arrows move/drive (`3.72 m/s²`), `[E]` Board NASA Rover / collect sample, `[R]` A\* Autopilot, `[V]` Visor, `[B]` Lights, `[X]` Omni-Probe X-Ray, `[Q]` CTX A\* Preset, `[J]` Mars `Ls` Season, `[Y]` Clean/Full HUD, `[H]` Layers, `[D/C]` Storm `τ`, `[N]` 28s Demo, `[P]` 60s Demo, `[K]` Mute.
- **Protected Baselines:** `archive/Mars_backup_before_quality_pass.blend`, `data/JEZ_hirise_soc_006_DTM_1m.tif` (DOI `10.5066/P9REJ9JN`), `data/JEZ_ctx_B_soc_008_DTM_20m.tif` (DOI `10.5066/P906QQT8`). Public NASA data used under `14 CFR 1221` (educational entry, not NASA-endorsed).
