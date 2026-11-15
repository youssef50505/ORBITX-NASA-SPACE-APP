# NASA Space Apps 2026 — Historical Mars Exploration Fleet Catalog & Simulation Blueprint

**Event:** NASA Space Apps Challenge 2026  
**Challenge:** *Interplanetary Survival Guide: Martian Map*  
**Operational Base:** Jezero Crater (`18.42875° N, 77.44215° E`)  
**Asset Folder:** `data/missions_3d/`  

---

## 1. Challenge Alignment: From Robotic Precursors to Human Survival

The NASA Space Apps Challenge *Interplanetary Survival Guide: Martian Map* mandates creating a layered, multi-mission decision support environment that leverages decades of NASA robotic exploration data to ensure the survival and scientific success of human astronauts on Mars.

Before human boots touch Martian regolith, decades of robotic explorers mapped the terrain, sampled the rocks, analyzed the thin carbon dioxide atmosphere, detected subsurface water ice, and recorded seismic activity. This catalog records the 3D digital twin assets acquired and structured into the project to support both tactical EVA route planning and historical exploration education.

---

## 2. Acquired 3D Mission Assets (`data/missions_3d/`)

| Mission / Vehicle | Agency | Role & Scientific Objective | File Format & Location | Scale / Notes |
|---|---|---|---|---|
| **Perseverance Rover** | NASA | Core Jezero explorer; sample cache collection; Mastcam-Z & SuperCam | Main Scene (`MARS_SENTINEL_HERO_PRODUCTION.blend`) + Standalone GLB (`data/missions_3d/perseverance/`) | 39 mechanical components, grounded flush at base camp |
| **Ingenuity Mars Helicopter** | NASA | First powered atmospheric flight on another planet; aerial scouting | GLB (`data/missions_3d/ingenuity/Ingenuity_Mars_Helicopter.glb`) | True scale; rotor blades and solar mast intact |
| **Curiosity Rover (MSL)** | NASA | Gale Crater habitability and boxwork geological study; SAM & CheMin | High-poly .blend (`data/missions_3d/curiosity/Curiosity Rover (MSL) (Clean).blend`) | Complete rover chassis, wheels, mast, robotic arm |
| **Spirit & Opportunity (MER)** | NASA | Search for past liquid water; hematite spherules ("blueberries") | High-poly .blend (`data/missions_3d/spirit_opportunity/`) + Standalone GLB (`data/missions_3d/opportunity/`) | Authentic NASA solar array and instrument deployment |
| **InSight Lander** | NASA | Interior structure, Martian seismology (SEIS), heat flow | GLB (`data/missions_3d/insight/InSight Cruise Lander (panels deployed).glb`) | Deployed solar panels; Base Camp telemetry relay |
| **Viking Lander 1 & 2** | NASA | First long-term operational surface missions (1976); biology experiments | GLB (`data/missions_3d/viking_lander/Viking Lander.glb`) | Three-legged landing gear, sampler arm, meteorology boom |
| **Mars Reconnaissance Orbiter (MRO)** | NASA | Ultra-high-resolution imaging (HiRISE 25cm/px) that provided our terrain | GLB (`data/missions_3d/mro/Mars Reconnaissance Orbiter (MRO) (A).glb`) | Dual solar wings, High Gain Antenna dish |
| **Mars Odyssey** | NASA | Longest-operating Mars orbiter; THEMIS thermal & chemical mapper | GLB (`data/missions_3d/mars_odyssey/Mars Odyssey.glb`) | Solar panel boom, star cameras, instrument deck |
| **MAVEN** | NASA | Upper atmosphere loss to solar wind; ion & neutral mass spectrometer | GLB (`data/missions_3d/maven/Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb`) | Articulated solar panels, magnetometer booms |
| **Mars Global Surveyor** | NASA | MOLA laser altimetry mapping Mars global topography | GLB (`data/missions_3d/global_surveyor/Mars Global Surveyor.glb`) | Solar panels and high-gain antenna |

---

## 3. Game Engine (Unity) Integration Architecture

### 3.1 Dual-Pipeline Design
1. **Asset Master (Blender 5.2)**: 
   - Authentic USGS/NASA HiRISE 4m DTM mesh (`1,132,096` vertices).
   - High-fidelity physical materials (PBR) and calibrated Martian daylight Mie scattering.
   - Master export pipeline to GLTF/FBX.
2. **Simulation & Gameplay (Unity Engine)**:
   - **Physics & Gravity**: Mars surface gravity set to $3.72 \text{ m/s}^2$ ($0.38g$).
   - **Rover Controller**: WheelColliders with dynamic suspension, motor torque curves, slip simulation on loose Martian sand.
   - **Ingenuity Flight Model**: Blade lift calculated against thin atmospheric density ($\sim 0.020 \text{ kg/m}^3$, $\sim 1\%$ of Earth).
   - **Astronaut Helmet Visor HUD**: Heads-Up Display showing EVA oxygen reserves, real-time terrain slope warnings ($>15^\circ$), route waypoint guidance, and radiation exposure.
   - **Target Builds**: Unity WebGL (for instant zero-install browser evaluation by NASA Space Apps judges), PC Standalone (high visual fidelity), and OpenXR (VR exploration).
