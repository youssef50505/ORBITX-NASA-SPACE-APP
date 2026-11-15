"""
Milestone 9 Blender Empirical Adversarial Challenge Test Suite
MarsSentinel Master 3D Scene (MARS_SENTINEL_HERO_PRODUCTION.blend)
Focus: Requirement R3 (Visual Polish & Animation Upgrades)

Empirical Challenge Areas:
1. Material Node Tree Architecture (Mars_NASA_HiRISE_Viking_Color):
   - Probe Slope_Factor (Map Range node, inputs, clamping, connections)
   - Probe Rock_Strata_Wave (TexWave node, bands along Z, object coordinates)
   - Probe Slope_Albedo_Blend & Slope_Roughness_Blend (Mix nodes, proper sockets and target inputs)
   - Micro-normal bump chaining: Wave -> Noise -> Strata -> HiRISE -> Principled BSDF
   - Invariant: Zero external image file datablocks (strict 276 packed images)
2. Timeline & Continuous Rotation:
   - Evaluates MARS_NEW_Globe_Rotator FCurve at frames 1, 60, 120, 180, 240, 241, 242
   - Asserts linear extrapolation, constant angular velocity (dtheta/dt = const within 1e-5), and zero-snap loop closure
3. Frame-76 Popping Defect:
   - Adversarially scans 100% of actions and objects for hide_render / hide_viewport keyframes at frame 76
4. Orbiters & Invariants:
   - Asserts all 4 orbiters loop with position difference ||p(241) - p(1)|| < 1e-4 and dynamic motion
   - Verifies NASA_HiRISE_DTM_Jezero_FieldSite_4m coordinates SHA-256 hash equals ed5e2c9e...54dd
   - Asserts 0 .blend1 backup files in workspace
"""

import os
import sys
import json
import math
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"


