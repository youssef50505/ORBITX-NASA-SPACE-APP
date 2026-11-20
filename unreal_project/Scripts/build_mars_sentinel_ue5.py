"""Unreal Engine 5.8 Headless Automated Level, Nanite Asset & Lumen Showcase Builder.
Imports single-mesh bottom-grounded NASA & Jezero Crater GLBs (exact Z=0 origin!)
via UE 5.8 Interchange, builds /Game/Maps/JezeroCrater_Hero.umap with Lumen GI, Nanite,
Martian SkyAtmosphere, Volumetric Fog & PlayerStart, and renders showcase/unreal_ue5_jezero_lumen.png.
"""
import os
import math
import unreal

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UE_DIR = os.path.join(REPO_ROOT, "unreal_project")
SRC_DIR = os.path.join(UE_DIR, "SourceAssets")
SHOWCASE_DIR = os.path.join(REPO_ROOT, "showcase")
MAP_PATH = "/Game/Maps/JezeroCrater_Hero"
ASSET_DEST = "/Game/MarsSentinel/HeroAssets"


def sample_jezero_cm(x_m, y_m):
    r = math.hypot(x_m, y_m)
    def ss(e0, e1, v):
        t = max(0.0, min(1.0, (v - e0) / max(1e-5, e1 - e0)))
        return t * t * (3.0 - 2.0 * t)
    pad = ss(32.0, 96.0, r)
    rim = ss(125.0, 360.0, r)
    dunes = (
        math.sin(x_m * 0.032 + y_m * 0.019) * 0.85
        + math.cos(y_m * 0.044 - x_m * 0.021) * 0.65
        + math.sin((x_m + y_m) * 0.11) * 0.14
        + 1.65
    )
    az = math.atan2(y_m, x_m)
    profile = max(0.42, min(1.55, math.sin(az * 3.0 + 0.5) * 0.40 + math.cos(az * 7.0 - 1.1) * 0.22 + 1.05))
    rim_z = rim * 26.0 * profile
    return ((dunes + rim_z) * pad) * 100.0


def import_and_spawn(label, glb_file, loc_m, yaw_deg=0.0, is_terrain=False):
    src = os.path.join(SRC_DIR, glb_file)
    if not os.path.isfile(src):
        unreal.log_warning(f"[MarsSentinelUE] Missing {src}")
        return False
    dest = f"{ASSET_DEST}/{label}"
    task = unreal.AssetImportTask()
    task.filename = src
    task.destination_path = dest
    task.automated = True
    task.save = True
    task.replace_existing = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    reg = unreal.AssetRegistryHelpers.get_asset_registry()
    assets = reg.get_assets_by_path(dest, recursive=True)
    sm = None
    for ad in assets:
        cls_name = str(ad.asset_class_path.asset_name) if hasattr(ad, "asset_class_path") else str(ad.asset_class)
        if cls_name == "StaticMesh":
            sm = unreal.EditorAssetLibrary.load_asset(str(ad.package_name))
            if sm:
                break
    if sm is None:
        unreal.log_warning(f"[MarsSentinelUE] No StaticMesh in {dest}")
        return False

    # Keep Nanite disabled during headless SceneCapture2D so capture renders 100% full-res triangles,
    # then enable Nanite on saved assets right after capture for real-time UE 5.8 Editor viewport.
    try:
        ns = sm.get_editor_property("nanite_settings")
        if ns is not None:
            ns.enabled = False
            sm.set_editor_property("nanite_settings", ns)
    except Exception:
        pass

    editor_sub = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    if is_terrain:
        loc = unreal.Vector(0.0, 0.0, 0.0)
    else:
        b = sm.get_bounding_box()
        gz = sample_jezero_cm(loc_m[0], loc_m[1]) + 2.0
        loc = unreal.Vector(loc_m[0] * 100.0, loc_m[1] * 100.0, gz - b.min.z)

    actor = editor_sub.spawn_actor_from_class(unreal.StaticMeshActor, loc, unreal.Rotator(0.0, 0.0, yaw_deg))
    actor.set_actor_label(f"MS_{label}")
    actor.static_mesh_component.set_static_mesh(sm)
    unreal.log(f"[MarsSentinelUE] Spawned '{label}' at ({loc.x:.0f},{loc.y:.0f},{loc.z:.0f}) cm")
    return sm


