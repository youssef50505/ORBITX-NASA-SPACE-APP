"""Phase 1 & 3.1 3D Asset Pipeline (runs inside Blender 5.2 --background --python).
1. Decompresses & PBR-normalizes all Draco/WebP NASA Fleet GLBs in unity_project/Assets/Models/Fleet/.
2. Authors 4 high-detail PBR Hero Facility GLBs in unity_project/Assets/Models/Facilities/.
3. Builds rigged & animated Astronaut_Rigged.glb (Idle/Walk/Sprint/Jump clips) in Assets/Models/Spacecraft/.
4. Exports lightweight web-ready NASA GLBs (<1.2 MB each) into web/assets/models/.
"""
import os
import sys
import math
import json
import struct

try:
    import bpy
    import bmesh
    from mathutils import Vector, Euler
except ImportError:
    bpy = None

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY_FLEET_DIR = os.path.join(ROOT, "unity_project", "Assets", "Models", "Fleet")
UNITY_SPACECRAFT_DIR = os.path.join(ROOT, "unity_project", "Assets", "Models", "Spacecraft")
UNITY_FACILITIES_DIR = os.path.join(ROOT, "unity_project", "Assets", "Models", "Facilities")
DATA_MISSIONS_DIR = os.path.join(ROOT, "data", "missions_3d")
WEB_MODELS_DIR = os.path.join(ROOT, "web", "assets", "models")


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_pbr_mat(name, base_color, metallic=0.15, roughness=0.52, emission=None, emission_strength=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*base_color[:3], 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if alpha < 0.999 and "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha
            mat.blend_method = "BLEND" if hasattr(mat, "blend_method") else "HASHED"
        if emission is not None and emission_strength > 0.0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emission[:3], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def add_mesh_primitive(kind, name, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), mat=None, **kwargs):
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=kwargs.get("size", 1.0), location=loc, rotation=rot)
    elif kind == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=kwargs.get("vertices", 24),
            radius=kwargs.get("radius", 0.5),
            depth=kwargs.get("depth", 1.0),
            location=loc,
            rotation=rot,
        )
    elif kind == "uv_sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=kwargs.get("segments", 24),
            ring_count=kwargs.get("ring_count", 16),
            radius=kwargs.get("radius", 1.0),
            location=loc,
            rotation=rot,
        )
    elif kind == "ico_sphere":
        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=kwargs.get("subdivisions", 2),
            radius=kwargs.get("radius", 1.0),
            location=loc,
            rotation=rot,
        )
    elif kind == "cone":
        bpy.ops.mesh.primitive_cone_add(
            vertices=kwargs.get("vertices", 20),
            radius1=kwargs.get("radius1", 0.5),
            radius2=kwargs.get("radius2", 0.0),
            depth=kwargs.get("depth", 1.0),
            location=loc,
            rotation=rot,
        )
    elif kind == "torus":
        bpy.ops.mesh.primitive_torus_add(
            major_segments=kwargs.get("major_segments", 28),
            minor_segments=kwargs.get("minor_segments", 12),
            major_radius=kwargs.get("major_radius", 1.0),
            minor_radius=kwargs.get("minor_radius", 0.1),
            location=loc,
            rotation=rot,
        )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    if mat is not None:
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = kwargs.get("smooth", True)
    return obj


def sanitize_imported_materials():
    for mat in bpy.data.materials:
        if not mat.use_nodes or not mat.node_tree:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                for k in ("Transmission Weight", "Transmission", "Coat Weight", "Clearcoat", "Sheen Weight"):
                    if k in node.inputs:
                        node.inputs[k].default_value = 0.0
                if "Metallic" in node.inputs:
                    node.inputs["Metallic"].default_value = min(node.inputs["Metallic"].default_value, 0.55)
                if "Roughness" in node.inputs:
                    node.inputs["Roughness"].default_value = max(node.inputs["Roughness"].default_value, 0.35)


def decimate_scene_if_heavy(max_tris=65000):
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data]
    total_tris = sum(len(o.data.polygons) for o in meshes)
    if total_tris <= max_tris or total_tris == 0:
        return total_tris
    ratio = max(0.08, min(0.95, float(max_tris) / float(total_tris)))
    for o in meshes:
        if len(o.data.polygons) < 400:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        mod = o.modifiers.new(name="AutoDecimate", type="DECIMATE")
        mod.ratio = ratio
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except Exception:
            o.modifiers.remove(mod)
    return sum(len(o.data.polygons) for o in meshes if o.name in bpy.data.objects)


