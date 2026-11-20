"""Prepares single-mesh bottom-grounded GLBs (origin at bottom-center Z=0, exact real-world meters)
in unreal_project/SourceAssets/ via Blender 5.2 so Unreal Engine 5.8 Interchange imports every
NASA spacecraft, Hero Facility, and Jezero Crater Terrain as 1 clean Nanite StaticMesh with zero offset."""
import os
import math
import random
import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "unreal_project", "SourceAssets")
NASA_TEX = os.path.join(ROOT, "data", "jez_hirise_site_nasa_color_1m.png")


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / max(1e-5, e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def sample_jezero_z(x, y):
    r = math.hypot(x, y)
    pad = smoothstep(32.0, 96.0, r)
    rim = smoothstep(125.0, 360.0, r)
    dunes = (
        math.sin(x * 0.032 + y * 0.019) * 0.85
        + math.cos(y * 0.044 - x * 0.021) * 0.65
        + math.sin((x + y) * 0.11) * 0.14
        + 1.65
    )
    az = math.atan2(y, x)
    profile = max(0.42, min(1.55, math.sin(az * 3.0 + 0.5) * 0.40 + math.cos(az * 7.0 - 1.1) * 0.22 + 1.05))
    rim_z = rim * 26.0 * profile
    return (dunes + rim_z) * pad


def build_terrain_glb():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    res = 140
    size = 800.0
    half = size * 0.5
    verts, uvs_raw, faces = [], [], []
    for j in range(res + 1):
        y = (j / res) * size - half
        for i in range(res + 1):
            x = (i / res) * size - half
            verts.append((x, y, sample_jezero_z(x, y)))
            # Standard seamless repeating 6x6 UV so normal-mapped Martian sand ripples never show a mirror seam
            uvs_raw.append(((i / res) * 6.0, (j / res) * 6.0))
    stride = res + 1
    for j in range(res):
        for i in range(res):
            a = j * stride + i
            faces.append((a, a + 1, a + stride + 1, a + stride))

    mesh = bpy.data.meshes.new("JezeroTerrainMesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            uv_layer.data[li].uv = uvs_raw[mesh.loops[li].vertex_index]

    obj = bpy.data.objects.new("JezeroCrater_Terrain_800m", mesh)
    bpy.context.scene.collection.objects.link(obj)

    mat = bpy.data.materials.new("M_Jezero_NASA_Regolith")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.72, 0.39, 0.21, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.88
    bsdf.inputs["Metallic"].default_value = 0.02
    if os.path.isfile(NASA_TEX):
        img = bpy.data.images.load(NASA_TEX)
        try:
            img.pack()
        except Exception:
            pass
        tex_node = nt.nodes.new("ShaderNodeTexImage")
        tex_node.image = img
        nt.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])

    # Bake a crisp 512x512 seamless Martian sand-ripple & pebble tangent-space normal map directly into the GLB
    nres = 512
    nimg = bpy.data.images.new("JezeroSandRipple_NRM", width=nres, height=nres, alpha=False)
    nimg.colorspace_settings.name = "Non-Color"
    import numpy as np
    yy, xx = np.mgrid[0:nres, 0:nres].astype(np.float32) / nres * math.tau
    # Multi-frequency wind-carved Martian ripples + fine regolith grit
    h = (
        0.55 * np.sin(xx * 6.0 + np.sin(yy * 2.0) * 1.4)
        + 0.28 * np.sin(xx * 13.0 - yy * 4.0)
        + 0.14 * np.cos(xx * 23.0 + yy * 19.0)
        + 0.08 * np.sin(xx * 47.0 - yy * 41.0)
    )
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 2.4
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 2.4
    dz = np.ones_like(dx)
    inv_len = 1.0 / np.sqrt(dx * dx + dy * dy + dz * dz)
    rgba = np.empty((nres, nres, 4), dtype=np.float32)
    rgba[:, :, 0] = (-dx * inv_len) * 0.5 + 0.5
    rgba[:, :, 1] = (-dy * inv_len) * 0.5 + 0.5
    rgba[:, :, 2] = (dz * inv_len) * 0.5 + 0.5
    rgba[:, :, 3] = 1.0
    nimg.pixels.foreach_set(rgba.ravel())
    nimg.pack()
    n_tex = nt.nodes.new("ShaderNodeTexImage")
    n_tex.image = nimg
    n_map = nt.nodes.new("ShaderNodeNormalMap")
    n_map.inputs["Strength"].default_value = 0.85
    nt.links.new(n_tex.outputs["Color"], n_map.inputs["Color"])
    nt.links.new(n_map.outputs["Normal"], bsdf.inputs["Normal"])
    obj.data.materials.append(mat)

    # Scatter 24 grounded Jezero basalt boulders on the perimeter for authentic geological scale
    rock_mat = bpy.data.materials.new("M_Jezero_BasaltRock")
    rock_mat.use_nodes = True
    rbsdf = rock_mat.node_tree.nodes.get("Principled BSDF")
    rbsdf.inputs["Base Color"].default_value = (0.28, 0.16, 0.11, 1.0)
    rbsdf.inputs["Roughness"].default_value = 0.92
    rng = random.Random(1042)
    rock_objs = [obj]
    for k in range(24):
        ang = (k / 24.0) * math.tau + rng.uniform(-0.14, 0.14)
        rad = rng.uniform(14.5, 42.0)
        rx, ry = math.cos(ang) * rad, math.sin(ang) * rad
        # Keep hero camera corridor clear
        if abs(rx + 2.5) < 7.0 and -24.0 < ry < -6.0:
            continue
        rz = sample_jezero_z(rx, ry)
        sc = rng.uniform(0.35, 1.35)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=sc, location=(rx, ry, rz + sc * 0.22))
        rk = bpy.context.active_object
        rk.scale = (rng.uniform(0.8, 1.4), rng.uniform(0.7, 1.3), rng.uniform(0.45, 0.85))
        rk.rotation_euler = (rng.uniform(0, 6.28), rng.uniform(0, 6.28), rng.uniform(0, 6.28))
        for v in rk.data.vertices:
            v.co *= 1.0 + rng.uniform(-0.18, 0.18)
        rk.data.materials.append(rock_mat)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        rock_objs.append(rk)
    bpy.ops.object.select_all(action="DESELECT")
    for ro in rock_objs:
        ro.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if len(rock_objs) > 1:
        bpy.ops.object.join()

    out = os.path.join(OUT_DIR, "JezeroTerrain_UE5.glb")
    bpy.ops.export_scene.gltf(
        filepath=out, export_format="GLB", export_yup=True,
        export_draco_mesh_compression_enable=False, export_image_format="JPEG", export_apply=True
    )
    print(f"[UE5 ASSET OK] JezeroTerrain_UE5.glb ({os.path.getsize(out):,} B)")


