"""Renders 3 authentic 1920x1080 production shots directly from MARS_SENTINEL_HERO_PRODUCTION.blend
(read-only in memory; never modifies or overwrites the protected .blend file on disk):
1. showcase/hero_mars_planet_and_sun_orbit.png — Mars Planet from outer space with the blazing Sun,
   atmospheric crescent rim, Phobos, Deimos, reconnaissance satellite & crisp starfield.
2. showcase/blender_3d_night_starfield.png — 3D Jezero Crater Surface Base at NIGHT with vivid stars
   in the Martian sky, illuminated habitat pod, glowing hydroponics dome, rover headlights & A* routes.
3. showcase/blender_3d_day_closeup.png — High-detail 3D Surface Base, Perseverance Rover, Astronauts,
   Habitat Pod, Agri-Dome & 1,132,096-vertex 4m HiRISE terrain in golden Martian daylight.
"""
import math
import os
import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HERO_BLEND = os.path.join(ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
SHOWCASE = os.path.join(ROOT, "showcase")
DATA = os.path.join(ROOT, "data")


def configure_render(scn, w=1920, h=1080):
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scn.render.engine = eng
            break
        except Exception:
            pass
    scn.render.resolution_x = w
    scn.render.resolution_y = h
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"


def ensure_starfield_world(scn, strength=1.25):
    world = scn.world
    if not world:
        world = bpy.data.worlds.new("MarsWorld")
        scn.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = strength
    star_path = os.path.join(DATA, "starfield_equirectangular.png")
    if os.path.isfile(star_path):
        tex = nt.nodes.new("ShaderNodeTexEnvironment")
        tex.image = bpy.data.images.load(star_path)
        nt.links.new(tex.outputs["Color"], bg.inputs["Color"])
    else:
        bg.inputs["Color"].default_value = (0.01, 0.015, 0.03, 1.0)
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])


def render_1_mars_planet_with_sun():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)

    # Use MARS_NEW_Globe_Camera which frames Mars Globe, orbital rings, Phobos, Deimos, MRO & the scene's built-in glowing Space Sun!
    cam = (
        bpy.data.objects.get("MARS_NEW_Globe_Camera")
        or bpy.data.objects.get("Mars_Orbit_View_Camera")
        or scn.camera
    )
    if cam:
        scn.camera = cam
        # Zoom lens slightly closer so Mars Globe & its orbital rings + the natural Space Sun fill the 16:9 frame heroically
        if cam.data and hasattr(cam.data, "lens"):
            cam.data.lens = max(cam.data.lens * 1.32, 48.0)

    out = os.path.join(SHOWCASE, "hero_mars_planet_and_sun_orbit.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_1_ORBIT_SUN_OK] {out} ({os.path.getsize(out):,} B)")


def fix_base_hero_visibility():
    # Ensure the iconic 4-legged NASA Habitat Pod (Sketchfab_model.012) is visible in render
    # and hide any non-NASA sci-fi robot rig so the base is 100% authentic NASA Mars architecture
    for o in bpy.data.objects:
        nl = o.name.lower()
        if "sketchfab_model.012" in nl or ("habitat" in nl and "pod" in nl):
            o.hide_render = False
            for ch in o.children_recursive:
                ch.hide_render = False
        if "ludovisions" in nl or "fc1" in nl:
            o.hide_render = True
            for ch in o.children_recursive:
                ch.hide_render = True


def render_2_night_base_with_stars():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)
    fix_base_hero_visibility()

    cam = bpy.data.objects.get("Astronauts_Camera") or scn.camera
    if cam:
        scn.camera = cam

    # 1. Replace daytime beige sky with crystal-clear Martian NIGHT Starfield!
    ensure_starfield_world(scn, strength=1.55)

    # Hide any opaque sky-dome mesh that would block the night stars
    for o in bpy.data.objects:
        nl = o.name.lower()
        if any(k in nl for k in ("skydome", "sky_dome", "atmosphere_dome", "day_sky", "sky_sphere", "background_sky")):
            o.hide_render = True

    # Dim directional sun lights to cool blue Martian moonlight (0.28 energy) so the 3D terrain & stars both read clearly!
    for o in bpy.data.objects:
        if o.type == "LIGHT" and o.data.type == "SUN":
            o.data.energy = 0.32
            o.data.color = (0.45, 0.62, 0.95)

    # Add dramatic nocturnal warm Floodlights & glowing Bio-Dome / Habitat / Rover lights across the base!
    night_lights = [
        ("Night_Hab_WarmSpot", (1.8, 21.5, 5.2), (1.0, 0.72, 0.38), 2400.0, 28.0),
        ("Night_Agri_EmeraldGlow", (-18.5, 19.0, 4.0), (0.25, 1.0, 0.65), 2200.0, 30.0),
        ("Night_Agri_MagentaGrow", (-24.0, 21.5, 3.8), (0.95, 0.35, 0.85), 1800.0, 26.0),
        ("Night_Rover_Headlamp", (9.5, 12.5, 3.2), (0.85, 0.95, 1.0), 2100.0, 25.0),
        ("Night_Crew_RimFill", (-4.5, 8.5, 3.5), (1.0, 0.65, 0.35), 1400.0, 22.0),
    ]
    for lname, lpos, lcol, lenergy, ldist in night_lights:
        ld = bpy.data.lights.new(lname, type="POINT")
        ld.energy = lenergy
        ld.color = lcol
        ld.shadow_soft_size = 1.5
        if hasattr(ld, "use_custom_distance"):
            ld.use_custom_distance = True
            ld.cutoff_distance = ldist
        lo = bpy.data.objects.new(lname, ld)
        lo.location = lpos
        bpy.context.scene.collection.objects.link(lo)

    out = os.path.join(SHOWCASE, "blender_3d_night_starfield.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_2_NIGHT_STARS_OK] {out} ({os.path.getsize(out):,} B)")


def render_3_daylight_base_closeup():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)
    fix_base_hero_visibility()

    cam = bpy.data.objects.get("Astronauts_Camera") or scn.camera
    if cam:
        scn.camera = cam

    out = os.path.join(SHOWCASE, "blender_3d_day_closeup.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_3_DAY_CLOSEUP_OK] {out} ({os.path.getsize(out):,} B)")


if __name__ == "__main__":
    os.makedirs(SHOWCASE, exist_ok=True)
    render_1_mars_planet_with_sun()
    render_2_night_base_with_stars()
    render_3_daylight_base_closeup()