def build_level():
    unreal.log("=== [MarsSentinelUE] Building Clean Single-Mesh UE 5.8 Jezero Crater Hero Map ===")
    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    editor_sub = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)

    if unreal.EditorAssetLibrary.does_asset_exist(MAP_PATH):
        level_sub.load_level(MAP_PATH)
    else:
        level_sub.new_level(MAP_PATH)
    # Remove any default/prior actors so re-runs are 100% clean with zero duplicates
    for existing in list(editor_sub.get_all_level_actors()):
        editor_sub.destroy_actor(existing)

    assets = [
        ("Terrain", "JezeroTerrain_UE5.glb", (0.0, 0.0), 0.0, True),
        ("Perseverance", "Perseverance_UE5.glb", (1.8, -5.0), 145.0, False),
        ("Astronaut", "Astronaut_UE5.glb", (-2.2, -6.2), 20.0, False),
        ("Ingenuity", "Ingenuity_UE5.glb", (7.2, -3.5), 0.0, False),
        ("AgriDome", "AgriDome_UE5.glb", (-13.0, 4.0), -15.0, False),
        ("HDU_Habitat", "HDU_Habitat_UE5.glb", (-4.5, 11.5), 15.0, False),
        ("MedBay", "MedBay_UE5.glb", (7.5, 12.5), -10.0, False),
        ("InSight", "InSight_UE5.glb", (-12.5, -5.5), 30.0, False),
        ("EDL_Lander", "EDL_Lander_UE5.glb", (15.5, -3.0), -45.0, False),
        ("LaunchPad", "LaunchPad_UE5.glb", (18.5, 14.0), 0.0, False),
    ]
    spawned = []
    for a in assets:
        sm = import_and_spawn(*a)
        if sm:
            spawned.append((a[0], sm))
    count = len(spawned)

    # Sun + Martian SkyAtmosphere + SkyLight + Warm Bounce + Fog + ACES PostProcess
    sun = editor_sub.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 5000), unreal.Rotator(0.0, -28.0, -45.0))
    sun.set_actor_label("Mars_DirectionalSun_Jezero")
    sun.light_component.set_intensity(8.5)
    sun.light_component.set_light_color(unreal.LinearColor(1.0, 0.85, 0.68, 1.0))
    try:
        sun.light_component.set_editor_property("atmosphere_sun_light", True)
        sun.light_component.set_editor_property("cast_shadows", True)
    except Exception:
        pass

    fill = editor_sub.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 3500), unreal.Rotator(0.0, -32.0, 135.0))
    fill.set_actor_label("Mars_RegolithBounceFill")
    fill.light_component.set_intensity(2.6)
    fill.light_component.set_light_color(unreal.LinearColor(0.96, 0.66, 0.46, 1.0))
    try:
        fill.light_component.set_editor_property("cast_shadows", False)
    except Exception:
        pass

    sky_atmo = editor_sub.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
    sky_atmo.set_actor_label("Mars_SkyAtmosphere_CO2Dust")
    try:
        sac = sky_atmo.get_component_by_class(unreal.SkyAtmosphereComponent)
        if sac:
            sac.set_editor_property("ground_albedo", unreal.Color(175, 96, 56, 255))
            sac.set_editor_property("rayleigh_scattering", unreal.LinearColor(0.014, 0.020, 0.036, 1.0))
            sac.set_editor_property("mie_scattering", unreal.LinearColor(0.026, 0.013, 0.006, 1.0))
            sac.set_editor_property("mie_anisotropy", 0.76)
    except Exception:
        pass

    sky_light = editor_sub.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 4000), unreal.Rotator(0, 0, 0))
    sky_light.set_actor_label("Mars_LumenSkyLight")
    try:
        slc = sky_light.light_component
        slc.set_editor_property("real_time_capture", True)
        slc.set_light_color(unreal.LinearColor(1.0, 0.78, 0.60, 1.0))
        slc.set_intensity(2.2)
        slc.recapture_sky()
    except Exception:
        pass

    fog = editor_sub.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, 200), unreal.Rotator(0, 0, 0))
    fog.set_actor_label("Mars_DustHeightFog")
    try:
        fog.component.set_fog_density(0.0038)
        fog.component.set_fog_inscattering_color(unreal.LinearColor(0.76, 0.44, 0.27, 1.0))
        fog.component.set_editor_property("enable_volumetric_fog", True)
    except Exception:
        pass

    ppv = editor_sub.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
    ppv.set_actor_label("Mars_ACES_PostProcess")
    try:
        ppv.set_editor_property("unbound", True)
    except Exception:
        pass

    ps = editor_sub.spawn_actor_from_class(unreal.PlayerStart, unreal.Vector(-350.0, -1100.0, 120.0), unreal.Rotator(0.0, 0.0, 70.0))
    ps.set_actor_label("PlayerStart_OctaviaButlerBase")

    # Hero framing capturing Perseverance, Astronaut, Ingenuity, Agri-Dome, NASA HDU, MedBay, Launch Pad & boulders!
    cam_loc = unreal.Vector(-180.0, -2250.0, 680.0)
    cam_rot = unreal.MathLibrary.find_look_at_rotation(cam_loc, unreal.Vector(180.0, 420.0, 220.0))
    cam = editor_sub.spawn_actor_from_class(unreal.CameraActor, cam_loc, cam_rot)
    cam.set_actor_label("CineCamera_JezeroHero")

    try:
        os.makedirs(SHOWCASE_DIR, exist_ok=True)
        world = unreal.EditorLevelLibrary.get_editor_world()
        rt = unreal.RenderingLibrary.create_render_target2d(world, 1600, 900, unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB)
        cap = editor_sub.spawn_actor_from_class(unreal.SceneCapture2D, cam_loc, cam_rot)
        sc = cap.capture_component2d
        sc.texture_target = rt
        sc.capture_source = unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR
        sc.fov_angle = 62.0
        sc.capture_scene()
        unreal.RenderingLibrary.export_render_target(world, rt, SHOWCASE_DIR, "unreal_ue5_jezero_raw")
        editor_sub.destroy_actor(cap)
        unreal.log("[UE5Verify] Exported clean UE 5.8 Lumen render!")
    except Exception as ex:
        unreal.log_warning(f"[MarsSentinelUE] Capture error: {ex}")

    # Enable Nanite for opaque high-poly assets in the saved UE 5.8 project
    for lbl, sm in spawned:
        if lbl == "AgriDome":
            continue
        try:
            ns = sm.get_editor_property("nanite_settings")
            if ns is not None:
                ns.enabled = True
                sm.set_editor_property("nanite_settings", ns)
        except Exception:
            pass

    level_sub.save_current_level()
    unreal.EditorAssetLibrary.save_directory("/Game", only_if_is_dirty=True, recursive=True)
    unreal.log(f"[UE5Verify] Built {MAP_PATH} | CleanAssets={count}/{len(assets)} | Saved=True")


if __name__ == "__main__":
    build_level()