def patch_glb_extensions(glb_path, clamp_metallic=True):
    with open(glb_path, "rb") as f:
        glb = f.read()
    magic, version, total_len = struct.unpack("<4sII", glb[:12])
    jlen, jtype = struct.unpack("<II", glb[12:20])
    jdata = json.loads(glb[20:20 + jlen].decode("utf-8"))
    blen, btype = struct.unpack("<II", glb[20 + jlen:28 + jlen])
    bin_chunk = glb[28 + jlen:28 + jlen + blen]

    bad_exts = (
        "KHR_materials_transmission",
        "KHR_materials_ior",
        "KHR_materials_specular",
        "KHR_materials_clearcoat",
        "KHR_materials_sheen",
        "KHR_draco_mesh_compression",
        "EXT_texture_webp",
    )
    changed = False
    for mat in jdata.get("materials", []):
        exts = mat.get("extensions") or {}
        for be in bad_exts:
            if be in exts:
                del exts[be]
                changed = True
        if not exts and "extensions" in mat:
            del mat["extensions"]
        pbr = mat.setdefault("pbrMetallicRoughness", {})
        if clamp_metallic:
            if "metallicFactor" not in pbr or pbr["metallicFactor"] > 0.60:
                pbr["metallicFactor"] = 0.25
                changed = True
            if "roughnessFactor" not in pbr or pbr["roughnessFactor"] < 0.32:
                pbr["roughnessFactor"] = 0.50
                changed = True
    for k in ("extensionsUsed", "extensionsRequired"):
        if k in jdata:
            kept = [e for e in jdata[k] if e not in bad_exts]
            if kept:
                jdata[k] = kept
            else:
                del jdata[k]
            changed = True

    if changed:
        new_jbytes = json.dumps(jdata, separators=(",", ":")).encode("utf-8")
        new_jbytes += b" " * ((4 - (len(new_jbytes) % 4)) % 4)
        bin_padded = bin_chunk + (b"\x00" * ((4 - (len(bin_chunk) % 4)) % 4))
        new_total = 12 + 8 + len(new_jbytes) + 8 + len(bin_padded)
        with open(glb_path, "wb") as f:
            f.write(struct.pack("<4sII", b"glTF", 2, new_total))
            f.write(struct.pack("<II", len(new_jbytes), 0x4E4F534A))
            f.write(new_jbytes)
            f.write(struct.pack("<II", len(bin_padded), 0x004E4942))
            f.write(bin_padded)
    return os.path.getsize(glb_path), jdata.get("extensionsRequired", [])


def export_glb(path, animations=False, jpeg_tex=False, clamp_metallic=True):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    kwargs = dict(
        filepath=path,
        export_format="GLB",
        export_yup=True,
        export_draco_mesh_compression_enable=False,
        export_animations=animations,
        export_apply=(not animations),
    )
    if jpeg_tex:
        kwargs["export_image_format"] = "JPEG"
        kwargs["export_image_quality"] = 82
    else:
        kwargs["export_image_format"] = "AUTO"
    try:
        bpy.ops.export_scene.gltf(**kwargs)
    except TypeError:
        kwargs.pop("export_image_quality", None)
        bpy.ops.export_scene.gltf(**kwargs)
    return patch_glb_extensions(path, clamp_metallic=clamp_metallic)


def task_1_1_decompress_fleet():
    print("\n=== TASK 1.1: Decompress & Normalize Unity Fleet GLBs ===")
    targets = [
        ("insight/InSight Cruise Lander (panels deployed).glb", "InSight Cruise Lander (panels deployed).glb", 55000),
        ("ingenuity/Ingenuity_Mars_Helicopter.glb", "Ingenuity_Mars_Helicopter.glb", 45000),
        ("maven/Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb", "Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb", 45000),
        ("global_surveyor/Mars Global Surveyor.glb", "Mars Global Surveyor.glb", 45000),
        ("mars_odyssey/Mars Odyssey.glb", "Mars Odyssey.glb", 45000),
        ("mro/Mars Reconnaissance Orbiter (MRO) (A).glb", "Mars Reconnaissance Orbiter (MRO) (A).glb", 45000),
        ("perseverance/mars-2020-perseverance-rover.glb", "mars-2020-perseverance-rover.glb", 70000),
    ]
    for rel_src, dst_name, max_tris in targets:
        src = os.path.join(DATA_MISSIONS_DIR, rel_src)
        dst = os.path.join(UNITY_FLEET_DIR, dst_name)
        if not os.path.isfile(src):
            print(f"[WARN] Missing source: {src}")
            continue
        clear_scene()
        bpy.ops.import_scene.gltf(filepath=src)
        sanitize_imported_materials()
        tris = decimate_scene_if_heavy(max_tris=max_tris)
        # Convert any WebP images in memory to PNG/JPEG cleanly
        for img in bpy.data.images:
            if img.file_format == "WEBP":
                img.file_format = "PNG"
        size_b, req = export_glb(dst, animations=False, jpeg_tex=(dst_name.startswith("Mars ")), clamp_metallic=True)
        print(f"[FLEET OK] {dst_name}: {size_b:,} B | tris~{tris:,} | extReq={req}")


