# MARS SENTINEL — Mandatory Agent Workflow & Safety Rules

> **STRICT MANDATE: IN-PLACE EDITING ONLY.**
> Never append historical changelogs, dated diaries, or duplicate appendices to Markdown files.
> All modifications must be made **directly in place** on existing canonical files.
> Keep all documentation lean, current, and synchronized with actual repository code and assets.

---

## 1. Zero Redundant Copies Rule
- **Never create duplicate or numbered copies** of scenes, scripts, data, or documentation (e.g., `*_copy`, `*_backup_v2`, `*_new`, `*_test.blend`).
- Work strictly and directly on the designated active canonical files.
- Version control is handled by Git; do not simulate versioning via file naming.

## 2. Canonical Working Files
- **Active 3D Master Scene**: `MARS_SENTINEL_HERO_PRODUCTION.blend`
- **Target Game Engine**: Unity Engine (`Unity 6 — 6000.6.9f1` via `Unity CLI 1.0.0`)
- **Active Web Platform**: `web/index.html`, `web/styles.css`, `web/app.js`
- **Core Startup & Hotkey Tool**: `tools/mars_front_view.py`
- **Blender MCP Bridge**: `tools/blender_mcp_server.py`
- **Single Source of Truth Reference**: `docs/SYSTEM_GUIDE.md`

## 3. Protected Baselines (Immutable Guardrails)
Under no circumstances should an agent alter, overwrite, or delete:
1. **Untouched Baseline Archive**: `archive/Mars_backup_before_quality_pass.blend`.
2. **NASA HiRISE Terrain Mesh**: `NASA_HiRISE_DTM_Jezero_FieldSite_4m` inside `.blend` scenes. Must strictly maintain:
   - Vertex count: `1,132,096`
   - Scale: `(1.0, 1.0, 1.0)`
   - Location: `(0.23, -0.49, 0.015)`
   - SHA-256 coordinate hash: `ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd`
3. **Raw Scientific Rasters**: `data/JEZ_hirise_soc_006_DTM_1m.tif` (1.84 GB) and `data/JEZ_ctx_B_soc_008_DTM_20m.tif`.

## 4. Live Blender Interaction & Security Guardrails
- All live Blender operations must connect to the active Blender session via the **`blender-mcp`** server on `localhost:9876` (`execute_read_only_query`).
- Never use legacy or unverified MCP servers.
- Keep Blender's global script auto-execution setting **OFF** (`use_scripts_auto_execute = False`). Internal datablocks must maintain `use_module = False` so `"Continue safely or Run"` popups are never triggered.

## 5. Token Conservation & Documentation Discipline
- Onboarding agents must read `docs/SYSTEM_GUIDE.md` and `README.md` only.
- Do NOT read obsolete or archived documents into context.
- When adding new features or fixing bugs, update the relevant section of `docs/SYSTEM_GUIDE.md` in place to reflect the new state.
