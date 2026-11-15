"""
Milestone 8 Deep Adversarial Oracle Test Suite
Author: Challenger M8-1 (Blender & Astrodynamics Empirical Challenger)
Scope: Empirical and mathematical verification of Mars orbital mechanics, celestial bodies, diurnal solar arc, and nadir constellation tracking.
"""

import os
import sys
import json
import math
import array
import hashlib
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"


class Milestone8DeepAdversarialOracle(unittest.TestCase):
    """Empirical mathematical and physics verification suite executed against MARS_SENTINEL_HERO_PRODUCTION.blend."""

    @classmethod
    def setUpClass(cls):
        """Execute headless Blender inspection capturing empirical astrodynamics data."""
        if not os.path.isfile(BLENDER_BIN) or not os.path.isfile(HERO_BLEND):
            raise unittest.SkipTest("Blender binary or hero blend file not found.")

        script = """
import bpy, json, hashlib, array, math, mathutils

result = {}

# 1. HiRISE Terrain Invariant
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
if site and site.data:
    verts = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (verts * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    result["hirise"] = {"exists": True, "verts": verts, "sha256": h}
else:
    result["hirise"] = {"exists": False}

# Ensure orbital fleet and globe collections are visible to depsgraph
for c in ['NEW_Mars_Globe', 'MS_Mars_Orbital_Fleet']:
    if c in bpy.data.collections:
        bpy.data.collections[c].hide_viewport = False

scene = bpy.context.scene
globe = bpy.data.objects.get('MARS_NEW_Globe')
scene.frame_set(1)
dg = bpy.context.evaluated_depsgraph_get()
g_mat = globe.evaluated_get(dg).matrix_world
g_pos = g_mat.translation
mars_polar = (g_mat.to_3x3() @ mathutils.Vector((0, 0, 1))).normalized()

# 2. Celestial Moons (Phobos & Deimos)
phobos = bpy.data.objects.get('MS_Moon_Phobos')
deimos = bpy.data.objects.get('MS_Moon_Deimos')
p_curve = bpy.data.objects.get('MS_Orbit_Path_Phobos')
d_curve = bpy.data.objects.get('MS_Orbit_Path_Deimos')

moons_data = {}
for name, obj, curve, exp_inc in [('phobos', phobos, p_curve, 1.08), ('deimos', deimos, d_curve, 1.79)]:
    if not obj or not curve:
        moons_data[name] = {"exists": False}
        continue
    
    mat = obj.material_slots[0].material if obj.material_slots else None
    node_types = [n.type for n in mat.node_tree.nodes] if (mat and mat.node_tree) else []
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat else None
    albedo = list(bsdf.inputs['Base Color'].default_value[:3]) if bsdf else []
    roughness = bsdf.inputs['Roughness'].default_value if bsdf else 0.0
    
    # Measure distances across frames 1 to 241
    distances = []
    positions = []
    for f in [1, 30, 60, 90, 120, 150, 180, 210, 240, 241]:
        scene.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        p = obj.evaluated_get(dg).matrix_world.translation
        positions.append(list(p))
        distances.append((p - g_pos).length)
    
    # Trajectory inclination from cross product of 2 positions from center
    v1 = mathutils.Vector(positions[1]) - g_pos
    v2 = mathutils.Vector(positions[3]) - g_pos
    normal = v1.cross(v2).normalized()
    inc_deg = math.degrees(normal.angle(mars_polar))
    if inc_deg > 90.0:
        inc_deg = 180.0 - inc_deg
        
    moons_data[name] = {
        "exists": True,
        "dimensions": list(obj.dimensions),
        "vertices_count": len(obj.data.vertices),
        "polygons_count": len(obj.data.polygons),
        "node_types": node_types,
        "has_image_texture": any(t in node_types for t in ['TEX_IMAGE', 'TEX_ENVIRONMENT']),
        "albedo": albedo,
        "roughness": roughness,
        "distances": distances,
        "measured_inclination": inc_deg,
        "curve_rot_x_deg": math.degrees(curve.rotation_euler.x)
    }
result["moons"] = moons_data

# 3. Mars Sun Diurnal Lighting
sun = bpy.data.objects.get('Mars_Sun')
sun_data = {"exists": sun is not None}
if sun and sun.data:
    sun_data["type"] = sun.data.type
    frames_eval = {}
    for f in [1, 60, 120, 150, 180, 240, 241]:
        scene.frame_set(f)
        sun_dir = (sun.matrix_world.to_3x3() @ mathutils.Vector((0, 0, -1))).normalized()
        frames_eval[str(f)] = {
            "energy": sun.data.energy,
            "color": list(sun.data.color[:3]),
            "rot_euler_deg": [math.degrees(a) for a in sun.rotation_euler],
            "dir_z": sun_dir.z
        }
    sun_data["frames"] = frames_eval
result["sun"] = sun_data

# 4. Satellite Constellation & Nadir Pointing
const_data = {}
sat_list = [
    ('MS_Orbiter_MRO', 'MS_Orbit_Path_MRO', 92.8),
    ('MS_Orbiter_Odyssey', 'MS_Orbit_Path_Odyssey', 93.1),
    ('MS_Orbiter_MAVEN', 'MS_Orbit_Path_MAVEN', 75.0),
    ('MS_Orbiter_GlobalSurveyor', 'MS_Orbit_Path_GlobalSurveyor', 92.8),
]
for sat_name, curve_name, exp_inc in sat_list:
    sat_obj = bpy.data.objects.get(sat_name)
    curve_obj = bpy.data.objects.get(curve_name)
    if not sat_obj or not curve_obj:
        const_data[sat_name] = {"exists": False}
        continue
    
    dots = []
    for f in [1, 30, 60, 90, 120, 150, 180, 210, 240]:
        scene.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        s_eval = sat_obj.evaluated_get(dg)
        s_pos = s_eval.matrix_world.translation
        gp = globe.evaluated_get(dg).matrix_world.translation
        u_nadir = (gp - s_pos).normalized()
        d_track = (s_eval.matrix_world.to_3x3() @ mathutils.Vector((0, 0, -1))).normalized()
        dots.append(d_track.dot(u_nadir))
        
    const_data[sat_name] = {
        "exists": True,
        "curve_rot_euler_deg": [math.degrees(a) for a in curve_obj.rotation_euler],
        "nadir_dots": dots,
        "min_dot": min(dots),
        "max_dot": max(dots)
    }
result["constellation"] = const_data

# 5. Datablock Hygiene
all_images = [img for img in bpy.data.images if img.name not in ('Render Result', 'Viewer Node')]
result["hygiene"] = {
    "total_images": len(all_images),
    "libraries_count": len(bpy.data.libraries),
    "missing_images": [img.name for img in all_images if not img.packed_file and not os.path.exists(bpy.path.abspath(img.filepath))]
}

print("ADVERSARIAL_JSON_START")
print(json.dumps(result))
print("ADVERSARIAL_JSON_END")
"""
        cmd = [BLENDER_BIN, HERO_BLEND, "--background", "--python-expr", script]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        assert proc.returncode == 0, f"Blender headless execution failed: {proc.stderr}"
        s = proc.stdout.find("ADVERSARIAL_JSON_START")
        e = proc.stdout.find("ADVERSARIAL_JSON_END")
        assert s != -1 and e != -1, f"Failed to extract JSON markers:\n{proc.stdout}"
        cls.data = json.loads(proc.stdout[s + len("ADVERSARIAL_JSON_START"):e].strip())

    def test_01_hirise_invariant_hash_and_verts(self):
        """Stress-test: HiRISE mesh must match exact 1,132,096 verts and ed5e2c9e...354dd hash."""
        hirise = self.data.get("hirise", {})
        self.assertTrue(hirise.get("exists"), "HiRISE terrain object missing")
        self.assertEqual(hirise.get("verts"), HIRISE_EXPECTED_VERTS)
        self.assertEqual(hirise.get("sha256"), HIRISE_EXPECTED_SHA256)

    def test_02_phobos_physical_dimensions_and_scaling(self):
        """Stress-test: Phobos dimensions match 27x22x18 km scaled ellipsoid (ratio ~ 1.195:0.974:0.797)."""
        phobos = self.data["moons"]["phobos"]
        self.assertTrue(phobos["exists"])
        dims = phobos["dimensions"]
        # In blender local coordinates: ~2.32 x 1.95 x 1.59
        self.assertAlmostEqual(dims[0] / dims[1], 27.0 / 22.0, delta=0.08)
        self.assertAlmostEqual(dims[1] / dims[2], 22.0 / 18.0, delta=0.08)
        self.assertFalse(phobos["has_image_texture"], "Phobos must not use image textures")
        self.assertAlmostEqual(phobos["albedo"][0], 0.071, delta=0.01)

    def test_03_deimos_physical_dimensions_and_scaling(self):
        """Stress-test: Deimos dimensions match 15x12x10 km scaled ellipsoid (ratio ~ 1.5:1.2:1.0)."""
        deimos = self.data["moons"]["deimos"]
        self.assertTrue(deimos["exists"])
        dims = deimos["dimensions"]
        # In blender local coordinates: ~1.33 x 1.06 x 0.89
        self.assertAlmostEqual(dims[0] / dims[1], 15.0 / 12.0, delta=0.08)
        self.assertAlmostEqual(dims[1] / dims[2], 12.0 / 10.0, delta=0.08)
        self.assertFalse(deimos["has_image_texture"], "Deimos must not use image textures")
        self.assertAlmostEqual(deimos["albedo"][0], 0.068, delta=0.01)

    def test_04_moons_semi_major_axis_and_inclinations(self):
        """Stress-test: Phobos and Deimos orbital radii ratio and inclination angles."""
        p = self.data["moons"]["phobos"]
        d = self.data["moons"]["deimos"]
        # Phobos R ~ 829,850 world units (ratio ~ 2.766 R_Mars where R_Mars = 300,000)
        p_avg_r = sum(p["distances"]) / len(p["distances"])
        d_avg_r = sum(d["distances"]) / len(d["distances"])
        r_mars = 300000.0
        self.assertAlmostEqual(p_avg_r / r_mars, 2.766, delta=0.02)
        self.assertAlmostEqual(d_avg_r / r_mars, 6.922, delta=0.04)
        
        # Inclinations
        self.assertAlmostEqual(p["measured_inclination"], 1.08, delta=0.05)
        self.assertAlmostEqual(d["measured_inclination"], 1.79, delta=0.05)
        self.assertAlmostEqual(p["curve_rot_x_deg"], 1.08, delta=0.01)
        self.assertAlmostEqual(d["curve_rot_x_deg"], 1.79, delta=0.01)

    def test_05_sun_diurnal_lighting_keyframes(self):
        """Stress-test: Mars_Sun diurnal lighting cycle, peak noon energy, and twilight transition."""
        sun = self.data.get("sun", {})
        self.assertTrue(sun.get("exists"))
        self.assertEqual(sun.get("type"), "SUN")
        frames = sun.get("frames", {})
        
        f1 = frames["1"]
        f60 = frames["60"]
        f120 = frames["120"]
        f150 = frames["150"]
        f180 = frames["180"]
        f241 = frames["241"]
        
        # Energy variation: Peak at noon, lower at sunrise/sunset, lowest at midnight
        self.assertGreater(f60["energy"], f1["energy"])
        self.assertGreater(f1["energy"], f150["energy"])
        self.assertGreater(f150["energy"], f180["energy"])
        self.assertAlmostEqual(f60["energy"], 2.85, delta=0.05)
        self.assertAlmostEqual(f180["energy"], 0.08, delta=0.02)
        
        # Horizon transition: Noon points down (-Z), Midnight points up (+Z)
        self.assertLess(f60["dir_z"], -0.4)
        self.assertGreater(f180["dir_z"], 0.4)
        
        # Seamless loop: frame 1 and 241 identical
        self.assertEqual(f1["energy"], f241["energy"])
        self.assertEqual(f1["color"], f241["color"])
        for a, b in zip(f1["rot_euler_deg"], f241["rot_euler_deg"]):
            self.assertAlmostEqual(a, b, delta=0.01)

    def test_06_satellites_nadir_pointing_accuracy(self):
        """Stress-test: All 4 orbiters maintain exact nadir tracking vector dot product == 1.0."""
        const = self.data.get("constellation", {})
        orbiters = ["MS_Orbiter_MRO", "MS_Orbiter_Odyssey", "MS_Orbiter_MAVEN", "MS_Orbiter_GlobalSurveyor"]
        for name in orbiters:
            self.assertIn(name, const, f"Missing constellation data for {name}")
            sat = const[name]
            self.assertTrue(sat.get("exists"), f"{name} does not exist")
            min_dot = sat.get("min_dot", 0.0)
            max_dot = sat.get("max_dot", 0.0)
            self.assertAlmostEqual(min_dot, 1.0, delta=1e-5, msg=f"{name} failed nadir minimum dot product")
            self.assertAlmostEqual(max_dot, 1.0, delta=1e-5, msg=f"{name} failed nadir maximum dot product")

    def test_07_satellites_curve_inclinations(self):
        """Stress-test: Spline inclinations match mission specifications (MRO ~92.8°, Odyssey ~93.1°, MAVEN ~75°, MGS ~92.8°)."""
        const = self.data.get("constellation", {})
        expected = {
            "MS_Orbiter_MRO": 92.8,
            "MS_Orbiter_Odyssey": 93.1,
            "MS_Orbiter_MAVEN": 75.0,
            "MS_Orbiter_GlobalSurveyor": 92.8
        }
        for name, exp_inc in expected.items():
            sat = const[name]
            rot_x = sat["curve_rot_euler_deg"][0]
            self.assertAlmostEqual(rot_x, exp_inc, delta=0.05, msg=f"{name} curve inclination mismatch")

    def test_08_blend_image_and_library_hygiene(self):
        """Stress-test: Exactly 276 image datablocks, 0 missing images, 0 external libraries."""
        hygiene = self.data.get("hygiene", {})
        self.assertEqual(hygiene.get("total_images"), 276)
        self.assertEqual(hygiene.get("libraries_count"), 0)
        self.assertEqual(hygiene.get("missing_images"), [])

    def test_09_zero_blend1_backup_files(self):
        """Stress-test: Entire project repository must contain zero .blend1 backup files."""
        blend1_files = []
        for root, dirs, files in os.walk(PROJECT_ROOT):
            # Skip .git directory
            if ".git" in root:
                continue
            for f in files:
                if f.endswith(".blend1"):
                    blend1_files.append(os.path.join(root, f))
        self.assertEqual(blend1_files, [], f"Found .blend1 backup files: {blend1_files}")


if __name__ == "__main__":
    unittest.main()