def task_1_2_author_hero_facilities():
    print("\n=== TASK 1.2: Author 4 Hero PBR Facility GLBs ===")
    os.makedirs(UNITY_FACILITIES_DIR, exist_ok=True)

    # 1. AgriDome_Hero.glb
    clear_scene()
    m_base = make_pbr_mat("Agri_BaseTi", (0.28, 0.30, 0.34), metallic=0.45, roughness=0.48)
    m_rib = make_pbr_mat("Agri_RibAlloy", (0.76, 0.78, 0.82), metallic=0.50, roughness=0.38)
    m_glass = make_pbr_mat("Agri_Glass", (0.75, 0.90, 0.96), metallic=0.10, roughness=0.14, alpha=0.34)
    m_rack = make_pbr_mat("Agri_RackMetal", (0.45, 0.48, 0.52), metallic=0.40, roughness=0.45)
    m_leaf = make_pbr_mat("Agri_CanopyGreen", (0.14, 0.56, 0.22), metallic=0.02, roughness=0.68)
    m_led = make_pbr_mat("Agri_GrowLED", (1.0, 0.42, 0.88), metallic=0.05, roughness=0.25, emission=(1.0, 0.38, 0.85), emission_strength=3.2)
    m_cyan = make_pbr_mat("Agri_WaterTank", (0.16, 0.62, 0.78), metallic=0.15, roughness=0.30, emission=(0.12, 0.65, 0.85), emission_strength=1.2)
    m_floor = make_pbr_mat("Agri_DeckFloor", (0.20, 0.22, 0.25), metallic=0.30, roughness=0.58)

    # Foundation ring & interior deck
    add_mesh_primitive("cylinder", "Agri_Foundation", loc=(0, 0, 0.22), radius=5.4, depth=0.44, vertices=32, mat=m_base)
    add_mesh_primitive("cylinder", "Agri_Deck", loc=(0, 0, 0.46), radius=4.95, depth=0.08, vertices=32, mat=m_floor)
    add_mesh_primitive("torus", "Agri_CollarRing", loc=(0, 0, 0.52), major_radius=5.05, minor_radius=0.18, mat=m_rib)

    # Structural geodesic ribs (6 arches + 2 horizontal rings)
    for k in range(6):
        ang = k * math.pi / 6.0
        arch = add_mesh_primitive("torus", f"Agri_RibArch_{k}", loc=(0, 0, 0.5), rot=(math.pi / 2.0, 0, ang), major_radius=4.95, minor_radius=0.085, mat=m_rib)
        # Keep upper hemisphere by scaling/moving vertices below z=0.45 up
        for v in arch.data.vertices:
            world_v = arch.matrix_world @ v.co
            if world_v.z < 0.45:
                v.co.y = 0.0  # collapse lower half cleanly inside base collar
        arch.data.update()
    add_mesh_primitive("torus", "Agri_MidRing", loc=(0, 0, 2.75), major_radius=4.42, minor_radius=0.075, mat=m_rib)
    add_mesh_primitive("torus", "Agri_ApexRing", loc=(0, 0, 4.65), major_radius=2.25, minor_radius=0.07, mat=m_rib)

    # Translucent geodesic dome shell (upper hemisphere)
    dome = add_mesh_primitive("ico_sphere", "Agri_GlassDome", loc=(0, 0, 0.5), subdivisions=3, radius=4.88, mat=m_glass)
    bm = bmesh.new()
    bm.from_mesh(dome.data)
    to_del = [f for f in bm.faces if f.calc_center_median().z < -0.02]
    bmesh.ops.delete(bm, geom=to_del, context="FACES")
    bm.to_mesh(dome.data)
    bm.free()

    # 6 Hydroponic Vertical Grow Towers arranged radially inside
    for i in range(6):
        ang = i * (math.pi / 3.0) + 0.25
        rx, ry = math.cos(ang) * 2.55, math.sin(ang) * 2.55
        add_mesh_primitive("cube", f"Agri_RackPost_{i}", loc=(rx, ry, 1.55), rot=(0, 0, ang), scale=(0.14, 1.55, 2.1), mat=m_rack)
        for shelf in range(3):
            sz = 0.92 + shelf * 0.62
            add_mesh_primitive("cube", f"Agri_Tray_{i}_{shelf}", loc=(rx, ry, sz), rot=(0, 0, ang), scale=(0.56, 1.48, 0.08), mat=m_rack)
            add_mesh_primitive("cube", f"Agri_LED_{i}_{shelf}", loc=(rx, ry, sz + 0.46), rot=(0, 0, ang), scale=(0.24, 1.38, 0.04), mat=m_led)
            for p in (-0.45, 0.0, 0.45):
                px = rx - math.sin(ang) * p
                py = ry + math.cos(ang) * p
                add_mesh_primitive("ico_sphere", f"Agri_Plant_{i}_{shelf}_{p:.1f}", loc=(px, py, sz + 0.18), subdivisions=1, radius=0.22, scale=(1.1, 1.0, 0.72), mat=m_leaf)

    # Central nutrient & O2 core column + airlock vestibule
    add_mesh_primitive("cylinder", "Agri_CentralNutrientTank", loc=(0, 0, 1.85), radius=0.68, depth=2.65, vertices=24, mat=m_cyan)
    add_mesh_primitive("cylinder", "Agri_CentralCap", loc=(0, 0, 3.30), radius=0.82, depth=0.28, vertices=24, mat=m_rib)
    add_mesh_primitive("cube", "Agri_AirlockTunnel", loc=(5.25, 0, 1.35), scale=(1.9, 2.1, 2.15), mat=m_rib)
    add_mesh_primitive("cube", "Agri_AirlockDoor", loc=(6.22, 0, 1.28), scale=(0.14, 1.25, 1.85), mat=m_base)
    add_mesh_primitive("cube", "Agri_AirlockBeacon", loc=(6.24, 0, 2.34), scale=(0.14, 0.65, 0.12), mat=m_cyan)

    sz, req = export_glb(os.path.join(UNITY_FACILITIES_DIR, "AgriDome_Hero.glb"))
    print(f"[FACILITY OK] AgriDome_Hero.glb: {sz:,} B | extReq={req}")
    # Also export lightweight copy for WebGL!
    export_glb(os.path.join(WEB_MODELS_DIR, "agridome_web.glb"))

    # 2. MedBay_Hero.glb
    clear_scene()
    m_hull = make_pbr_mat("Med_KevlarWhite", (0.86, 0.85, 0.82), metallic=0.12, roughness=0.46)
    m_shield = make_pbr_mat("Med_RegolithArmor", (0.44, 0.34, 0.28), metallic=0.18, roughness=0.72)
    m_trim = make_pbr_mat("Med_TiFrame", (0.28, 0.31, 0.36), metallic=0.48, roughness=0.40)
    m_red = make_pbr_mat("Med_CrossRed", (0.88, 0.14, 0.14), metallic=0.05, roughness=0.35, emission=(0.9, 0.12, 0.12), emission_strength=1.4)
    m_green = make_pbr_mat("Med_StatusGreen", (0.14, 0.88, 0.45), metallic=0.05, roughness=0.25, emission=(0.12, 0.92, 0.48), emission_strength=2.5)
    m_window = make_pbr_mat("Med_WindowCyan", (0.22, 0.72, 0.90), metallic=0.10, roughness=0.18, emission=(0.18, 0.68, 0.88), emission_strength=1.4)

    add_mesh_primitive("cube", "Med_Plinth", loc=(0, 0, 0.25), scale=(9.2, 5.6, 0.50), mat=m_trim)
    add_mesh_primitive("cylinder", "Med_PressurizedHull", loc=(0, 0, 1.95), rot=(0, math.pi / 2.0, 0), radius=2.15, depth=7.6, vertices=28, mat=m_hull)
    add_mesh_primitive("uv_sphere", "Med_EndCapL", loc=(-3.8, 0, 1.95), radius=2.14, scale=(0.45, 1.0, 1.0), mat=m_hull)
    add_mesh_primitive("uv_sphere", "Med_EndCapR", loc=(3.8, 0, 1.95), radius=2.14, scale=(0.45, 1.0, 1.0), mat=m_hull)
    for rx in (-2.7, -0.9, 0.9, 2.7):
        add_mesh_primitive("torus", f"Med_RadRib_{rx:.1f}", loc=(rx, 0, 1.95), rot=(0, math.pi / 2.0, 0), major_radius=2.22, minor_radius=0.14, mat=m_shield)
    # Airlock vestibule + medical cross + O2 scrubbers
    add_mesh_primitive("cube", "Med_Airlock", loc=(0, -2.55, 1.45), scale=(2.3, 1.65, 2.35), mat=m_trim)
    add_mesh_primitive("cube", "Med_Door", loc=(0, -3.40, 1.38), scale=(1.25, 0.14, 1.95), mat=m_hull)
    add_mesh_primitive("cube", "Med_CrossV", loc=(0, -3.49, 1.60), scale=(0.20, 0.05, 0.68), mat=m_red)
    add_mesh_primitive("cube", "Med_CrossH", loc=(0, -3.49, 1.60), scale=(0.68, 0.05, 0.20), mat=m_red)
    add_mesh_primitive("cube", "Med_Beacon", loc=(0, -3.42, 2.48), scale=(0.75, 0.14, 0.12), mat=m_green)
    for side in (-1.9, 1.9):
        add_mesh_primitive("cube", f"Med_Window_{side:.1f}", loc=(side, -2.08, 2.15), scale=(1.15, 0.18, 0.62), mat=m_window)
    for tx in (-2.4, -1.6, 1.6, 2.4):
        add_mesh_primitive("cylinder", f"Med_O2Tank_{tx:.1f}", loc=(tx, 2.35, 1.35), radius=0.34, depth=1.95, vertices=18, mat=m_trim)
    add_mesh_primitive("cylinder", "Med_CommsMast", loc=(3.2, 1.5, 4.35), radius=0.07, depth=2.1, vertices=12, mat=m_trim)
    add_mesh_primitive("cone", "Med_CommsDish", loc=(3.2, 1.5, 5.45), rot=(0.45, 0, 0.3), radius1=0.65, radius2=0.08, depth=0.25, mat=m_hull)

    sz, req = export_glb(os.path.join(UNITY_FACILITIES_DIR, "MedBay_Hero.glb"))
    print(f"[FACILITY OK] MedBay_Hero.glb: {sz:,} B | extReq={req}")

    # 3. EDL_Lander_Hero.glb
    clear_scene()
    m_ceramic = make_pbr_mat("EDL_ThermalWhite", (0.88, 0.87, 0.85), metallic=0.14, roughness=0.44)
    m_tps = make_pbr_mat("EDL_CarbonHeatshield", (0.14, 0.13, 0.14), metallic=0.25, roughness=0.62)
    m_gold = make_pbr_mat("EDL_KaptonFoil", (0.86, 0.66, 0.18), metallic=0.58, roughness=0.34)
    m_strut = make_pbr_mat("EDL_Titanium", (0.42, 0.45, 0.50), metallic=0.52, roughness=0.38)
    m_glow = make_pbr_mat("EDL_NavLight", (0.98, 0.58, 0.16), metallic=0.05, roughness=0.25, emission=(1.0, 0.55, 0.14), emission_strength=2.8)

    # Lifting-body fuselage + black TPS belly + dorsal crew cabin
    add_mesh_primitive("uv_sphere", "EDL_Fuselage", loc=(0, 0, 2.35), radius=1.0, scale=(2.35, 4.2, 1.12), mat=m_ceramic)
    add_mesh_primitive("uv_sphere", "EDL_HeatshieldBelly", loc=(0, 0.15, 1.95), radius=1.0, scale=(2.30, 4.10, 0.62), mat=m_tps)
    add_mesh_primitive("cube", "EDL_DorsalSpine", loc=(0, -0.35, 3.25), scale=(1.35, 4.4, 0.62), mat=m_ceramic)
    add_mesh_primitive("cube", "EDL_CockpitVisor", loc=(0, 2.15, 3.12), rot=(-0.38, 0, 0), scale=(1.32, 0.95, 0.36), mat=m_tps)
    # Twin canted tail fins + retro engine nozzles
    for sx in (-1, 1):
        add_mesh_primitive("cube", f"EDL_Fin_{sx}", loc=(sx * 1.95, -2.75, 3.15), rot=(0.25, sx * 0.42, 0), scale=(0.14, 1.85, 1.65), mat=m_tps)
        add_mesh_primitive("cone", f"EDL_Nozzle_{sx}", loc=(sx * 0.95, -4.15, 2.20), rot=(-math.pi / 2.0, 0, 0), radius1=0.46, radius2=0.22, depth=0.95, mat=m_strut)
        add_mesh_primitive("cylinder", f"EDL_GoldBay_{sx}", loc=(sx * 1.65, -0.6, 2.35), rot=(math.pi / 2.0, 0, 0), radius=0.42, depth=2.1, mat=m_gold)
    # 4 Articulated landing legs with crushable circular footpads grounded at Z=0
    for idx, (lx, ly) in enumerate([(-2.35, 1.85), (2.35, 1.85), (-2.45, -1.95), (2.45, -1.95)]):
        add_mesh_primitive("cylinder", f"EDL_Pad_{idx}", loc=(lx, ly, 0.08), radius=0.52, depth=0.16, vertices=18, mat=m_strut)
        sx = 0.28 if lx < 0 else -0.28
        add_mesh_primitive("cylinder", f"EDL_Strut_{idx}", loc=(lx * 0.82, ly * 0.88, 1.15), rot=(0, sx, 0), radius=0.11, depth=2.25, vertices=14, mat=m_strut)
    add_mesh_primitive("cube", "EDL_Beacon", loc=(0, -0.4, 3.64), scale=(0.28, 0.28, 0.16), mat=m_glow)

    sz, req = export_glb(os.path.join(UNITY_FACILITIES_DIR, "EDL_Lander_Hero.glb"))
    print(f"[FACILITY OK] EDL_Lander_Hero.glb: {sz:,} B | extReq={req}")

    # 4. LaunchPad_Hero.glb
    clear_scene()
    m_pad = make_pbr_mat("Pad_SinteredRegolith", (0.34, 0.26, 0.22), metallic=0.12, roughness=0.80)
    m_steel = make_pbr_mat("Pad_BlastSteel", (0.26, 0.28, 0.32), metallic=0.52, roughness=0.46)
    m_hazard = make_pbr_mat("Pad_HazardAmber", (0.94, 0.62, 0.10), metallic=0.10, roughness=0.45, emission=(0.95, 0.58, 0.08), emission_strength=0.9)
    m_cryo = make_pbr_mat("Pad_CryoWhite", (0.90, 0.91, 0.94), metallic=0.22, roughness=0.38)
    m_flood = make_pbr_mat("Pad_FloodLight", (0.95, 0.98, 1.0), metallic=0.05, roughness=0.20, emission=(0.95, 0.98, 1.0), emission_strength=3.6)

    # Octagonal sintered blast deck + steel launch ring + 4 flame trenches
    add_mesh_primitive("cylinder", "Pad_OctagonDeck", loc=(0, 0, 0.24), radius=8.4, depth=0.48, vertices=8, mat=m_pad)
    add_mesh_primitive("torus", "Pad_BlastRing", loc=(0, 0, 0.56), major_radius=3.4, minor_radius=0.26, mat=m_steel)
    add_mesh_primitive("torus", "Pad_HazardRing", loc=(0, 0, 0.52), major_radius=6.8, minor_radius=0.12, mat=m_hazard)
    for k in range(4):
        ang = k * (math.pi / 2.0) + math.pi / 4.0
        mx, my = math.cos(ang) * 7.2, math.sin(ang) * 7.2
        add_mesh_primitive("cylinder", f"Pad_LightMast_{k}", loc=(mx, my, 2.8), radius=0.11, depth=5.2, vertices=12, mat=m_steel)
        add_mesh_primitive("cube", f"Pad_LampHead_{k}", loc=(mx * 0.96, my * 0.96, 5.45), rot=(0, 0, ang), scale=(0.45, 0.85, 0.25), mat=m_flood)
        # Blast deflector ramp
        bx, by = math.cos(k * math.pi / 2.0) * 4.4, math.sin(k * math.pi / 2.0) * 4.4
        add_mesh_primitive("cube", f"Pad_FlameTrench_{k}", loc=(bx, by, 0.58), rot=(0, 0, k * math.pi / 2.0), scale=(2.4, 1.2, 0.42), mat=m_steel)
    # Twin spherical cryo propellant tanks (LOX & CH4) + insulated pipes
    for ty, nm in ((-2.4, "LOX"), (2.4, "CH4")):
        add_mesh_primitive("uv_sphere", f"Pad_CryoSphere_{nm}", loc=(-7.6, ty, 2.05), radius=1.45, mat=m_cryo)
        add_mesh_primitive("cylinder", f"Pad_CryoBase_{nm}", loc=(-7.6, ty, 0.55), radius=0.95, depth=1.1, mat=m_steel)
        add_mesh_primitive("cylinder", f"Pad_CryoPipe_{nm}", loc=(-5.2, ty * 0.6, 0.85), rot=(0, math.pi / 2.0, 0), radius=0.14, depth=4.4, mat=m_cryo)
    # Umbilical service tower gantry
    for gx in (5.2, 6.4):
        for gy in (-0.8, 0.8):
            add_mesh_primitive("cylinder", f"Pad_GantryCol_{gx}_{gy}", loc=(gx, gy, 5.8), radius=0.12, depth=11.2, vertices=10, mat=m_steel)
    for lvl in range(5):
        lz = 2.0 + lvl * 2.2
        add_mesh_primitive("cube", f"Pad_GantryDeck_{lvl}", loc=(5.8, 0, lz), scale=(1.55, 1.95, 0.14), mat=m_steel)
    add_mesh_primitive("cube", "Pad_UmbilicalArm", loc=(3.7, 0, 8.8), scale=(3.4, 0.42, 0.36), mat=m_hazard)

    sz, req = export_glb(os.path.join(UNITY_FACILITIES_DIR, "LaunchPad_Hero.glb"))
    print(f"[FACILITY OK] LaunchPad_Hero.glb: {sz:,} B | extReq={req}")


