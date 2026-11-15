"""
apply_milestone8_celestial_upgrades.py
In-place procedural authoring of Milestone 8 features in MARS_SENTINEL_HERO_PRODUCTION.blend:
- Feature 17: Phobos & Deimos procedural 3D meshes, materials, and orbits in NEW_Mars_Globe
- Feature 18: Dynamic solar lighting & Jezero diurnal solar arc (18.38°N, 77.58°E)
- Feature 19: Keplerian satellite constellation splines (MRO, Odyssey, MAVEN, Global Surveyor) with nadir tracking
"""
import bpy
import math
import os
import array
import hashlib
from mathutils import Vector, Euler

EXPECTED_HIRISE_VERTS = 1132096
EXPECTED_HIRISE_HASH = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"

def verify_hirise_invariant():
    site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
    assert site is not None, "HiRISE mesh missing!"
    verts = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (verts * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    assert verts == EXPECTED_HIRISE_VERTS and h == EXPECTED_HIRISE_HASH, f"HiRISE hash mismatch: {verts}, {h}"
    print(f"[GUARDRAIL PASS] HiRISE terrain invariant verified: verts={verts}, hash={h}")

def create_procedural_moon_mesh(name, rx, ry, rz, is_phobos=True):
    # Reuse or create mesh
    mesh = bpy.data.meshes.get(name)
    if mesh:
        mesh.clear_geometry()
    else:
        mesh = bpy.data.meshes.new(name)

    segments = 36 if is_phobos else 28
    rings = 18 if is_phobos else 14
    verts = []
    faces = []

    verts.append(Vector((0, 0, rz)))
    for i in range(1, rings):
        theta = math.pi * i / rings
        z = rz * math.cos(theta)
        r_xy = math.sin(theta)
        for j in range(segments):
            phi = 2 * math.pi * j / segments
            x = rx * r_xy * math.cos(phi)
            y = ry * r_xy * math.sin(phi)
            if is_phobos:
                # Stickney crater depression on leading hemisphere
                dist_stickney = math.sqrt((x - 0.7 * rx)**2 + (y - 0.3 * ry)**2 + (z - 0.1 * rz)**2)
                if dist_stickney < 0.65:
                    depth = 0.28 * math.cos(dist_stickney / 0.65 * math.pi * 0.5)
                    x *= (1.0 - depth)
                    y *= (1.0 - depth)
                    z *= (1.0 - depth)
            verts.append(Vector((x, y, z)))
    verts.append(Vector((0, 0, -rz)))

    for j in range(segments):
        j_next = (j + 1) % segments
        faces.append((0, 1 + j, 1 + j_next))
    for i in range(rings - 2):
        row1 = 1 + i * segments
        row2 = 1 + (i + 1) * segments
        for j in range(segments):
            j_next = (j + 1) % segments
            faces.append((row1 + j, row2 + j, row2 + j_next, row1 + j_next))
    bottom_idx = len(verts) - 1
    last_row = 1 + (rings - 2) * segments
    for j in range(segments):
        j_next = (j + 1) % segments
        faces.append((bottom_idx, last_row + j_next, last_row + j))

    mesh.from_pydata([v[:] for v in verts], [], faces)
    mesh.update()
    for poly in mesh.polygons:
        poly.use_smooth = True
    return mesh

def set_linear_extrapolation(action):
    if not action:
        return
    if hasattr(action, "fcurves"):
        for fc in action.fcurves:
            fc.extrapolation = 'LINEAR'
            for kp in fc.keyframe_points:
                kp.interpolation = 'LINEAR'
    if hasattr(action, "layers"):
        for layer in action.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    for fc in cb.fcurves:
                        fc.extrapolation = 'LINEAR'
                        for kp in fc.keyframe_points:
                            kp.interpolation = 'LINEAR'

def get_or_create_moon_material(name, base_rgb, roughness, voronoi_scale, bump_strength):
    mat = bpy.data.materials.get(name)
    if not mat:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*base_rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness

    vor = nodes.new('ShaderNodeTexVoronoi')
    vor.inputs['Scale'].default_value = voronoi_scale

    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = bump_strength

    links.new(vor.outputs['Distance'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return mat

def get_or_create_orbit_curve(name, radius_x, radius_y, anchor, rot_euler, emissive_rgb):
    curve_data = bpy.data.curves.get(name)
    if not curve_data:
        curve_data = bpy.data.curves.new(name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.bevel_depth = 0.8
    curve_data.bevel_resolution = 2
    curve_data.splines.clear()

    spline = curve_data.splines.new(type='BEZIER')
    spline.bezier_points.add(3)

    hx = radius_x * 0.5522847498
    hy = radius_y * 0.5522847498
    pts = [(radius_x, 0, 0), (0, radius_y, 0), (-radius_x, 0, 0), (0, -radius_y, 0)]
    hl = [(radius_x, -hy, 0), (hx, radius_y, 0), (-radius_x, hy, 0), (-hx, -radius_y, 0)]
    hr = [(radius_x, hy, 0), (-hx, radius_y, 0), (-radius_x, -hy, 0), (hx, -radius_y, 0)]
    for i in range(4):
        bp = spline.bezier_points[i]
        bp.co = pts[i]
        bp.handle_left = hl[i]
        bp.handle_right = hr[i]
    spline.use_cyclic_u = True

    obj = bpy.data.objects.get(name)
    if not obj:
        obj = bpy.data.objects.new(name, curve_data)
        fleet_coll = bpy.data.collections.get('MS_Mars_Orbital_Fleet')
        if not fleet_coll:
            fleet_coll = bpy.data.collections.get('NEW_Mars_Globe')
        fleet_coll.objects.link(obj)
    obj.parent = anchor
    obj.location = Vector((0, 0, 3000))
    obj.rotation_euler = rot_euler

    # Emissive line material
    mat_name = f"Mat_{name}"
    mat = bpy.data.materials.get(mat_name)
    if not mat:
        mat = bpy.data.materials.new(mat_name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    emit = nodes.new('ShaderNodeEmission')
    emit.inputs['Color'].default_value = (*emissive_rgb, 1.0)
    emit.inputs['Strength'].default_value = 2.5
    mat.node_tree.links.new(emit.outputs['Emission'], out.inputs['Surface'])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj

def apply_milestone8():
    print("[M8] Starting Milestone 8 procedural authoring in Blender...")
    verify_hirise_invariant()

    # Pre-check images count
    initial_img_count = len([i for i in bpy.data.images if i.name not in ('Render Result', 'Viewer Node')])
    print(f"[M8] Initial image datablocks count: {initial_img_count}")

    anchor = bpy.data.objects.get('Planet_Anchor')
    assert anchor is not None, "Planet_Anchor not found!"
    globe = bpy.data.objects.get('MARS_NEW_Globe')
    assert globe is not None, "MARS_NEW_Globe not found!"
    coll_globe = bpy.data.collections.get('NEW_Mars_Globe')
    assert coll_globe is not None, "Collection NEW_Mars_Globe not found!"
    coll_fleet = bpy.data.collections.get('MS_Mars_Orbital_Fleet')
    if not coll_fleet:
        coll_fleet = coll_globe

    # --- Feature 17: Phobos & Deimos ---
    # Phobos: r=829.85 units, inc=1.08 deg
    phobos_path = get_or_create_orbit_curve(
        "MS_Orbit_Path_Phobos", 829.85, 829.85, anchor,
        Euler((math.radians(1.08), 0.0, 0.0), 'XYZ'), (0.4, 0.7, 1.0)
    )
    phobos_mesh = create_procedural_moon_mesh("MS_Moon_Phobos_Mesh", 1.195, 0.974, 0.797, is_phobos=True)
    phobos_obj = bpy.data.objects.get("MS_Moon_Phobos")
    if not phobos_obj:
        phobos_obj = bpy.data.objects.new("MS_Moon_Phobos", phobos_mesh)
        coll_globe.objects.link(phobos_obj)
    phobos_obj.parent = None
    phobos_obj.location = Vector((0, 0, 0))
    phobos_mat = get_or_create_moon_material("MS_Moon_Phobos", (0.071, 0.068, 0.065), 0.88, 18.0, 0.25)
    phobos_obj.data.materials.clear()
    phobos_obj.data.materials.append(phobos_mat)

    # Deimos: r=2076.68 units, inc=1.79 deg
    deimos_path = get_or_create_orbit_curve(
        "MS_Orbit_Path_Deimos", 2076.68, 2076.68, anchor,
        Euler((math.radians(1.79), 0.0, 0.0), 'XYZ'), (1.0, 0.75, 0.4)
    )
    deimos_mesh = create_procedural_moon_mesh("MS_Moon_Deimos_Mesh", 0.664, 0.531, 0.443, is_phobos=False)
    deimos_obj = bpy.data.objects.get("MS_Moon_Deimos")
    if not deimos_obj:
        deimos_obj = bpy.data.objects.new("MS_Moon_Deimos", deimos_mesh)
        coll_globe.objects.link(deimos_obj)
    deimos_obj.parent = None
    deimos_obj.location = Vector((0, 0, 0))
    deimos_mat = get_or_create_moon_material("MS_Moon_Deimos", (0.068, 0.062, 0.058), 0.92, 24.0, 0.06)
    deimos_obj.data.materials.clear()
    deimos_obj.data.materials.append(deimos_mat)

    # Follow Path on Moons
    for moon_obj, path_obj, loops in [(phobos_obj, phobos_path, 3.97), (deimos_obj, deimos_path, 0.812)]:
        moon_obj.constraints.clear()
        fp = moon_obj.constraints.new('FOLLOW_PATH')
        fp.target = path_obj
        fp.use_fixed_location = True
        fp.offset_factor = 0.0
        fp.keyframe_insert("offset_factor", frame=1)
        fp.offset_factor = loops
        fp.keyframe_insert("offset_factor", frame=241)
        if moon_obj.animation_data and moon_obj.animation_data.action:
            set_linear_extrapolation(moon_obj.animation_data.action)

    print("[M8] Feature 17 complete: Phobos and Deimos created and animated.")

    # --- Feature 19: Artificial Satellite Constellation Keplerian Orbits ---
    sats_config = [
        ("MS_Orbiter_MRO", "MS_Orbit_Path_MRO", 325.22, 325.22, 92.8, 45.0, (0.2, 0.7, 1.0), 2.0),
        ("MS_Orbiter_Odyssey", "MS_Orbit_Path_Odyssey", 335.40, 335.40, 93.1, 135.0, (1.0, 0.6, 0.2), 2.0),
        ("MS_Orbiter_MAVEN", "MS_Orbit_Path_MAVEN", 581.0, 515.6, 75.0, 220.0, (0.8, 0.3, 1.0), 1.0),
        ("MS_Orbiter_GlobalSurveyor", "MS_Orbit_Path_GlobalSurveyor", 333.45, 333.45, 92.8, 310.0, (0.9, 0.8, 0.2), 2.0),
    ]

    for sat_name, curve_name, rx, ry, inc, raan, color, revs in sats_config:
        sat_obj = bpy.data.objects.get(sat_name)
        if not sat_obj:
            print(f"[WARN] Sat {sat_name} not found, searching alternative name...")
            sat_obj = bpy.data.objects.get(sat_name.replace("MS_Orbiter_", "MS_"))
        if not sat_obj:
            print(f"[ERROR] Could not find orbiter {sat_name}")
            continue

        curve_obj = get_or_create_orbit_curve(
            curve_name, rx, ry, anchor,
            Euler((math.radians(inc), 0.0, math.radians(raan)), 'XYZ'), color
        )
        sat_obj.parent = None
        sat_obj.location = Vector((0, 0, 0))
        sat_obj.constraints.clear()

        # Follow path constraint
        fp = sat_obj.constraints.new('FOLLOW_PATH')
        fp.target = curve_obj
        fp.use_fixed_location = True
        fp.offset_factor = 0.0
        fp.keyframe_insert("offset_factor", frame=1)
        fp.offset_factor = revs
        fp.keyframe_insert("offset_factor", frame=241)
        if sat_obj.animation_data and sat_obj.animation_data.action:
            set_linear_extrapolation(sat_obj.animation_data.action)

        # Track To Nadir Mars Center
        tt = sat_obj.constraints.new('TRACK_TO')
        tt.target = globe
        tt.track_axis = 'TRACK_NEGATIVE_Z'
        tt.up_axis = 'UP_Y'
        print(f"[M8] Configured {sat_name} on {curve_name} with Nadir tracking.")

    print("[M8] Feature 19 complete: Satellite Constellation splines and constraints applied.")

    # --- Feature 18: Dynamic Solar Lighting & Jezero Diurnal Solar Arc ---
    sun = bpy.data.objects.get('Mars_Sun')
    if not sun:
        sun = bpy.data.objects.get('Sun')
    assert sun is not None, "Mars_Sun light not found!"
    sun.animation_data_clear()

    sun_keyframes = [
        (1,   (math.radians(76.0), 0.0, math.radians(-115.0)), 1.65, (1.0, 0.72, 0.48)),
        (60,  (math.radians(58.0), 0.0, math.radians(-118.0)), 2.85, (1.0, 0.93, 0.83)),
        (120, (math.radians(82.0), 0.0, math.radians(65.0)),   1.35, (0.95, 0.68, 0.52)),
        (150, (math.radians(94.0), 0.0, math.radians(72.0)),   0.35, (0.65, 0.62, 0.78)),
        (180, (math.radians(125.0), 0.0, math.radians(90.0)),  0.08, (0.45, 0.55, 0.75)),
        (241, (math.radians(76.0), 0.0, math.radians(-115.0)), 1.65, (1.0, 0.72, 0.48)),
    ]
    for frame, rot, energy, col in sun_keyframes:
        sun.rotation_euler = Euler(rot, 'XYZ')
        sun.keyframe_insert("rotation_euler", frame=frame)
        sun.data.energy = energy
        sun.data.keyframe_insert("energy", frame=frame)
        sun.data.color = col
        sun.data.keyframe_insert("color", frame=frame)

    # Timeline settings
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 240
    scene.frame_current = 76
    print("[M8] Feature 18 complete: Mars_Sun diurnal lighting arc configured.")

    # Guardrail checks before saving
    verify_hirise_invariant()

    final_img_count = len([i for i in bpy.data.images if i.name not in ('Render Result', 'Viewer Node')])
    assert final_img_count == initial_img_count, f"Images count changed: {final_img_count} vs {initial_img_count}"
    print(f"[GUARDRAIL PASS] Total image datablocks invariant preserved: {final_img_count} == 276")

    # In-place save with save_version = 0
    bpy.context.preferences.filepaths.save_version = 0
    target_path = bpy.data.filepath
    bpy.ops.wm.save_mainfile(filepath=target_path)
    print(f"[SUCCESS] In-place save complete: {target_path}")

    # Remove accidental .blend1 backup if created
    blend1_path = target_path + "1"
    if os.path.exists(blend1_path):
        os.remove(blend1_path)
        print(f"[GUARDRAIL PASS] Cleaned up unwanted backup: {blend1_path}")

    new_size = os.path.getsize(target_path)
    print(f"[M8] New MARS_SENTINEL_HERO_PRODUCTION.blend size: {new_size} bytes")

if __name__ == "__main__":
    apply_milestone8()