def bake_single_mesh_glb(src_paths, out_name, target_span_m):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for sp in src_paths:
        if os.path.isfile(sp):
            bpy.ops.import_scene.gltf(filepath=sp)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data and o.data.vertices]
    if not meshes:
        return
    # TWO-PASS world-transform bake:
    # Pass 1: Detach ALL meshes with CLEAR_KEEP_TRANSFORM *before* any transform_apply so nested children keep exact poses!
    import bmesh
    for o in meshes:
        if o.parent is not None:
            bpy.ops.object.select_all(action="DESELECT")
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    # Pass 2: Apply transforms cleanly on each independent mesh & fix negative-scale winding
    for o in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        neg_det = o.matrix_world.determinant() < 0
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if neg_det:
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    hero = bpy.context.active_object
    hero.name = os.path.splitext(out_name)[0]

    # Normalize to exact target_span_m with origin at exact bottom-center (X=0, Y=0, Z_min=0)
    pts = [v.co for v in hero.data.vertices]
    min_x = min(p.x for p in pts); max_x = max(p.x for p in pts)
    min_y = min(p.y for p in pts); max_y = max(p.y for p in pts)
    min_z = min(p.z for p in pts); max_z = max(p.z for p in pts)
    cx, cy = 0.5 * (min_x + max_x), 0.5 * (min_y + max_y)
    span = max(0.01, max(max_x - min_x, max_y - min_y, max_z - min_z))
    scl = target_span_m / span
    for v in hero.data.vertices:
        v.co.x = (v.co.x - cx) * scl
        v.co.y = (v.co.y - cy) * scl
        v.co.z = (v.co.z - min_z) * scl
    hero.data.update()

    # Clamp overly dark metallic factors for crisp Lumen reflections
    for mat in bpy.data.materials:
        if mat.use_nodes and mat.node_tree:
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            if bsdf:
                bsdf.inputs["Metallic"].default_value = min(bsdf.inputs["Metallic"].default_value, 0.35)
                bsdf.inputs["Roughness"].default_value = max(bsdf.inputs["Roughness"].default_value, 0.38)

    out = os.path.join(OUT_DIR, out_name)
    bpy.ops.export_scene.gltf(
        filepath=out, export_format="GLB", export_yup=True,
        export_draco_mesh_compression_enable=False, export_image_format="JPEG", export_apply=True
    )
    print(f"[UE5 ASSET OK] {out_name} ({os.path.getsize(out):,} B, span={target_span_m}m)")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    build_terrain_glb()
    fleet = os.path.join(ROOT, "unity_project", "Assets", "Models", "Fleet")
    fac = os.path.join(ROOT, "unity_project", "Assets", "Models", "Facilities")
    sc = os.path.join(ROOT, "unity_project", "Assets", "Models", "Spacecraft")

    bake_single_mesh_glb([os.path.join(fleet, "mars-2020-perseverance-rover.glb")], "Perseverance_UE5.glb", 3.4)
    bake_single_mesh_glb([os.path.join(fleet, "Ingenuity_Mars_Helicopter.glb")], "Ingenuity_UE5.glb", 1.5)
    bake_single_mesh_glb([
        os.path.join(sc, "Habitat Demonstration Unit (part 1).glb"),
        os.path.join(sc, "Habitat Demonstration Unit (part 2).glb"),
    ], "HDU_Habitat_UE5.glb", 9.6)
    bake_single_mesh_glb([os.path.join(fac, "AgriDome_Hero.glb")], "AgriDome_UE5.glb", 11.2)
    bake_single_mesh_glb([os.path.join(fac, "MedBay_Hero.glb")], "MedBay_UE5.glb", 9.4)
    bake_single_mesh_glb([os.path.join(fac, "EDL_Lander_Hero.glb")], "EDL_Lander_UE5.glb", 8.8)
    bake_single_mesh_glb([os.path.join(fac, "LaunchPad_Hero.glb")], "LaunchPad_UE5.glb", 16.0)
    bake_single_mesh_glb([os.path.join(sc, "Astronaut_Rigged.glb")], "Astronaut_UE5.glb", 1.95)
    bake_single_mesh_glb([os.path.join(fleet, "InSight Cruise Lander (panels deployed).glb")], "InSight_UE5.glb", 5.6)


if __name__ == "__main__":
    main()