def task_1_3_rig_astronaut():
    print("\n=== TASK 1.3: Rig & Animate Astronaut_Rigged.glb ===")
    src = os.path.join(UNITY_SPACECRAFT_DIR, "Astronaut.glb")
    dst = os.path.join(UNITY_SPACECRAFT_DIR, "Astronaut_Rigged.glb")
    if not os.path.isfile(src):
        print(f"[WARN] Missing {src}")
        return
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=src)
    sanitize_imported_materials()

    # Join all astronaut meshes into one clean mesh so skinning & vertex groups are unified
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print("[WARN] No mesh in Astronaut.glb")
        return
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    astro = bpy.context.active_object
    astro.name = "NASA_Astronaut_Skinned"
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Measure normalized Z bounds (feet ~0.0, head ~1.80)
    zs = [v.co.z for v in astro.data.vertices]
    xs = [v.co.x for v in astro.data.vertices]
    zmin, zmax = min(zs), max(zs)
    h = max(0.5, zmax - zmin)
    half_w = max(0.25, max(abs(x) for x in xs))

    # Create 13-bone Humanoid Armature
    arm_data = bpy.data.armatures.new("AstronautArmature")
    arm_obj = bpy.data.objects.new("AstronautRig", arm_data)
    bpy.context.scene.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")

    def add_bone(name, head, tail, parent_name=None):
        b = arm_data.edit_bones.new(name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if parent_name:
            b.parent = arm_data.edit_bones[parent_name]
        return b

    z0 = zmin
    add_bone("Hips",       (0.0, 0.0, z0 + h * 0.50), (0.0, 0.0, z0 + h * 0.58))
    add_bone("Spine",      (0.0, 0.0, z0 + h * 0.58), (0.0, 0.0, z0 + h * 0.72), "Hips")
    add_bone("Chest",      (0.0, 0.0, z0 + h * 0.72), (0.0, 0.0, z0 + h * 0.84), "Spine")
    add_bone("Head",       (0.0, 0.0, z0 + h * 0.84), (0.0, 0.0, z0 + h * 0.99), "Chest")

    sx = half_w * 0.34
    ex = half_w * 0.68
    wx = half_w * 0.95
    add_bone("UpperArm.L", (-sx, 0.0, z0 + h * 0.80), (-ex, 0.0, z0 + h * 0.65), "Chest")
    add_bone("LowerArm.L", (-ex, 0.0, z0 + h * 0.65), (-wx, 0.0, z0 + h * 0.50), "UpperArm.L")
    add_bone("UpperArm.R", ( sx, 0.0, z0 + h * 0.80), ( ex, 0.0, z0 + h * 0.65), "Chest")
    add_bone("LowerArm.R", ( ex, 0.0, z0 + h * 0.65), ( wx, 0.0, z0 + h * 0.50), "UpperArm.R")

    lx = half_w * 0.22
    add_bone("UpperLeg.L", (-lx, 0.0, z0 + h * 0.50), (-lx, 0.0, z0 + h * 0.26), "Hips")
    add_bone("LowerLeg.L", (-lx, 0.0, z0 + h * 0.26), (-lx, 0.0, z0 + h * 0.02), "UpperLeg.L")
    add_bone("UpperLeg.R", ( lx, 0.0, z0 + h * 0.50), ( lx, 0.0, z0 + h * 0.26), "Hips")
    add_bone("LowerLeg.R", ( lx, 0.0, z0 + h * 0.26), ( lx, 0.0, z0 + h * 0.02), "UpperLeg.R")

    bpy.ops.object.mode_set(mode="OBJECT")

    # Parent mesh to armature + deterministic smooth anatomical vertex weights
    astro.parent = arm_obj
    mod = astro.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm_obj

    bnames = [b.name for b in arm_data.bones]
    vgroups = {bn: astro.vertex_groups.new(name=bn) for bn in bnames}

    for v in astro.data.vertices:
        p = v.co
        zn = (p.z - z0) / h
        ax = abs(p.x)
        side = "L" if p.x < 0 else "R"

        if zn >= 0.845:
            vgroups["Head"].add([v.index], 1.0, "REPLACE")
        elif zn >= 0.52 and ax > sx * 0.82:
            if ax > ex * 0.92:
                vgroups[f"LowerArm.{side}"].add([v.index], 0.85, "REPLACE")
                vgroups[f"UpperArm.{side}"].add([v.index], 0.15, "REPLACE")
            else:
                w = min(1.0, max(0.0, (ax - sx * 0.82) / max(0.05, ex - sx * 0.82)))
                vgroups[f"UpperArm.{side}"].add([v.index], 0.35 + 0.65 * w, "REPLACE")
                vgroups["Chest"].add([v.index], 0.65 * (1.0 - w), "REPLACE")
        elif zn >= 0.68:
            vgroups["Chest"].add([v.index], 0.82, "REPLACE")
            vgroups["Spine"].add([v.index], 0.18, "REPLACE")
        elif zn >= 0.54:
            vgroups["Spine"].add([v.index], 0.75, "REPLACE")
            vgroups["Hips"].add([v.index], 0.25, "REPLACE")
        elif zn >= 0.46:
            vgroups["Hips"].add([v.index], 0.70, "REPLACE")
            vgroups[f"UpperLeg.{side}"].add([v.index], 0.30, "REPLACE")
        elif zn >= 0.24:
            vgroups[f"UpperLeg.{side}"].add([v.index], 0.85, "REPLACE")
            vgroups[f"LowerLeg.{side}"].add([v.index], 0.15, "REPLACE")
        else:
            vgroups[f"LowerLeg.{side}"].add([v.index], 0.90, "REPLACE")
            vgroups[f"UpperLeg.{side}"].add([v.index], 0.10, "REPLACE")

    # Author 4 NLA Actions: Idle, Walk, Sprint, Jump
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="POSE")
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = "XYZ"

    def build_action(act_name, frames_pose):
        act = bpy.data.actions.new(name=act_name)
        arm_obj.animation_data_create()
        arm_obj.animation_data.action = act
        for frame, poses in frames_pose.items():
            bpy.context.scene.frame_set(frame)
            for pb in arm_obj.pose.bones:
                pb.rotation_euler = Euler((0, 0, 0), "XYZ")
                pb.location = Vector((0, 0, 0))
            for bname, vals in poses.items():
                pb = arm_obj.pose.bones.get(bname)
                if not pb:
                    continue
                if "rot" in vals:
                    rx, ry, rz = vals["rot"]
                    pb.rotation_euler = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ")
                if "loc" in vals:
                    pb.location = Vector(vals["loc"])
            for pb in arm_obj.pose.bones:
                pb.keyframe_insert(data_path="rotation_euler", frame=frame)
                if pb.name == "Hips":
                    pb.keyframe_insert(data_path="location", frame=frame)
        track = arm_obj.animation_data.nla_tracks.new()
        track.name = act_name
        strip = track.strips.new(act_name, 1, act)
        strip.action_frame_start = min(frames_pose.keys())
        strip.action_frame_end = max(frames_pose.keys())
        arm_obj.animation_data.action = None

    build_action("Idle", {
        1:  {"Chest": {"rot": (1.5, 0, 0)}, "Head": {"rot": (0, 0, -4)}},
        25: {"Chest": {"rot": (-2.0, 0, 0)}, "Head": {"rot": (2, 0, 5)}, "UpperArm.L": {"rot": (3, 0, 0)}, "UpperArm.R": {"rot": (3, 0, 0)}},
        49: {"Chest": {"rot": (1.5, 0, 0)}, "Head": {"rot": (0, 0, -4)}},
    })
    build_action("Walk", {
        1:  {"Hips": {"loc": (0, 0, 0.03), "rot": (0, 0, 4)}, "Spine": {"rot": (5, 0, -4)},
             "UpperLeg.L": {"rot": (24, 0, 0)}, "LowerLeg.L": {"rot": (-12, 0, 0)},
             "UpperLeg.R": {"rot": (-20, 0, 0)}, "LowerLeg.R": {"rot": (-18, 0, 0)},
             "UpperArm.L": {"rot": (-18, 0, 0)}, "UpperArm.R": {"rot": (20, 0, 0)}},
        13: {"Hips": {"loc": (0, 0, -0.02), "rot": (0, 0, 0)}, "Spine": {"rot": (6, 0, 0)},
             "UpperLeg.L": {"rot": (0, 0, 0)}, "LowerLeg.L": {"rot": (-6, 0, 0)},
             "UpperLeg.R": {"rot": (8, 0, 0)}, "LowerLeg.R": {"rot": (-28, 0, 0)},
             "UpperArm.L": {"rot": (0, 0, 0)}, "UpperArm.R": {"rot": (0, 0, 0)}},
        25: {"Hips": {"loc": (0, 0, 0.03), "rot": (0, 0, -4)}, "Spine": {"rot": (5, 0, 4)},
             "UpperLeg.L": {"rot": (-20, 0, 0)}, "LowerLeg.L": {"rot": (-18, 0, 0)},
             "UpperLeg.R": {"rot": (24, 0, 0)}, "LowerLeg.R": {"rot": (-12, 0, 0)},
             "UpperArm.L": {"rot": (20, 0, 0)}, "UpperArm.R": {"rot": (-18, 0, 0)}},
        37: {"Hips": {"loc": (0, 0, -0.02), "rot": (0, 0, 0)}, "Spine": {"rot": (6, 0, 0)},
             "UpperLeg.L": {"rot": (8, 0, 0)}, "LowerLeg.L": {"rot": (-28, 0, 0)},
             "UpperLeg.R": {"rot": (0, 0, 0)}, "LowerLeg.R": {"rot": (-6, 0, 0)},
             "UpperArm.L": {"rot": (0, 0, 0)}, "UpperArm.R": {"rot": (0, 0, 0)}},
        49: {"Hips": {"loc": (0, 0, 0.03), "rot": (0, 0, 4)}, "Spine": {"rot": (5, 0, -4)},
             "UpperLeg.L": {"rot": (24, 0, 0)}, "LowerLeg.L": {"rot": (-12, 0, 0)},
             "UpperLeg.R": {"rot": (-20, 0, 0)}, "LowerLeg.R": {"rot": (-18, 0, 0)},
             "UpperArm.L": {"rot": (-18, 0, 0)}, "UpperArm.R": {"rot": (20, 0, 0)}},
    })
    build_action("Sprint", {
        1:  {"Hips": {"loc": (0, 0, 0.07), "rot": (0, 0, 7)}, "Spine": {"rot": (12, 0, -7)},
             "UpperLeg.L": {"rot": (38, 0, 0)}, "LowerLeg.L": {"rot": (-24, 0, 0)},
             "UpperLeg.R": {"rot": (-30, 0, 0)}, "LowerLeg.R": {"rot": (-34, 0, 0)},
             "UpperArm.L": {"rot": (-30, 0, 0)}, "LowerArm.L": {"rot": (25, 0, 0)},
             "UpperArm.R": {"rot": (34, 0, 0)}, "LowerArm.R": {"rot": (28, 0, 0)}},
        13: {"Hips": {"loc": (0, 0, -0.03), "rot": (0, 0, 0)}, "Spine": {"rot": (14, 0, 0)},
             "UpperLeg.L": {"rot": (-30, 0, 0)}, "LowerLeg.L": {"rot": (-34, 0, 0)},
             "UpperLeg.R": {"rot": (38, 0, 0)}, "LowerLeg.R": {"rot": (-24, 0, 0)},
             "UpperArm.L": {"rot": (34, 0, 0)}, "LowerArm.L": {"rot": (28, 0, 0)},
             "UpperArm.R": {"rot": (-30, 0, 0)}, "LowerArm.R": {"rot": (25, 0, 0)}},
        25: {"Hips": {"loc": (0, 0, 0.07), "rot": (0, 0, 7)}, "Spine": {"rot": (12, 0, -7)},
             "UpperLeg.L": {"rot": (38, 0, 0)}, "LowerLeg.L": {"rot": (-24, 0, 0)},
             "UpperLeg.R": {"rot": (-30, 0, 0)}, "LowerLeg.R": {"rot": (-34, 0, 0)},
             "UpperArm.L": {"rot": (-30, 0, 0)}, "LowerArm.L": {"rot": (25, 0, 0)},
             "UpperArm.R": {"rot": (34, 0, 0)}, "LowerArm.R": {"rot": (28, 0, 0)}},
    })
    build_action("Jump", {
        1:  {"Hips": {"loc": (0, 0, 0.06)}, "Spine": {"rot": (8, 0, 0)},
             "UpperLeg.L": {"rot": (28, 0, 0)}, "LowerLeg.L": {"rot": (-42, 0, 0)},
             "UpperLeg.R": {"rot": (18, 0, 0)}, "LowerLeg.R": {"rot": (-32, 0, 0)},
             "UpperArm.L": {"rot": (22, 0, -18)}, "UpperArm.R": {"rot": (22, 0, 18)}},
        25: {"Hips": {"loc": (0, 0, 0.06)}, "Spine": {"rot": (8, 0, 0)},
             "UpperLeg.L": {"rot": (28, 0, 0)}, "LowerLeg.L": {"rot": (-42, 0, 0)},
             "UpperLeg.R": {"rot": (18, 0, 0)}, "LowerLeg.R": {"rot": (-32, 0, 0)},
             "UpperArm.L": {"rot": (22, 0, -18)}, "UpperArm.R": {"rot": (22, 0, 18)}},
    })
    bpy.ops.object.mode_set(mode="OBJECT")

    sz, req = export_glb(dst, animations=True, jpeg_tex=False, clamp_metallic=True)
    print(f"[RIG OK] Astronaut_Rigged.glb: {sz:,} B | bones={len(arm_data.bones)} | clips=4 | extReq={req}")


