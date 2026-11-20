"""Renders 3 authentic 1920x1080 production shots directly from MARS_SENTINEL_HERO_PRODUCTION.blend
(read-only in memory; never modifies or overwrites the protected .blend file on disk):
1. showcase/hero_mars_planet_and_sun_orbit.png — Mars Planet from outer space with the blazing Sun,
   atmospheric crescent rim, Phobos, Deimos, reconnaissance satellite & crisp starfield.
2. showcase/blender_3d_night_starfield.png — Wide 3D Jezero Crater Surface Base at NIGHT using the
   built-in Mars_Sky_NASA procedural pinpoint starfield, Phobos & Deimos moons above the base,
   Geodesic & Vaulted Hydroponics Greenhouses, Perseverance, Crew Rover, Drones & A* routes.
3. showcase/blender_3d_day_closeup.png — High-detail 3D Surface Base, Perseverance Rover, Astronauts,
   Geodesic & Vaulted Greenhouses, Phobos & Deimos moons & 1,132,096-vertex 4m HiRISE terrain in daylight.
"""
import math
import os
import sys
import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

from tools.mars_front_view import apply_martian_time_of_day

HERO_BLEND = os.path.join(ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
SHOWCASE = os.path.join(ROOT, "showcase")


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


def hide_black_pod_and_frame_wide_base(scn, lens=26.0):
    # Ensure the ugly black Sketchfab_model.012 pod stays hidden so Phobos & Deimos and the horizon are unobstructed
    for o in bpy.data.objects:
        if "sketchfab_model.012" in o.name.lower():
            o.hide_render = True
            o.hide_viewport = True
            for ch in o.children_recursive:
                ch.hide_render = True
                ch.hide_viewport = True

    cam = bpy.data.objects.get("Astronauts_Camera") or scn.camera
    if cam and cam.type == "CAMERA":
        if cam.animation_data:
            cam.animation_data_clear()
        eye = Vector((-3.2, -21.0, 3.8))
        target = Vector((-2.2, 16.0, 2.5))
        direction = (target - eye).normalized()
        cam.location = eye
        cam.rotation_mode = "QUATERNION"
        cam.rotation_quaternion = direction.to_track_quat("-Z", "Y")
        cam.rotation_mode = "XYZ"
        cam.data.lens = lens
        scn.camera = cam


def render_1_mars_planet_with_sun():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)

    cam = (
        bpy.data.objects.get("MARS_NEW_Globe_Camera")
        or bpy.data.objects.get("Mars_Orbit_View_Camera")
        or scn.camera
    )
    if cam:
        scn.camera = cam
        if cam.data and hasattr(cam.data, "lens"):
            cam.data.lens = max(cam.data.lens * 1.32, 48.0)

    out = os.path.join(SHOWCASE, "hero_mars_planet_and_sun_orbit.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_1_ORBIT_SUN_OK] {out} ({os.path.getsize(out):,} B)")


def render_2_night_base_with_stars():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)
    scn.frame_set(76)

    # Use the scene's authentic Mars_Sky_NASA procedural pinpoint starfield & night atmosphere!
    apply_martian_time_of_day("NIGHT", force_space_stars=False)
    hide_black_pod_and_frame_wide_base(scn, lens=26.0)

    # Subtle fill lights so the rovers, astronauts, and greenhouses pop cleanly under Phobos & Deimos
    night_lights = [
        ("Night_Agri_EmeraldGlow", (-18.5, 19.0, 4.0), (0.35, 1.0, 0.65), 1400.0, 28.0),
        ("Night_Rover_Headlamp", (7.5, 11.5, 3.2), (0.85, 0.93, 1.0), 1200.0, 24.0),
        ("Night_Crew_WarmFill", (-3.5, 8.0, 3.4), (1.0, 0.72, 0.45), 950.0, 22.0),
    ]
    for lname, lpos, lcol, lenergy, ldist in night_lights:
        ld = bpy.data.lights.new(lname, type="POINT")
        ld.energy = lenergy
        ld.color = lcol
        ld.shadow_soft_size = 1.6
        lo = bpy.data.objects.new(lname, ld)
        lo.location = lpos
        scn.collection.objects.link(lo)

    out = os.path.join(SHOWCASE, "blender_3d_night_starfield.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_2_NIGHT_STARS_OK] {out} ({os.path.getsize(out):,} B)")


def render_3_daylight_base_closeup():
    bpy.ops.wm.open_mainfile(filepath=HERO_BLEND)
    scn = bpy.context.scene
    configure_render(scn)
    scn.frame_set(76)
    apply_martian_time_of_day("DAY", force_space_stars=False)
    hide_black_pod_and_frame_wide_base(scn, lens=26.0)

    out = os.path.join(SHOWCASE, "blender_3d_day_closeup.png")
    scn.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER_3_DAY_CLOSEUP_OK] {out} ({os.path.getsize(out):,} B)")


if __name__ == "__main__":
    os.makedirs(SHOWCASE, exist_ok=True)
    render_2_night_base_with_stars()
    render_3_daylight_base_closeup()
