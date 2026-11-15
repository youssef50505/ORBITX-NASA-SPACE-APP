"""
Script to apply Milestone 9 updates to MARS_SENTINEL_HERO_PRODUCTION.blend headlessly.
- Feature 20: Slope-aware procedural blend material, procedural micro-normals, roughness variation
- Feature 21: Continuous 360 degree globe rotation, eliminate frame-76 popping artifact, ensure orbital fleet visibility
"""

import bpy
import math
import hashlib
import array
import os

print("=== STARTING MILESTONE 9 BLENDER UPGRADE ===")

# ----------------------------------------------------------------------
# 1. TIMELINE CALIBRATION
# ----------------------------------------------------------------------
scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 240
scene.frame_current = 1
scene.render.fps = 24
scene.render.fps_base = 1.0

# ----------------------------------------------------------------------
# 2. FEATURE 21: CONTINUOUS 360 MARS GLOBE ROTATION
# ----------------------------------------------------------------------
rot = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
if rot and rot.animation_data and rot.animation_data.action:
    act = rot.animation_data.action
    # Update channelbag fcurves in Blender 5.2
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in list(cb.fcurves):
                    if fc.data_path == 'rotation_euler' and fc.array_index == 2:
                        cb.fcurves.remove(fc)
                fc = cb.fcurves.new(data_path='rotation_euler', index=2)
                fc.extrapolation = 'LINEAR'
                fc.keyframe_points.add(2)
                fc.keyframe_points[0].co = (1.0, 0.0)
                fc.keyframe_points[0].interpolation = 'LINEAR'
                fc.keyframe_points[1].co = (241.0, 2.0 * math.pi)
                fc.keyframe_points[1].interpolation = 'LINEAR'
                fc.update()

# ----------------------------------------------------------------------
# 3. FEATURE 21: RESOLVE FRAME-76 POPPING ARTIFACT
# ----------------------------------------------------------------------
popping_count = 0
for act in bpy.data.actions:
    if hasattr(act, 'layers'):
        for layer in getattr(act, 'layers', []):
            for strip in getattr(layer, 'strips', []):
                for cb in getattr(strip, 'channelbags', []):
                    for fc in list(cb.fcurves):
                        if fc.data_path in ('hide_render', 'hide_viewport'):
                            frames = [kp.co[0] for kp in fc.keyframe_points]
                            if any(abs(f - 76.0) < 1.0 for f in frames):
                                cb.fcurves.remove(fc)
                                popping_count += 1
    if hasattr(act, 'fcurves'):
        for fc in list(act.fcurves):
            if fc.data_path in ('hide_render', 'hide_viewport'):
                frames = [kp.co[0] for kp in fc.keyframe_points]
                if any(abs(f - 76.0) < 1.0 for f in frames):
                    act.fcurves.remove(fc)
                    popping_count += 1

for obj in bpy.data.objects:
    obj.hide_render = False
    obj.hide_viewport = False

print(f"Removed {popping_count} popping fcurves from objects.")

# Ensure orbital fleet collections are visible
for cname in ["Mars_New_Features", "NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"]:
    c = bpy.data.collections.get(cname)
    if c:
        c.hide_viewport = False
        c.hide_render = False