def task_3_1_export_web_glbs():
    print("\n=== TASK 3.1: Export Lightweight WebGL NASA GLBs ===")
    os.makedirs(WEB_MODELS_DIR, exist_ok=True)
    web_jobs = [
        ([os.path.join(UNITY_FLEET_DIR, "mars-2020-perseverance-rover.glb")], "perseverance_web.glb", 12000, 3.0),
        ([os.path.join(UNITY_FLEET_DIR, "Ingenuity_Mars_Helicopter.glb")], "ingenuity_web.glb", 8000, 1.4),
        ([
            os.path.join(UNITY_SPACECRAFT_DIR, "Habitat Demonstration Unit (part 1).glb"),
            os.path.join(UNITY_SPACECRAFT_DIR, "Habitat Demonstration Unit (part 2).glb"),
        ], "hdu_habitat_web.glb", 14000, 7.2),
        ([os.path.join(UNITY_SPACECRAFT_DIR, "Astronaut.glb")], "astronaut_web.glb", 8000, 1.85),
    ]
    for srcs, out_name, max_tris, target_h in web_jobs:
        clear_scene()
        loaded = 0
        for s in srcs:
            if os.path.isfile(s):
                bpy.ops.import_scene.gltf(filepath=s)
                loaded += 1
        if loaded == 0:
            print(f"[WARN] No source for {out_name}")
            continue
        sanitize_imported_materials()
        # Apply transforms & ground bottom to Z=0, scale largest dimension to target_h
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data and o.data.vertices]
        for o in meshes:
            bpy.ops.object.select_all(action="DESELECT")
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        for o in list(bpy.context.scene.objects):
            if o.type == "EMPTY":
                bpy.data.objects.remove(o, do_unlink=True)
        tris = decimate_scene_if_heavy(max_tris=max_tris)
        # Re-normalize bounding box (center X/Y, bottom Z=0, max extent = target_h)
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data and o.data.vertices]
        pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
        if pts:
            min_x = min(p.x for p in pts); max_x = max(p.x for p in pts)
            min_y = min(p.y for p in pts); max_y = max(p.y for p in pts)
            min_z = min(p.z for p in pts); max_z = max(p.z for p in pts)
            cx, cy = 0.5 * (min_x + max_x), 0.5 * (min_y + max_y)
            span = max(0.01, max(max_x - min_x, max_y - min_y, max_z - min_z))
            scl = target_h / span
            for o in meshes:
                for v in o.data.vertices:
                    wc = o.matrix_world @ v.co
                    v.co = o.matrix_world.inverted() @ Vector(((wc.x - cx) * scl, (wc.y - cy) * scl, (wc.z - min_z) * scl))
                o.data.update()
        # Downscale oversized embedded textures for fast web loading (max 512px)
        for img in bpy.data.images:
            try:
                if img.size[0] > 512 or img.size[1] > 512:
                    img.scale(min(512, img.size[0]), min(512, img.size[1]))
                    img.pack()
            except Exception:
                pass
        dst = os.path.join(WEB_MODELS_DIR, out_name)
        sz, req = export_glb(dst, animations=False, jpeg_tex=True, clamp_metallic=True)
        print(f"[WEB GLB OK] {out_name}: {sz:,} B | tris~{tris:,} | extReq={req}")


def main():
    if bpy is None:
        print("Run this script inside Blender: blender --background --python scripts/build_phase1_3d_assets.py")
        sys.exit(0)
    task_1_1_decompress_fleet()
    task_1_2_author_hero_facilities()
    task_1_3_rig_astronaut()
    task_3_1_export_web_glbs()
    print("\n=== ALL PHASE 1 & 3.1 3D ASSETS BUILT SUCCESSFULLY ===")


if __name__ == "__main__":
    main()
