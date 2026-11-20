# ORBITX · MARS SENTINEL

**Interplanetary Survival Guide: Martian Map**  
*NASA Space Apps Challenge 2026 — Team ORBITX (`orbitX00`)*  
**Target Location:** Jezero Crater, Mars (`18.4447° N, 77.4508° E · -2,569.9 m MOLA`)

---

## Vision: Toward "Google Earth for Mars" (Web + 3D + Game)

Our long-term goal is to scale **ORBITX: Mars Sentinel** into a unified **"Google Earth for Mars"** — an open, planetary-scale exploration, navigation, and mission-planning ecosystem that seamlessly connects **three synchronized surfaces** backed by real NASA & USGS science:

1. **Interactive Web Digital Twin (`web/index.html`)** — Zero-install, browser-based 3D Mars globe & Jezero Crater twin with a modern 3D-first aerospace glass HUD, live dynamic 8-connected A\* traverse planner (`nav_sun_engine.js`), Mars orbital solar/shadow optics (`Ls 0°–355°`), terrain-occluded RF Line-of-Sight viewshed, Outpost Site Check, **Omni-Probe Planetary X-Ray Scanner**, 7-Slide Pitch Deck, and one-click **`● Rec 28s`** WebM video capture.
2. **Playable Game & Survival Simulation (`unity_project/` — Unity 6 `6000.6.9f1`)** — Standalone Windows 64-bit simulation (`MarsSentinel.exe`) featuring authentic NASA GLB hardware (`mars-2020-perseverance-rover.glb`, `Astronaut.glb`, `Ingenuity`, `Curiosity`, `HDU`, `Saturn V`, orbiters), 6-point Rocker-Bogie rover suspension & A\* Autopilot (`[R]`), procedural astronaut motion & camera collision, live EVA Survival Loop (`O2`, `Power`, `Radiation`, `RF Signal`, MedEvac, 3 collectible Jezero core samples), URP ACES Post-Processing + pooled Martian dust VFX, FBM sand-slope terrain shading, and full C# `nav-module` integration (`MarsCtxNavSolarEngine`).
3. **High-Fidelity Scientific 3D Master Pipeline (`MARS_SENTINEL_HERO_PRODUCTION.blend` — Blender 5.2)** — 4 m resolution USGS HiRISE DTM (`1,132,096` vertices, SHA-256 verified) and 80 m USGS CTX DTM mosaic (`399×378`) feeding both Web and Unity assets.

---

## Quick Navigation & Architecture Links

- 🌐 **Interactive Web Digital Twin:** [`web/index.html`](web/index.html) (serve locally via `python -m http.server 8080` and open `http://127.0.0.1:8080/web/index.html`)
- 🧭 **Core Navigation & Solar/RF Module:** [`nav-module/nav.py`](nav-module/nav.py), [`web/nav_sun_engine.js`](web/nav_sun_engine.js), and [`unity_project/Assets/Scripts/NASA/MarsCtxNavSolarEngine.cs`](unity_project/Assets/Scripts/NASA/MarsCtxNavSolarEngine.cs)
- 🎮 **Unity 6 Survival Game Scripts:** [`unity_project/Assets/Scripts/NASA/`](unity_project/Assets/Scripts/NASA/)
- 📊 **Presentation Deck (7 Slides):** [`docs/presentation/ORBITX_Deck.pdf`](docs/presentation/ORBITX_Deck.pdf) & [`Mars_Sentinel_OrbitX00.pptx`](Mars_Sentinel_OrbitX00.pptx)
- 📖 **Single Source of Truth (SSOT):** [`docs/SYSTEM_GUIDE.md`](docs/SYSTEM_GUIDE.md)
- 🛡️ **Agent Guardrails:** [`docs/AGENTS.md`](docs/AGENTS.md)

---

## Core Capabilities