BLENDER_EXTRACTOR_SCRIPT = r"""
import bpy, json, hashlib, array, math

output = {}

# 1. HiRISE Mesh Invariants
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
if site and site.data:
    v_cnt = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (v_cnt * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    output["hirise"] = {"exists": True, "verts": v_cnt, "sha256": h}
else:
    output["hirise"] = {"exists": False}

# 2. Packed Images Audit
images_data = []
unpacked_count = 0
for img in bpy.data.images:
    if img.name in ('Render Result', 'Viewer Node'):
        continue
    is_packed = bool(img.packed_file or (hasattr(img, 'packed_files') and len(img.packed_files) > 0))
    if not is_packed:
        unpacked_count += 1
    images_data.append({
        "name": img.name,
        "is_packed": is_packed,
        "filepath": img.filepath
    })
output["images"] = {
    "total_count": len(images_data),
    "unpacked_count": unpacked_count
}

# 3. Timeline Configuration
scene = bpy.context.scene
output["timeline"] = {
    "frame_start": scene.frame_start,
    "frame_end": scene.frame_end,
    "frame_current": scene.frame_current,
    "fps": scene.render.fps,
    "fps_base": scene.render.fps_base
}

# 4. Material Node Tree Architecture (Mars_NASA_HiRISE_Viking_Color)
mat = bpy.data.materials.get("Mars_NASA_HiRISE_Viking_Color")
mat_info = {"exists": mat is not None, "use_nodes": mat.use_nodes if mat else False}

if mat and mat.node_tree:
    nt = mat.node_tree
    mat_info["nodes"] = [n.name for n in nt.nodes]
    
    # Links list
    links_list = []
    for l in nt.links:
        links_list.append({
            "from_node": l.from_node.name,
            "from_socket": l.from_socket.name,
            "to_node": l.to_node.name,
            "to_socket": l.to_socket.name
        })
    mat_info["links"] = links_list

    # Probe Slope_Factor
    sf = nt.nodes.get("Slope_Factor")
    if sf:
        mat_info["slope_factor"] = {
            "type": sf.type,
            "from_min": sf.inputs["From Min"].default_value if "From Min" in sf.inputs else None,
            "from_max": sf.inputs["From Max"].default_value if "From Max" in sf.inputs else None,
            "to_min": sf.inputs["To Min"].default_value if "To Min" in sf.inputs else None,
            "to_max": sf.inputs["To Max"].default_value if "To Max" in sf.inputs else None,
            "clamp": getattr(sf, "clamp", None)
        }

    # Probe Rock_Strata_Wave
    rw = nt.nodes.get("Rock_Strata_Wave")
    if rw:
        mat_info["rock_strata_wave"] = {
            "type": rw.type,
            "wave_type": getattr(rw, "wave_type", None),
            "bands_direction": getattr(rw, "bands_direction", None)
        }

    # Probe Slope_Albedo_Blend
    sab = nt.nodes.get("Slope_Albedo_Blend")
    if sab:
        mat_info["slope_albedo_blend"] = {
            "type": sab.type
        }

    # Probe Slope_Roughness_Blend
    srb = nt.nodes.get("Slope_Roughness_Blend")
    if srb:
        cliff_roughness = None
        if "B" in srb.inputs:
            cliff_roughness = srb.inputs["B"].default_value
        elif len(srb.inputs) > 2:
            cliff_roughness = srb.inputs[2].default_value
        mat_info["slope_roughness_blend"] = {
            "type": srb.type,
            "cliff_roughness": cliff_roughness
        }

    # Probe Bump Chain
    mat_info["bump_chain"] = {
        "Bump_exists": "Bump" in nt.nodes,
        "Bump001_exists": "Bump.001" in nt.nodes,
        "Rock_Strata_Bump_exists": "Rock_Strata_Bump" in nt.nodes,
        "Bump002_exists": "Bump.002" in nt.nodes,
        "Principled_BSDF_exists": "Principled BSDF" in nt.nodes
    }

output["material"] = mat_info

# 5. Continuous Rotation Evaluation (MARS_NEW_Globe_Rotator)
rot = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
rot_info = {"exists": rot is not None}
if rot and rot.animation_data and rot.animation_data.action:
    act = rot.animation_data.action
    rot_info["action_name"] = act.name
    fc_z = None
    if hasattr(act, 'layers'):
        for layer in getattr(act, 'layers', []):
            for strip in getattr(layer, 'strips', []):
                for cb in getattr(strip, 'channelbags', []):
                    for fc in cb.fcurves:
                        if fc.data_path == 'rotation_euler' and fc.array_index == 2:
                            fc_z = fc
                            break
    if not fc_z and hasattr(act, 'fcurves'):
        for fc in act.fcurves:
            if fc.data_path == 'rotation_euler' and fc.array_index == 2:
                fc_z = fc
                break

    if fc_z:
        eval_frames = [1, 60, 120, 180, 240, 241, 242]
        evaluations = {str(f): float(fc_z.evaluate(float(f))) for f in eval_frames}
        rot_info["fcurve"] = {
            "extrapolation": fc_z.extrapolation,
            "evaluations": evaluations,
            "keyframe_points": len(fc_z.keyframe_points)
        }
output["globe_rotator"] = rot_info

# 6. Frame 76 Popping Adversarial Scan
popping_records = []

# Scan all actions in bpy.data.actions
for act in bpy.data.actions:
    fcurves_to_check = []
    if hasattr(act, 'layers'):
        for layer in getattr(act, 'layers', []):
            for strip in getattr(layer, 'strips', []):
                for cb in getattr(strip, 'channelbags', []):
                    fcurves_to_check.extend(cb.fcurves)
    if hasattr(act, 'fcurves'):
        fcurves_to_check.extend(act.fcurves)

    for fc in fcurves_to_check:
        if fc.data_path in ('hide_render', 'hide_viewport'):
            for kp in fc.keyframe_points:
                if abs(kp.co[0] - 76.0) < 1.0:
                    popping_records.append({
                        "source": "action",
                        "action": act.name,
                        "data_path": fc.data_path,
                        "frame": kp.co[0],
                        "value": kp.co[1]
                    })

# Scan all objects
for obj in bpy.data.objects:
    if obj.animation_data and obj.animation_data.action:
        act = obj.animation_data.action
        fcs = []
        if hasattr(act, 'layers'):
            for layer in getattr(act, 'layers', []):
                for strip in getattr(layer, 'strips', []):
                    for cb in getattr(strip, 'channelbags', []):
                        fcs.extend(cb.fcurves)
        if hasattr(act, 'fcurves'):
            fcs.extend(act.fcurves)
        for fc in fcs:
            if fc.data_path in ('hide_render', 'hide_viewport'):
                for kp in fc.keyframe_points:
                    if abs(kp.co[0] - 76.0) < 1.0:
                        popping_records.append({
                            "source": "object",
                            "object": obj.name,
                            "data_path": fc.data_path,
                            "frame": kp.co[0]
                        })

output["frame76_popping"] = {
    "count": len(popping_records),
    "records": popping_records
}

# 7. Four Orbiters Loop Closure & Motion
orbiters_list = ['MS_Orbiter_MRO', 'MS_Orbiter_Odyssey', 'MS_Orbiter_MAVEN', 'MS_Orbiter_GlobalSurveyor']
orbiters_data = {}
for name in orbiters_list:
    obj = bpy.data.objects.get(name)
    if not obj:
        orbiters_data[name] = {"exists": False}
        continue
    scene.frame_set(1)
    p1 = obj.matrix_world.translation.copy()
    scene.frame_set(60)
    p60 = obj.matrix_world.translation.copy()
    scene.frame_set(120)
    p120 = obj.matrix_world.translation.copy()
    scene.frame_set(240)
    p240 = obj.matrix_world.translation.copy()
    scene.frame_set(241)
    p241 = obj.matrix_world.translation.copy()
    
    delta_loop = (p241 - p1).length
    displacement_mid = (p120 - p1).length
    orbiters_data[name] = {
        "exists": True,
        "p1": [p1.x, p1.y, p1.z],
        "p120": [p120.x, p120.y, p120.z],
        "p240": [p240.x, p240.y, p240.z],
        "p241": [p241.x, p241.y, p241.z],
        "delta_loop": float(delta_loop),
        "displacement_mid": float(displacement_mid)
    }
output["orbiters"] = orbiters_data

# 8. Collection Visibility
cols_checked = {}
for cname in ["NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"]:
    c = bpy.data.collections.get(cname)
    cols_checked[cname] = {
        "exists": c is not None,
        "hide_viewport": c.hide_viewport if c else None,
        "hide_render": c.hide_render if c else None
    }
output["collections"] = cols_checked

print("CHALLENGE_DATA_BEGIN")
print(json.dumps(output))
print("CHALLENGE_DATA_END")
"""


