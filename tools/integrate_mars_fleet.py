"""
Mars Sentinel — 3D Exploration Fleet Integration Tool
Integrates unmerged 3D fleet assets (Perseverance, InSight, Opportunity)
into MARS_SENTINEL_HERO_PRODUCTION.blend while strictly preserving
baseline HiRISE terrain geometry and test invariants.
"""

import os
import sys
import math
import array
import hashlib
import bpy
from mathutils import Vector, Euler

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS_DIR = os.path.join(PROJECT_ROOT, "data", "missions_3d")
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLEND1_PATH = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend1")

HIRISE_OBJECT_NAME = "NASA_HiRISE_DTM_Jezero_FieldSite_4m"
HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_HASH = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"

# Verbatim list of all 72 Perseverance component objects present in master scene
PERSEVERANCE_OBJECTS = [
    'Body', 'Body.002', 'Body.003', 'Body_Parts', 'Body_Parts.001',
    'Cylinder', 'Cylinder.001', 'Cylinder.002', 'Down_Look_Camera',
    'Empty', 'Empty.001', 'Mastcam_Z_cams', 'Name_Chips', 'NavCams',
    'PIXL', 'PIXL_cover_01', 'RIMFAX', 'Up_Look_Camera', 'WATSON',
    'Wheels_objs', 'antenna_hg', 'antenna_lg', 'antenna_uhf', 'arm',
    'arm.001', 'arm.002', 'arm.003', 'arm.004', 'arm_01', 'arm_01_joint',
    'arm_01_pivot', 'arm_02', 'arm_02.001', 'arm_02_joint', 'arm_02_pivot',
    'arm_cable_etc', 'base', 'bottom', 'bottom.001', 'bottom.002',
    'bottom.003', 'box', 'calibration_target', 'calibration_target_bracket',
    'corring_drill', 'cover_02', 'cover_03', 'hazcams_front',
    'hazcams_front_cover', 'hazcams_rear', 'hazcams_rear_cover_l',
    'hazcams_rear_cover_r', 'hazcams_rear_wiring', 'head', 'hinge',
    'hinge_cover_01', 'lab', 'lens', 'microphones', 'microphones.001',
    'pan_end cover', 'part_01', 'probe', 'probe.001', 'rtg',
    'suspension', 'top', 'turret_obj', 'turret_obj.002', 'turret_obj.004',
    'turret_obj.006', 'turret_parts'
]

HELPER_PREFIXES = ("_root", "shadow_", "cube_0")


def is_helper_mesh(obj):
    nl = obj.name.lower()
    return any(nl.startswith(p) for p in HELPER_PREFIXES) or nl.endswith("_p")


def assert_terrain_invariant():
    """Strictly assert HiRISE DTM vertex count and coordinate SHA-256 hash."""
    site = bpy.data.objects.get(HIRISE_OBJECT_NAME)
    assert site is not None, f"CRITICAL: {HIRISE_OBJECT_NAME} not found in scene!"
    vert_count = len(site.data.vertices)
    assert vert_count == HIRISE_EXPECTED_VERTS, (
        f"CRITICAL: HiRISE vertex count mismatch! Expected {HIRISE_EXPECTED_VERTS}, got {vert_count}"
    )

    coords = array.array('f', [0.0]) * (vert_count * 3)
    site.data.vertices.foreach_get('co', coords)
    digest = hashlib.sha256(coords.tobytes()).hexdigest()
    assert digest == HIRISE_EXPECTED_HASH, (
        f"CRITICAL: HiRISE hash mismatch! Expected {HIRISE_EXPECTED_HASH}, got {digest}"
    )
    print(f"[INVARIANT] HiRISE terrain verified: {vert_count} vertices, SHA-256: {digest}")


def get_or_create_collection(name, parent=None):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        if parent is None:
            bpy.context.scene.collection.children.link(col)
        else:
            parent.children.link(col)
    return col


def remove_hierarchy_by_root(root_name):
    root = bpy.data.objects.get(root_name)
    if root is None:
        return
    to_remove = [root] + list(root.children_recursive)
    for obj in to_remove:
        bpy.data.objects.remove(obj, do_unlink=True)


def compute_hierarchy_bounds(objects):
    bmin = [1e18, 1e18, 1e18]
    bmax = [-1e18, -1e18, -1e18]
    has_mesh = False
    for obj in objects:
        if obj.type != 'MESH' or obj.hide_viewport or is_helper_mesh(obj):
            continue
        has_mesh = True
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            for i in range(3):
                bmin[i] = min(bmin[i], w[i])
                bmax[i] = max(bmax[i], w[i])
    if not has_mesh:
        return Vector((0, 0, 0)), Vector((0, 0, 0))
    return Vector(bmin), Vector(bmax)