### 1. Live Dynamic A\* Navigation, Solar Optics & RF Viewshed (`nav-module` + `web/` + `Unity 6`)
- **8-Connected 3D A\* Pathfinding** over the real USGS Mars 2020 CTX DTM Mosaic (`399×378`, `80 m` cells, DOI `10.5066/P906QQT8`): dynamically solves **Shortest** vs. **Safest** routes (penalizing slope `<= 25°` and surface roughness rock-proxy) with **Douglas-Peucker (`120 m`)** waypoint simplification.
- **Mars Orbital Solar & Shadow Optics:** Computes solar elevation/azimuth and Beer-Lambert irradiance across Solar Longitude `Ls (0°–355°)` and Martian solar hour `(0–24 h)` with 75-step terrain shadow ray-marching.
- **Radio Line-of-Sight (RF Viewshed):** Flags terrain-occluded valleys between rover/astronaut and base antenna in real time.
- **Outpost Site Suitability Check:** Evaluates candidate landing/outpost locations against `Slope <= 10°`, `Roughness <= 2.0 m`, and `Daylight >= 50%`.

### 2. Omni-Probe Planetary X-Ray Scanner (`[X]` / Click Anywhere)
- Click any point on the Martian surface, rock outcrop, iron-nickel meteorite (`Cacao`, `Lebanon`), `Belva` Crater, base facility, or sky body (`Sol`, `Phobos`, `Deimos`, `Earth/DSN`) to inspect live IAU2000 coordinates, elevation, slope, Thermal Inertia (TIU), USGS SIM 3292 geologic unit (all 44 units), PIXL/SHERLOC oxide chemistry, and a 3-layer RIMFAX subsurface radar depth profile (`0–15 m`).

### 3. Playable Unity 6 Mars Survival & Exploration Game (`MarsSentinel.exe`)
- **Controls:**
  - `[F]` Explore / Showcase · `WASD`/Arrows walk & drive (`3.72 m/s²` Mars gravity, `Shift` sprint, `Space` jump)
  - `[E]` Board/Dismount NASA Perseverance GLB Rover or collect glowing Jezero Core Samples
  - `[R]` Toggle Rover A\* Safest Route Autopilot · `[V]` 1st/3rd-Person Visor · `[B]` Headlamp/Lights
  - `[X]` Omni-Probe X-Ray · `[Q]` Cycle CTX A\* Preset · `[J]` Advance Mars `Ls` Season · `[Y]` Toggle Clean/Full HUD
  - `[1–5]` Cameras · `[H]` Data Layer Isolation · `[D/C]` Dust Storm `τ` · `[N]` 28s NASA Demo · `[P]` 60s Demo · `[K]` Audio Mute
- **Survival Loop:** Live Oxygen (`O2 %`), Solar/Battery Power (`PWR %`), Radiation Trauma (`RAD %`), RF Signal (`RF %`), shelter recharging in **Hydroponics Dome / NASA HDU / Medical Bay**, 3 collectible Jezero core samples with score/rank, and automatic **Emergency MedEvac**.

---

## Authentic NASA & USGS Datasets
- **MRO HiRISE 4 m DTM** (`DTEEC_048842_1985_048908_1985_A01`, `1,132,096` vertices, SHA-256 verified) & **MOLA MEGDR**
- **USGS Mars 2020 TRN CTX DTM Mosaic** (DOI `10.5066/P906QQT8`) & **USGS SIM 3292 Geologic Map of Mars** (44 units, Tanaka et al., 2014)
- **Mars 2020 Perseverance** (559 traverse legs with 15 altitude dropouts filtered, 22 Sample Tubes, 12 Outcrops, Mastcam-Z, PIXL, SHERLOC, RIMFAX, MOXIE)
- **Ingenuity Mars Helicopter** (72 flights), **MSL Curiosity** (1,316 segments), **MEDA / RAD / InSight / NASA DONKI**, and official **NASA 3D Resources** (`14 CFR 1221` compliant, educational entry, not NASA-endorsed).

---

## Verification & Automated Testing
```bash
# 1. Workspace & Unity C# / .meta integrity audit
python -B scripts/audit_workspace_integrity.py --json

# 2. Nav-module + Web JS + Unity overhaul integration tests
python -B -m unittest tests/test_nav_module_integration.py -v

# 3. Full cross-platform regression & live Edge CDP test suite
python -B -m unittest tests/test_e2e_suite.py tests/test_milestone4_empirical_challenge.py tests/test_milestone6_empirical_challenge.py tests/test_m10_crossplatform_adversarial_challenge.py tests/test_milestone11_resources_and_integration.py tests/test_milestone8_empirical_challenge.py -v
```