# ----------------------------------------------------------------------
# 4. FEATURE 20: SLOPE-AWARE MATERIAL UPGRADE (Mars_NASA_HiRISE_Viking_Color)
# ----------------------------------------------------------------------
mat = bpy.data.materials.get("Mars_NASA_HiRISE_Viking_Color")
if mat and mat.node_tree:
    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links

    # 4a. Slope Geometry & Normal Z separation
    geom = nodes.get("Slope_Geometry") or nodes.new("ShaderNodeNewGeometry")
    geom.name = "Slope_Geometry"
    geom.label = "Slope Geometry"
    geom.location = (-700, 200)

    sep_norm = nodes.get("Slope_SepXYZ") or nodes.new("ShaderNodeSeparateXYZ")
    sep_norm.name = "Slope_SepXYZ"
    sep_norm.label = "Normal Z (Slope)"
    sep_norm.location = (-500, 200)
    links.new(geom.outputs["Normal"], sep_norm.inputs["Vector"])

    # 4b. Slope Factor (>15 deg slope transition)
    slope_map = nodes.get("Slope_Factor") or nodes.new("ShaderNodeMapRange")
    slope_map.name = "Slope_Factor"
    slope_map.label = "Slope Factor (>15 deg)"
    slope_map.location = (-300, 200)
    slope_map.clamp = True
    slope_map.inputs["From Min"].default_value = 0.966  # cos(15 deg)
    slope_map.inputs["From Max"].default_value = 0.900  # cos(25 deg)
    slope_map.inputs["To Min"].default_value = 0.0
    slope_map.inputs["To Max"].default_value = 1.0
    links.new(sep_norm.outputs["Z"], slope_map.inputs["Value"])

    # 4c. Object-Space Horizontal Rock Strata Bands
    tex_coord = nodes.get("Texture Coordinate")

    rock_wave = nodes.get("Rock_Strata_Wave") or nodes.new("ShaderNodeTexWave")
    rock_wave.name = "Rock_Strata_Wave"
    rock_wave.label = "Rock Strata Bands"
    rock_wave.wave_type = 'BANDS'
    rock_wave.bands_direction = 'Z'
    rock_wave.inputs["Scale"].default_value = 18.0
    rock_wave.inputs["Distortion"].default_value = 2.5
    rock_wave.inputs["Detail"].default_value = 6.0
    rock_wave.inputs["Detail Roughness"].default_value = 0.6
    rock_wave.location = (-300, 450)
    if tex_coord:
        links.new(tex_coord.outputs["Object"], rock_wave.inputs["Vector"])

    rock_ramp = nodes.get("Rock_Color_Ramp") or nodes.new("ShaderNodeValToRGB")
    rock_ramp.name = "Rock_Color_Ramp"
    rock_ramp.label = "Rock Strata Palette"
    rock_ramp.location = (-50, 450)
    cr = rock_ramp.color_ramp
    cr.elements[0].position = 0.2
    cr.elements[0].color = (0.24, 0.16, 0.12, 1.0) # Dark basalt
    cr.elements[1].position = 0.8
    cr.elements[1].color = (0.42, 0.28, 0.20, 1.0) # Clay/hematite strata
    links.new(rock_wave.outputs["Color"], rock_ramp.inputs["Factor"])

    # 4d. Slope Albedo Blend
    flat_mix = nodes.get("Mix")
    albedo_mix = nodes.get("Slope_Albedo_Blend") or nodes.new("ShaderNodeMix")
    albedo_mix.name = "Slope_Albedo_Blend"
    albedo_mix.label = "Slope Albedo Blend"
    albedo_mix.data_type = 'RGBA'
    albedo_mix.blend_type = 'MIX'
    albedo_mix.location = (250, 200)

    links.new(slope_map.outputs["Result"], albedo_mix.inputs["Factor"])
    if flat_mix:
        links.new(flat_mix.outputs["Result"], albedo_mix.inputs["A"])
    links.new(rock_ramp.outputs["Color"], albedo_mix.inputs["B"])

    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        links.new(albedo_mix.outputs["Result"], bsdf.inputs["Base Color"])

    # 4e. Tangent-Space Micro-Normal Rock Strata Bump Chain
    rock_bump = nodes.get("Rock_Strata_Bump") or nodes.new("ShaderNodeBump")
    rock_bump.name = "Rock_Strata_Bump"
    rock_bump.label = "Rock Strata Bump"
    rock_bump.location = (50, -450)
    rock_bump.inputs["Strength"].default_value = 0.35
    rock_bump.inputs["Distance"].default_value = 0.08
    links.new(rock_wave.outputs["Factor"], rock_bump.inputs["Height"])

    bump_001 = nodes.get("Bump.001")
    bump_002 = nodes.get("Bump.002")
    if bump_001 and bump_002:
        links.new(bump_001.outputs["Normal"], rock_bump.inputs["Normal"])
        links.new(rock_bump.outputs["Normal"], bump_002.inputs["Normal"])

    # 4f. Slope Roughness Modulation
    sand_rough = nodes.get("Map Range")
    rough_mix = nodes.get("Slope_Roughness_Blend") or nodes.new("ShaderNodeMix")
    rough_mix.name = "Slope_Roughness_Blend"
    rough_mix.label = "Slope Roughness Blend"
    rough_mix.data_type = 'FLOAT'
    rough_mix.blend_type = 'MIX'
    rough_mix.location = (250, 0)
    rough_mix.inputs["B"].default_value = 0.52

    links.new(slope_map.outputs["Result"], rough_mix.inputs["Factor"])
    if sand_rough:
        links.new(sand_rough.outputs["Result"], rough_mix.inputs["A"])
    if bsdf:
        links.new(rough_mix.outputs["Result"], bsdf.inputs["Roughness"])

# ----------------------------------------------------------------------
# 5. INVARIANTS CHECK & CLEAN SAVE
# ----------------------------------------------------------------------
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
assert site is not None, "Missing NASA_HiRISE_DTM_Jezero_FieldSite_4m object"
v_cnt = len(site.data.vertices)
coords = array.array('f', [0.0]) * (v_cnt * 3)
site.data.vertices.foreach_get('co', coords)
h = hashlib.sha256(coords.tobytes()).hexdigest()
assert v_cnt == 1132096, f"HiRISE vertex count altered: {v_cnt}"
assert h == "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd", f"HiRISE hash altered: {h}"

imgs = [i for i in bpy.data.images if i.name not in ('Render Result', 'Viewer Node')]
assert len(imgs) == 276, f"Packed images count altered: {len(imgs)}"

# Prevent .blend1 backup generation
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()

# Clean up any .blend1 file that may have been generated
blend1 = bpy.data.filepath + "1"
if os.path.exists(blend1):
    os.remove(blend1)

print("=== BLENDER UPGRADE COMPLETED SUCCESSFULLY ===")