class TestM9BlenderAdversarialChallenge(unittest.TestCase):
    """
    Adversarial challenge test suite verifying Milestone 9 master 3D blend integrity.
    """

    @classmethod
    def setUpClass(cls):
        if not os.path.isfile(BLENDER_BIN):
            raise unittest.SkipTest(f"Blender binary not found at: {BLENDER_BIN}")
        if not os.path.isfile(HERO_BLEND):
            raise unittest.SkipTest(f"Master scene not found at: {HERO_BLEND}")

        cmd = [
            BLENDER_BIN,
            "-b",
            HERO_BLEND,
            "--python-expr",
            BLENDER_EXTRACTOR_SCRIPT
        ]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        cls.raw_stdout = proc.stdout
        cls.raw_stderr = proc.stderr
        cls.returncode = proc.returncode

        if "CHALLENGE_DATA_BEGIN" not in proc.stdout:
            raise RuntimeError(f"Blender probe failed to execute cleanly:\n{proc.stdout}\n{proc.stderr}")

        json_text = proc.stdout.split("CHALLENGE_DATA_BEGIN")[1].split("CHALLENGE_DATA_END")[0].strip()
        cls.data = json.loads(json_text)

    # --------------------------------------------------------------------------
    # 1. MATERIAL NODE TREE ARCHITECTURE CHALLENGES
    # --------------------------------------------------------------------------

    def test_01_material_existence_and_nodes_enabled(self):
        """Probe Mars_NASA_HiRISE_Viking_Color exists and has active node tree."""
        mat = self.data.get("material", {})
        self.assertTrue(mat.get("exists"), "Material Mars_NASA_HiRISE_Viking_Color missing from blend")
        self.assertTrue(mat.get("use_nodes"), "Material Mars_NASA_HiRISE_Viking_Color does not have use_nodes enabled")

    def test_02_slope_factor_map_range_architecture(self):
        """Probe Slope_Factor Map Range node parameters, clamping, and inputs."""
        mat = self.data.get("material", {})
        sf = mat.get("slope_factor")
        self.assertIsNotNone(sf, "Slope_Factor node missing from Mars_NASA_HiRISE_Viking_Color")
        self.assertEqual(sf.get("type"), "MAP_RANGE", f"Slope_Factor type is {sf.get('type')}, expected MAP_RANGE")
        self.assertTrue(sf.get("clamp"), "Slope_Factor clamp must be enabled for bounded slope transition")

        # Slope angles: cos(15 deg) ~ 0.966, cos(25 deg) ~ 0.900
        self.assertAlmostEqual(sf.get("from_min"), 0.966, delta=0.01,
                               msg=f"Slope_Factor From Min ({sf.get('from_min')}) must match ~0.966 (15 deg slope)")
        self.assertAlmostEqual(sf.get("from_max"), 0.900, delta=0.01,
                               msg=f"Slope_Factor From Max ({sf.get('from_max')}) must match ~0.900 (25 deg slope)")
        self.assertEqual(sf.get("to_min"), 0.0, "Slope_Factor To Min must be 0.0")
        self.assertEqual(sf.get("to_max"), 1.0, "Slope_Factor To Max must be 1.0")

    def test_03_slope_factor_network_connections(self):
        """Probe that Slope_Factor is driven by Geometry normal Z and feeds both albedo & roughness blends."""
        links = self.data.get("material", {}).get("links", [])
        
        # Link 1: Geometry Normal -> SepXYZ -> Slope_Factor.Value
        has_geom_to_sep = any(l["from_node"] == "Slope_Geometry" and l["to_node"] == "Slope_SepXYZ" for l in links)
        self.assertTrue(has_geom_to_sep, "Missing link from Slope_Geometry to Slope_SepXYZ")

        has_sep_to_factor = any(l["from_node"] == "Slope_SepXYZ" and l["from_socket"] == "Z" and
                                l["to_node"] == "Slope_Factor" and l["to_socket"] == "Value" for l in links)
        self.assertTrue(has_sep_to_factor, "Missing link Slope_SepXYZ.Z -> Slope_Factor.Value")

        # Link 2: Slope_Factor.Result -> Slope_Albedo_Blend.Factor
        has_factor_to_albedo = any(l["from_node"] == "Slope_Factor" and l["to_node"] == "Slope_Albedo_Blend" and
                                   "Factor" in l["to_socket"] for l in links)
        self.assertTrue(has_factor_to_albedo, "Missing link Slope_Factor.Result -> Slope_Albedo_Blend.Factor")

        # Link 3: Slope_Factor.Result -> Slope_Roughness_Blend.Factor
        has_factor_to_roughness = any(l["from_node"] == "Slope_Factor" and l["to_node"] == "Slope_Roughness_Blend" and
                                      "Factor" in l["to_socket"] for l in links)
        self.assertTrue(has_factor_to_roughness, "Missing link Slope_Factor.Result -> Slope_Roughness_Blend.Factor")

    def test_04_rock_strata_wave_texture_architecture(self):
        """Probe Rock_Strata_Wave node: object coordinates, horizontal Z bands, ramp & bump feeding."""
        mat = self.data.get("material", {})
        rw = mat.get("rock_strata_wave")
        self.assertIsNotNone(rw, "Rock_Strata_Wave node missing from material")
        self.assertEqual(rw.get("type"), "TEX_WAVE", f"Rock_Strata_Wave type is {rw.get('type')}, expected TEX_WAVE")
        self.assertEqual(rw.get("wave_type"), "BANDS", "Rock_Strata_Wave wave_type must be BANDS")
        self.assertEqual(rw.get("bands_direction"), "Z", "Rock_Strata_Wave bands_direction must be Z for horizontal strata")

        links = mat.get("links", [])
        # Input Vector driven by Texture Coordinate.Object
        has_obj_coord = any(l["from_node"] == "Texture Coordinate" and l["from_socket"] == "Object" and
                            l["to_node"] == "Rock_Strata_Wave" for l in links)
        self.assertTrue(has_obj_coord, "Rock_Strata_Wave must use Texture Coordinate.Object to avoid UV distortion")

        # Color fed to Rock_Color_Ramp
        has_color_ramp = any(l["from_node"] == "Rock_Strata_Wave" and l["to_node"] == "Rock_Color_Ramp" for l in links)
        self.assertTrue(has_color_ramp, "Rock_Strata_Wave must feed Rock_Color_Ramp")

        # Factor fed to Rock_Strata_Bump
        has_strata_bump = any(l["from_node"] == "Rock_Strata_Wave" and l["to_node"] == "Rock_Strata_Bump" and
                              "Height" in l["to_socket"] for l in links)
        self.assertTrue(has_strata_bump, "Rock_Strata_Wave must feed Rock_Strata_Bump.Height")

    def test_05_slope_albedo_and_roughness_blends_into_principled_bsdf(self):
        """Probe Slope_Albedo_Blend -> Base Color and Slope_Roughness_Blend -> Roughness."""
        links = self.data.get("material", {}).get("links", [])

        # Slope_Albedo_Blend -> Principled BSDF.Base Color
        has_albedo_to_bsdf = any(l["from_node"] == "Slope_Albedo_Blend" and l["to_node"] == "Principled BSDF" and
                                 l["to_socket"] == "Base Color" for l in links)
        self.assertTrue(has_albedo_to_bsdf, "Slope_Albedo_Blend.Result must connect to Principled BSDF.Base Color")

        # Slope_Roughness_Blend -> Principled BSDF.Roughness
        has_roughness_to_bsdf = any(l["from_node"] == "Slope_Roughness_Blend" and l["to_node"] == "Principled BSDF" and
                                    l["to_socket"] == "Roughness" for l in links)
        self.assertTrue(has_roughness_to_bsdf, "Slope_Roughness_Blend.Result must connect to Principled BSDF.Roughness")

        # Check cliff roughness value
        mat = self.data.get("material", {})
        srb = mat.get("slope_roughness_blend", {})
        cliff_roughness = srb.get("cliff_roughness")
        self.assertIsNotNone(cliff_roughness, "Slope_Roughness_Blend input B (cliff roughness) not set")
        self.assertAlmostEqual(cliff_roughness, 0.52, delta=0.05,
                               msg=f"Cliff roughness is {cliff_roughness}, expected ~0.52")

    def test_06_micro_normal_bump_chain_integrity(self):
        """Adversarially probe 4-stage micro-normal bump chain into Principled BSDF."""
        mat = self.data.get("material", {})
        bump_chain = mat.get("bump_chain", {})
        for node_key in ["Bump_exists", "Bump001_exists", "Rock_Strata_Bump_exists", "Bump002_exists", "Principled_BSDF_exists"]:
            self.assertTrue(bump_chain.get(node_key), f"Bump chain node missing: {node_key}")

        links = mat.get("links", [])
        
        # Link: Bump.Normal -> Bump.001.Normal
        self.assertTrue(any(l["from_node"] == "Bump" and l["to_node"] == "Bump.001" and l["to_socket"] == "Normal" for l in links),
                        "Bump.Normal must link to Bump.001.Normal")

        # Link: Bump.001.Normal -> Rock_Strata_Bump.Normal
        self.assertTrue(any(l["from_node"] == "Bump.001" and l["to_node"] == "Rock_Strata_Bump" and l["to_socket"] == "Normal" for l in links),
                        "Bump.001.Normal must link to Rock_Strata_Bump.Normal")

        # Link: Rock_Strata_Bump.Normal -> Bump.002.Normal
        self.assertTrue(any(l["from_node"] == "Rock_Strata_Bump" and l["to_node"] == "Bump.002" and l["to_socket"] == "Normal" for l in links),
                        "Rock_Strata_Bump.Normal must link to Bump.002.Normal")

        # Link: Bump.002.Normal -> Principled BSDF.Normal
        self.assertTrue(any(l["from_node"] == "Bump.002" and l["to_node"] == "Principled BSDF" and l["to_socket"] == "Normal" for l in links),
                        "Bump.002.Normal must link to Principled BSDF.Normal")

    def test_07_packed_images_datablocks_invariant(self):
        """Assert zero external image file datablocks and exactly 276 packed images."""
        imgs = self.data.get("images", {})
        total_count = imgs.get("total_count", 0)
        unpacked = imgs.get("unpacked_count", 0)

        self.assertEqual(total_count, 276, f"Expected exactly 276 packed images, found {total_count}")
        self.assertEqual(unpacked, 0, f"Found {unpacked} unpacked external images! All images must be packed.")

    # --------------------------------------------------------------------------
    # 2. TIMELINE & CONTINUOUS ROTATION ADVERSARIAL CHALLENGES
    # --------------------------------------------------------------------------

    def test_08_timeline_bounds_and_frame_rate(self):
        """Probe timeline frame range [1..240] and standard 24 fps."""
        tl = self.data.get("timeline", {})
        self.assertEqual(tl.get("frame_start"), 1, "Timeline frame_start must be 1")
        self.assertEqual(tl.get("frame_end"), 240, "Timeline frame_end must be 240")
        self.assertEqual(tl.get("fps"), 24, "Timeline FPS must be 24")
        self.assertEqual(tl.get("fps_base"), 1.0, "Timeline FPS base must be 1.0")

    def test_09_globe_rotator_extrapolation_and_angular_velocity(self):
        """
        Adversarially evaluate MARS_NEW_Globe_Rotator FCurve across frames:
        - Linear extrapolation
        - Constant angular velocity: dtheta/dt = const within 1e-5
        - Continuous zero-snap wrap-around: (theta(241) - theta(1)) % (2*pi) == 0.0
        """
        rot = self.data.get("globe_rotator", {})
        self.assertTrue(rot.get("exists"), "MARS_NEW_Globe_Rotator object missing")
        fc = rot.get("fcurve")
        self.assertIsNotNone(fc, "MARS_NEW_Globe_Rotator missing rotation_euler[2] FCurve")

        # Check extrapolation
        self.assertEqual(fc.get("extrapolation"), "LINEAR", "Rotator FCurve extrapolation must be LINEAR")

        ev = fc.get("evaluations", {})
        frames = [1, 60, 120, 180, 240, 241, 242]
        for f in frames:
            self.assertIn(str(f), ev, f"Missing evaluation at frame {f}")

        t1 = ev["1"]
        t60 = ev["60"]
        t120 = ev["120"]
        t180 = ev["180"]
        t240 = ev["240"]
        t241 = ev["241"]
        t242 = ev["242"]

        # Expected nominal angular velocity: 2*pi / 240 rad/frame
        nominal_omega = (2.0 * math.pi) / 240.0

        # Step velocities
        omega_1_60 = (t60 - t1) / 59.0
        omega_60_120 = (t120 - t60) / 60.0
        omega_120_180 = (t180 - t120) / 60.0
        omega_180_240 = (t240 - t180) / 60.0
        omega_240_241 = (t241 - t240) / 1.0
        omega_241_242 = (t242 - t241) / 1.0

        omegas = [omega_1_60, omega_60_120, omega_120_180, omega_180_240, omega_240_241, omega_241_242]
        for idx, w in enumerate(omegas):
            diff = abs(w - nominal_omega)
            self.assertLess(diff, 1e-5,
                            f"Angular velocity variation at segment {idx}: {w} vs nominal {nominal_omega} (delta: {diff})")

        # Zero-snap loop closure: (theta(241) - theta(1)) mod 2*pi == 0.0
        delta_total = t241 - t1
        loop_closure_error = abs((delta_total) - (2.0 * math.pi))
        self.assertLess(loop_closure_error, 1e-5,
                        f"Loop closure error: {loop_closure_error} (total angle {delta_total} vs 2*pi)")

        # Verify seamless loop transition: frame 240 to frame 1
        # When wrapping from frame 240 to frame 1, the advance is (t1 + 2*pi - t240)
        wrap_step = (t1 + 2.0 * math.pi) - t240
        self.assertAlmostEqual(wrap_step, nominal_omega, delta=1e-5,
                               msg=f"Wrap-around step from frame 240 to 1 ({wrap_step}) deviates from nominal step ({nominal_omega})")

    # --------------------------------------------------------------------------
    # 3. FRAME-76 POPPING DEFECT ADVERSARIAL SCAN
    # --------------------------------------------------------------------------

    def test_10_exhaustive_frame76_popping_scan(self):
        """
        Adversarially scan 100% of actions and objects in MARS_SENTINEL_HERO_PRODUCTION.blend.
        Assert ZERO lingering keyframes on hide_render or hide_viewport at frame 76.
        """
        f76_data = self.data.get("frame76_popping", {})
        popping_count = f76_data.get("count", -1)
        records = f76_data.get("records", [])

        self.assertEqual(popping_count, 0,
                         f"Found {popping_count} lingering visibility popping keyframes at frame 76:\n{json.dumps(records, indent=2)}")

    # --------------------------------------------------------------------------
    # 4. ORBITERS & DATASET INVARIANTS CHALLENGES
    # --------------------------------------------------------------------------

    def test_11_four_orbiters_seamless_loop_and_active_motion(self):
        """
        Assert all 4 orbiters (MRO, Odyssey, MAVEN, Global Surveyor):
        - Exist and evaluate in world space
        - Move along their orbits (||p(120) - p(1)|| > 10.0)
        - Loop seamlessly: ||p(241) - p(1)|| < 1e-4
        """
        orbiters = self.data.get("orbiters", {})
        expected_orbiters = ['MS_Orbiter_MRO', 'MS_Orbiter_Odyssey', 'MS_Orbiter_MAVEN', 'MS_Orbiter_GlobalSurveyor']

        for name in expected_orbiters:
            self.assertIn(name, orbiters, f"Orbiter {name} missing from scene data")
            o_data = orbiters[name]
            self.assertTrue(o_data.get("exists"), f"Orbiter object {name} does not exist in scene")

            # Check loop closure
            delta_loop = o_data.get("delta_loop", 999.0)
            self.assertLess(delta_loop, 1e-4,
                            f"Orbiter {name} loop closure defect: delta ||p(241) - p(1)|| = {delta_loop} (expected < 1e-4)")

            # Check dynamic motion (cannot be static)
            displacement = o_data.get("displacement_mid", 0.0)
            self.assertGreater(displacement, 10.0,
                               f"Orbiter {name} appears stationary: displacement at mid-orbit = {displacement}")

    def test_12_hirise_coordinates_sha256_hash_preservation(self):
        """Verify NASA HiRISE Jezero mesh coordinates SHA-256 hash equals ed5e2c9e...54dd."""
        hirise = self.data.get("hirise", {})
        self.assertTrue(hirise.get("exists"), "NASA_HiRISE_DTM_Jezero_FieldSite_4m missing from scene")
        self.assertEqual(hirise.get("verts"), HIRISE_EXPECTED_VERTS,
                         f"HiRISE vertex count altered: {hirise.get('verts')} vs {HIRISE_EXPECTED_VERTS}")
        self.assertEqual(hirise.get("sha256"), HIRISE_EXPECTED_SHA256,
                         f"HiRISE SHA-256 hash altered: {hirise.get('sha256')} vs {HIRISE_EXPECTED_SHA256}")

    def test_13_collections_unhidden_in_viewport_and_render(self):
        """Verify collections NEW_Mars_Globe and MS_Mars_Orbital_Fleet are visible."""
        cols = self.data.get("collections", {})
        for cname in ["NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"]:
            self.assertIn(cname, cols, f"Collection {cname} missing from scene")
            c_info = cols[cname]
            self.assertTrue(c_info.get("exists"), f"Collection {cname} does not exist")
            self.assertFalse(c_info.get("hide_viewport"), f"Collection {cname} is hidden in viewport")
            self.assertFalse(c_info.get("hide_render"), f"Collection {cname} is hidden in render")

    def test_14_zero_blend1_backup_files(self):
        """Assert zero .blend1 backup files exist in the entire workspace directory tree."""
        backup_files = []
        for root, dirs, files in os.walk(PROJECT_ROOT):
            # Skip .git directory
            if ".git" in root.split(os.sep):
                continue
            for f in files:
                if f.endswith(".blend1") or ".blend@" in f:
                    backup_files.append(os.path.join(root, f))

        self.assertEqual(len(backup_files), 0,
                         f"Found {len(backup_files)} .blend1 backup files:\n" + "\n".join(backup_files))


if __name__ == "__main__":
    unittest.main(verbosity=2)