def get_terrain_elevation(x, y):
    terrain = bpy.data.objects.get(HIRISE_OBJECT_NAME)
    if terrain is None:
        return 0.0
    inv_m = terrain.matrix_world.inverted()
    ray_origin_local = inv_m @ Vector((x, y, 500.0))
    ray_dir_local = (inv_m.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized()
    hit, loc_local, norm_local, idx = terrain.ray_cast(ray_origin_local, ray_dir_local)
    if hit:
        loc_world = terrain.matrix_world @ loc_local
        return loc_world.z
    return 0.0


def ground_root_on_terrain(root, kept_objects, x, y, rot_z_deg=0.0):
    terrain = bpy.data.objects.get(HIRISE_OBJECT_NAME)
    if terrain is None:
        print(f"[WARN] Terrain not found for grounding {root.name}")
        return

    root.location = Vector((x, y, 0.0))
    root.rotation_euler = Euler((0.0, 0.0, math.radians(rot_z_deg)), 'XYZ')
    bpy.context.view_layer.update()

    bmin, bmax = compute_hierarchy_bounds(kept_objects)
    center_x = (bmin.x + bmax.x) * 0.5
    center_y = (bmin.y + bmax.y) * 0.5

    dx = x - center_x
    dy = y - center_y
    root.location.x += dx
    root.location.y += dy
    bpy.context.view_layer.update()

    terrain_z = get_terrain_elevation(x, y)

    bmin, _ = compute_hierarchy_bounds(kept_objects)
    dz = terrain_z - bmin.z
    root.location.z += dz
    bpy.context.view_layer.update()
    print(f"[SURFACE] {root.name} grounded at ({x:.2f}, {y:.2f}, z={terrain_z:.3f}), rotZ={rot_z_deg} deg")


def import_glb_under_root(filepath, root_name, target_col, scale_factor=1.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=filepath)
    imported = [o for o in bpy.data.objects if o not in before]

    kept = []
    for o in imported:
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            kept.append(o)

    root = bpy.data.objects.new(root_name, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 1.0
    target_col.objects.link(root)

    for o in kept:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        target_col.objects.link(o)
        if o.parent is None or o.parent not in kept:
            o.parent = root

    bpy.context.view_layer.update()
    root.scale = (scale_factor, scale_factor, scale_factor)
    bpy.context.view_layer.update()
    return root, kept


def append_blend_meshes_under_root(filepath, root_name, target_col, scale_factor=1.0):
    with bpy.data.libraries.load(filepath, link=False) as (data_from, data_to):
        data_to.objects = [name for name in data_from.objects]

    imported = [o for o in data_to.objects if o is not None]
    kept = []
    for o in imported:
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            target_col.objects.link(o)
            if o.type == 'MESH' and is_helper_mesh(o):
                o.hide_viewport = True
                o.hide_render = True
            kept.append(o)

    root = bpy.data.objects.new(root_name, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 1.0
    target_col.objects.link(root)

    for o in kept:
        if o.parent is None or o.parent not in kept:
            o.parent = root

    bpy.context.view_layer.update()
    root.scale = (scale_factor, scale_factor, scale_factor)
    bpy.context.view_layer.update()
    return root, kept


def place_orbiter_around_globe(root, kept_objects, world_pos, target_world_span=52000.0, rot_deg=(15.0, 25.0, 40.0)):
    bpy.context.view_layer.update()
    bmin, bmax = compute_hierarchy_bounds(kept_objects)
    dims = bmax - bmin
    max_dim = max(dims.x, dims.y, dims.z, 0.01)

    norm_scale = target_world_span / max_dim

    root.parent = None
    root.location = Vector(world_pos)
    root.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    root.scale = (norm_scale, norm_scale, norm_scale)
    bpy.context.view_layer.update()
    print(f"[ORBIT] {root.name} placed in world orbit at {world_pos}, scale={norm_scale:.2f}")


def organize_perseverance(surface_col):
    """
    Organizes the 72 Perseverance component objects from root Scene Collection
    into MS_Mars_Fleet_Surface, parents under MS_Perseverance_Root, and grounds flush.
    """
    print("[PERSEVERANCE] Organizing Perseverance Rover components...")
    scene_col = bpy.context.scene.collection
    persev_set = set(PERSEVERANCE_OBJECTS)

    root = bpy.data.objects.get("MS_Perseverance_Root")
    if root is None:
        root = bpy.data.objects.new("MS_Perseverance_Root", None)
        root.empty_display_type = 'PLAIN_AXES'
        root.empty_display_size = 1.0
        surface_col.objects.link(root)

    target_x, target_y = 8.68, 9.73
    terrain_z = get_terrain_elevation(target_x, target_y)

    # Compute lowest point of wheels
    wheels = bpy.data.objects.get("Wheels_objs")
    if wheels:
        zmin = min((wheels.matrix_world @ Vector(c)).z for c in wheels.bound_box)
    else:
        zmin = 0.0331

    dz = terrain_z - zmin
    print(f"[PERSEVERANCE] Terrain Z: {terrain_z:.4f}, Wheel Zmin: {zmin:.4f}, Grounding Delta dz: {dz:.4f}")

    root.location = Vector((target_x, target_y, terrain_z))
    bpy.context.view_layer.update()

    # Move objects to MS_Mars_Fleet_Surface and shift unparented by dz
    for name in PERSEVERANCE_OBJECTS:
        o = bpy.data.objects.get(name)
        if o:
            if o.name not in surface_col.objects:
                surface_col.objects.link(o)
            if o.name in scene_col.objects:
                scene_col.objects.unlink(o)
            if o.parent is None:
                o.location.z += dz

    bpy.context.view_layer.update()

    # Parent top-level components to root empty using inverse transform preservation
    for name in PERSEVERANCE_OBJECTS:
        o = bpy.data.objects.get(name)
        if o and (o.parent is None or o.parent.name not in persev_set):
            if o.parent != root:
                o.parent = root
                o.matrix_parent_inverse = root.matrix_world.inverted()

    bpy.context.view_layer.update()
    print(f"[PERSEVERANCE] Successfully organized {len(PERSEVERANCE_OBJECTS)} components under MS_Perseverance_Root.")
    return root


def integrate_insight_lander(surface_col):
    """
    Imports InSight Cruise Lander GLB (1:1 physical scale), packs textures,
    creates MS_InSight_Lander_Root, and grounds on Heritage Showcase Arc.
    """
    print("[INSIGHT] Integrating InSight Cruise Lander...")
    glb_path = os.path.join(MISSIONS_DIR, "insight", "InSight Cruise Lander (panels deployed).glb")
    if not os.path.exists(glb_path):
        print(f"[ERROR] InSight GLB not found at {glb_path}")
        return None

    # Remove stale root if re-running
    remove_hierarchy_by_root("MS_InSight_Lander_Root")

    root, kept = import_glb_under_root(glb_path, "MS_InSight_Lander_Root", surface_col, scale_factor=1.0)
    ground_root_on_terrain(root, kept, x=39.5, y=13.5, rot_z_deg=-160.0)

    # Pack InSight textures
    insight_images = [img for img in bpy.data.images if "insight" in img.name.lower() or "foil_silver" in img.name.lower()]
    for img in insight_images:
        if img.packed_file is None:
            try:
                img.pack()
            except Exception as e:
                print(f"[WARN] Packing {img.name}: {e}")

    print(f"[INSIGHT] Successfully integrated InSight Lander ({len(kept)} objects, {len(insight_images)} textures).")
    return root


def integrate_opportunity_rover(surface_col):
    """
    Imports Opportunity Rover GLB (scaled by 0.183 to authentic 2.3m MER span),
    creates MS_Opportunity_Root, and grounds on Heritage Showcase Arc.
    """
    print("[OPPORTUNITY] Integrating Opportunity Rover (MER-B)...")
    glb_path = os.path.join(MISSIONS_DIR, "opportunity", "mars-exploration-rover-opportunity-mer-b.glb")
    if not os.path.exists(glb_path):
        print(f"[ERROR] Opportunity GLB not found at {glb_path}")
        return None

    remove_hierarchy_by_root("MS_Opportunity_Root")

    root, kept = import_glb_under_root(glb_path, "MS_Opportunity_Root", surface_col, scale_factor=0.183)
    ground_root_on_terrain(root, kept, x=31.0, y=7.5, rot_z_deg=-145.0)

    print(f"[OPPORTUNITY] Successfully integrated Opportunity Rover (scale 0.183) at (31.0, 7.5).")
    return root


def main():
    print("=== Mars Sentinel: 3D Fleet Asset Integration (Milestone 1) ===")

    # 1. Pre-execution Invariant Check
    assert_terrain_invariant()

    # Ensure collection infrastructure
    surface_col = get_or_create_collection("MS_Mars_Fleet_Surface")
    globe_col = bpy.data.collections.get("NEW_Mars_Globe")
    orbital_col = get_or_create_collection("MS_Mars_Orbital_Fleet", parent=globe_col)

    # 2. Existing fleet assets: Preserve if already present, import if missing
    if not bpy.data.objects.get("MS_Ingenuity_Root"):
        ing_path = os.path.join(MISSIONS_DIR, "ingenuity", "Ingenuity_Mars_Helicopter.glb")
        if os.path.exists(ing_path):
            root, kept = import_glb_under_root(ing_path, "MS_Ingenuity_Root", surface_col, scale_factor=1.0)
            ground_root_on_terrain(root, kept, x=12.8, y=6.5, rot_z_deg=-35.0)

    if not bpy.data.objects.get("MS_Curiosity_Root"):
        cur_path = os.path.join(MISSIONS_DIR, "curiosity", "Curiosity Rover (MSL) (Clean).blend")
        if os.path.exists(cur_path):
            root, kept = append_blend_meshes_under_root(cur_path, "MS_Curiosity_Root", surface_col, scale_factor=1.0)
            ground_root_on_terrain(root, kept, x=24.5, y=2.0, rot_z_deg=-130.0)

    if not bpy.data.objects.get("MS_MER_SpiritOpp_Root"):
        mer_path = os.path.join(MISSIONS_DIR, "spirit_opportunity", "Mars Exploration Rover - Spirit and Opportunity.blend")
        if os.path.exists(mer_path):
            root, kept = append_blend_meshes_under_root(mer_path, "MS_MER_SpiritOpp_Root", surface_col, scale_factor=1.0)
            ground_root_on_terrain(root, kept, x=29.5, y=5.5, rot_z_deg=-140.0)

    if not bpy.data.objects.get("MS_Viking_Lander_Root"):
        vik_path = os.path.join(MISSIONS_DIR, "viking_lander", "Viking Lander.glb")
        if os.path.exists(vik_path):
            root, kept = import_glb_under_root(vik_path, "MS_Viking_Lander_Root", surface_col, scale_factor=0.22)
            ground_root_on_terrain(root, kept, x=34.5, y=9.5, rot_z_deg=-150.0)

    # Orbital Fleet check
    orbiters = [
        ("MS_Orbiter_MRO", os.path.join(MISSIONS_DIR, "mro", "Mars Reconnaissance Orbiter (MRO) (A).glb"), (110000.0, -380000.0, -25000.0), (20.0, -15.0, 50.0)),
        ("MS_Orbiter_Odyssey", os.path.join(MISSIONS_DIR, "mars_odyssey", "Mars Odyssey.glb"), (30000.0, -400000.0, -560000.0), (-15.0, 30.0, -40.0)),
        ("MS_Orbiter_MAVEN", os.path.join(MISSIONS_DIR, "maven", "Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb"), (-110000.0, 380000.0, -570000.0), (35.0, 10.0, 110.0)),
        ("MS_Orbiter_GlobalSurveyor", os.path.join(MISSIONS_DIR, "global_surveyor", "Mars Global Surveyor.glb"), (80000.0, -450000.0, -300000.0), (-25.0, -20.0, -120.0)),
    ]
    for name, fpath, wpos, rot in orbiters:
        if not bpy.data.objects.get(name) and os.path.exists(fpath):
            root, kept = import_glb_under_root(fpath, name, orbital_col, scale_factor=1.0)
            place_orbiter_around_globe(root, kept, wpos, target_world_span=78000.0, rot_deg=rot)

    if globe_col is not None:
        orbital_col.hide_viewport = globe_col.hide_viewport
        orbital_col.hide_render = globe_col.hide_render

    # 3. Integrate Milestone 1 Assets
    # A. Organize Perseverance (72 components from Scene Collection -> MS_Mars_Fleet_Surface)
    organize_perseverance(surface_col)

    # B. Import InSight Cruise Lander
    integrate_insight_lander(surface_col)

    # C. Import Opportunity Rover
    integrate_opportunity_rover(surface_col)

    # 4. Pack external images into master blend
    try:
        bpy.ops.file.pack_all()
        print("[TEXTURES] Packed all external textures successfully.")
    except Exception as e:
        print(f"[WARN] Packing external textures notice: {e}")

    # 5. In-Place Preservation Save
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_mainfile()
    print("[SAVE] Master scene saved in-place with save_version = 0.")

    # 6. Post-Save Cleanup of any accidental .blend1 file
    if os.path.exists(BLEND1_PATH):
        try:
            os.remove(BLEND1_PATH)
            print(f"[CLEANUP] Removed redundant backup: {BLEND1_PATH}")
        except Exception as e:
            print(f"[WARN] Could not remove .blend1: {e}")

    # 7. Post-execution Invariant Check
    assert_terrain_invariant()
    print("=== Milestone 1 Integration Complete and Verified! ===")


if __name__ == "__main__":
    main()
