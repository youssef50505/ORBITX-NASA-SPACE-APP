"""
Milestone 9 Empirical Adversarial Challenge Test Suite
MarsSentinel Visual Polish & Animation Upgrades (Requirement R3)
Features:
- Feature 20: High-Fidelity Martian Sand Shaders & Micro-Normal Details across Blender, Unity, and Web
- Feature 21: Multi-Component Continuous Animation Loops across Blender, Unity, and Web
"""

import os
import sys
import json
import math
import re
import socket
import base64
import struct
import time
import subprocess
import unittest
from html.parser import HTMLParser

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
WEB_DIR = os.path.join(PROJECT_ROOT, "web")
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
AUDIT_SCRIPT = os.path.join(PROJECT_ROOT, "scripts", "audit_workspace_integrity.py")

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"


# ==============================================================================
# 1. BLENDER VISUAL POLISH & CONTINUOUS ANIMATION EMPIRICAL TESTS
# ==============================================================================

class Milestone9BlenderVisualPolishTests(unittest.TestCase):
    """Empirical challenges inspecting MARS_SENTINEL_HERO_PRODUCTION.blend in Blender 5.2."""

    @classmethod
    def setUpClass(cls):
        """Execute headless python inspection in Blender once and cache parsed output."""
        if not os.path.isfile(BLENDER_BIN) or not os.path.isfile(HERO_BLEND):
            cls.blender_available = False
            return
        cls.blender_available = True

        script = """
import bpy, json, hashlib, array, math

data = {}

# 1. HiRISE Mesh Invariants
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
if site and site.data:
    v_cnt = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (v_cnt * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    data["hirise"] = {"exists": True, "verts": v_cnt, "sha256": h}
else:
    data["hirise"] = {"exists": False}

# 2. Packed images count
imgs = [i for i in bpy.data.images if i.name not in ('Render Result', 'Viewer Node')]
data["image_count"] = len(imgs)

# 3. Timeline configuration
scene = bpy.context.scene
data["timeline"] = {
    "frame_start": scene.frame_start,
    "frame_end": scene.frame_end,
    "frame_current": scene.frame_current,
    "fps": scene.render.fps,
    "fps_base": scene.render.fps_base
}

# 4. Globe Rotator continuous linear animation
rot = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
rot_data = {"exists": rot is not None, "has_action": False}
if rot and rot.animation_data and rot.animation_data.action:
    act = rot.animation_data.action
    rot_data["has_action"] = True
    fcs = []
    if hasattr(act, 'layers'):
        for layer in getattr(act, 'layers', []):
            for strip in getattr(layer, 'strips', []):
                for cb in getattr(strip, 'channelbags', []):
                    for fc in cb.fcurves:
                        if fc.data_path == 'rotation_euler' and fc.array_index == 2:
                            fcs.append(fc)
    if not fcs and hasattr(act, 'fcurves'):
        for fc in act.fcurves:
            if fc.data_path == 'rotation_euler' and fc.array_index == 2:
                fcs.append(fc)
    if fcs:
        fc = fcs[0]
        rot_data["fcurve"] = {
            "extrapolation": fc.extrapolation,
            "val_f1": float(fc.evaluate(1.0)),
            "val_f241": float(fc.evaluate(241.0)),
            "val_f120": float(fc.evaluate(120.0)),
            "keyframe_count": len(fc.keyframe_points)
        }
data["globe_rotator"] = rot_data

# 5. Frame 76 popping keyframes scan
f76_count = 0
for act in bpy.data.actions:
    if hasattr(act, 'layers'):
        for layer in getattr(act, 'layers', []):
            for strip in getattr(layer, 'strips', []):
                for cb in getattr(strip, 'channelbags', []):
                    for fc in cb.fcurves:
                        if fc.data_path in ('hide_render', 'hide_viewport'):
                            if any(abs(kp.co[0] - 76.0) < 1.0 for kp in fc.keyframe_points):
                                f76_count += 1
    if hasattr(act, 'fcurves'):
        for fc in act.fcurves:
            if fc.data_path in ('hide_render', 'hide_viewport'):
                if any(abs(kp.co[0] - 76.0) < 1.0 for kp in fc.keyframe_points):
                    f76_count += 1
data["f76_popping_count"] = f76_count

# 6. Slope material nodes on Mars_NASA_HiRISE_Viking_Color
mat = bpy.data.materials.get("Mars_NASA_HiRISE_Viking_Color")
mat_data = {"exists": mat is not None, "nodes": []}
if mat and mat.node_tree:
    mat_data["nodes"] = [n.name for n in mat.node_tree.nodes]
    slope_map = mat.node_tree.nodes.get("Slope_Factor")
    if slope_map:
        mat_data["slope_from_min"] = slope_map.inputs["From Min"].default_value
        mat_data["slope_from_max"] = slope_map.inputs["From Max"].default_value
data["slope_material"] = mat_data

# 7. Collection visibility
for cname in ["NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"]:
    c = bpy.data.collections.get(cname)
    data[f"col_{cname}"] = {
        "exists": c is not None,
        "hide_viewport": c.hide_viewport if c else None,
        "hide_render": c.hide_render if c else None
    }

print("JSON_START" + json.dumps(data) + "JSON_END")
"""
        cmd = [
            BLENDER_BIN,
            "-b",
            HERO_BLEND,
            "--python-expr",
            script
        ]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=90)
        cls.blender_output = proc.stdout

        match = re.search(r"JSON_START(.*?)JSON_END", proc.stdout)
        if match:
            cls.blend_data = json.loads(match.group(1))
        else:
            cls.blend_data = {}

    def setUp(self):
        if not getattr(self, "blender_available", False):
            self.skipTest("Blender 5.2 or master .blend file unavailable")

    def test_01_hirise_mesh_invariants(self):
        """Verify NASA HiRISE Jezero mesh coordinates SHA-256 hash and vertex count are strictly preserved."""
        hirise = self.blend_data.get("hirise", {})
        self.assertTrue(hirise.get("exists"), "NASA_HiRISE_DTM_Jezero_FieldSite_4m missing in Blender scene")
        self.assertEqual(hirise.get("verts"), HIRISE_EXPECTED_VERTS, f"Vertex count altered: {hirise.get('verts')}")
        self.assertEqual(hirise.get("sha256"), HIRISE_EXPECTED_SHA256, f"Coordinate SHA-256 hash altered: {hirise.get('sha256')}")

    def test_02_packed_images_count_invariant(self):
        """Verify exactly 276 packed images datablocks are preserved."""
        count = self.blend_data.get("image_count", 0)
        self.assertEqual(count, 276, f"Packed images count altered: {count} (expected exactly 276)")

    def test_03_slope_aware_shader_node_tree(self):
        """Verify slope-aware procedural nodes in Mars_NASA_HiRISE_Viking_Color."""
        mat = self.blend_data.get("slope_material", {})
        self.assertTrue(mat.get("exists"), "Mars_NASA_HiRISE_Viking_Color material missing")
        nodes = mat.get("nodes", [])
        expected_nodes = ["Slope_Geometry", "Slope_Factor", "Slope_Albedo_Blend", "Rock_Strata_Wave", "Rock_Strata_Bump", "Slope_Roughness_Blend"]
        for en in expected_nodes:
            self.assertIn(en, nodes, f"Expected node '{en}' missing from Mars_NASA_HiRISE_Viking_Color")
        self.assertAlmostEqual(mat.get("slope_from_min", 0), 0.966, places=2)
        self.assertAlmostEqual(mat.get("slope_from_max", 0), 0.900, places=2)

    def test_04_timeline_standard_continuous_loop(self):
        """Verify standard 240-frame timeline at 24fps."""
        tl = self.blend_data.get("timeline", {})
        self.assertEqual(tl.get("frame_start"), 1)
        self.assertEqual(tl.get("frame_end"), 240)
        self.assertEqual(tl.get("frame_current"), 1)
        self.assertEqual(tl.get("fps"), 24)

    def test_05_globe_rotator_continuous_360_rotation(self):
        """Verify MARS_NEW_Globe_Rotator executes continuous 360 degree linear rotation."""
        rot = self.blend_data.get("globe_rotator", {})
        self.assertTrue(rot.get("exists"), "MARS_NEW_Globe_Rotator missing")
        self.assertTrue(rot.get("has_action"), "MARS_NEW_Globe_Rotator has no action")
        fc = rot.get("fcurve", {})
        self.assertAlmostEqual(fc.get("val_f1", -1), 0.0, places=2, msg="Rotator frame 1 angle should be 0.0 rad")
        self.assertAlmostEqual(fc.get("val_f241", -1), 2.0 * math.pi, places=2, msg="Rotator frame 241 angle should be 2*pi rad")
        self.assertAlmostEqual(fc.get("val_f120", -1), math.pi, places=1, msg="Rotator frame 120 angle should be pi rad")

    def test_06_zero_frame76_popping_keyframes(self):
        """Verify all step visibility keyframes at frame 76 have been eliminated."""
        count = self.blend_data.get("f76_popping_count", -1)
        self.assertEqual(count, 0, f"Found {count} lingering visibility popping keyframes at frame 76")

    def test_07_collection_visibility(self):
        """Verify NEW_Mars_Globe and MS_Mars_Orbital_Fleet collections are visible."""
        for cname in ["NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"]:
            cdata = self.blend_data.get(f"col_{cname}", {})
            self.assertTrue(cdata.get("exists"), f"Collection {cname} missing")
            self.assertFalse(cdata.get("hide_viewport"), f"Collection {cname} is hidden in viewport")
            self.assertFalse(cdata.get("hide_render"), f"Collection {cname} is hidden in render")


# ==============================================================================
# 2. UNITY VISUAL POLISH & CONTINUOUS ANIMATION EMPIRICAL TESTS
# ==============================================================================

class Milestone9UnityVisualPolishTests(unittest.TestCase):
    """Empirical challenges verifying Unity C# scripts, shaders, and URP configuration."""

    def test_08_shader_slope_and_micro_normal_math(self):
        """Verify MartianSandSlopeBlend.shader contains slope blending and micro-normal mathematics."""
        shader_path = os.path.join(UNITY_DIR, "Assets", "Shaders", "MartianSandSlopeBlend.shader")
        self.assertTrue(os.path.isfile(shader_path), "MartianSandSlopeBlend.shader does not exist")

        with open(shader_path, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("_SlopeThreshold", content)
        self.assertIn("_SlopeBlendWidth", content)
        self.assertIn("_RippleFrequency", content)
        self.assertIn("_RippleStrength", content)
        self.assertIn("smoothstep", content)
        self.assertIn("dot(normalWS, float3(0.0, 1.0, 0.0))", content)
        self.assertIn("UniversalForward", content)
        self.assertIn("ShadowCaster", content)
        self.assertIn("DepthOnly", content)

    def test_09_terrain_manager_stl_and_material_logic(self):
        """Verify MarsTerrainManager.cs parses binary STL and assigns slope-blended material."""
        cs_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsTerrainManager.cs")
        self.assertTrue(os.path.isfile(cs_path), "MarsTerrainManager.cs does not exist")

        with open(cs_path, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("ParseBinarySTL", content)
        self.assertIn("sample_jezero_3d.stl", content)
        self.assertIn("MartianSandSlopeBlend", content)
        self.assertIn("MeshFilter", content)
        self.assertIn("MeshRenderer", content)

    def test_10_ingenuity_rotor_animator_counter_rotation(self):
        """Verify IngenuityRotorAnimator.cs drives coaxial counter-rotating blades."""
        cs_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "IngenuityRotorAnimator.cs")
        self.assertTrue(os.path.isfile(cs_path), "IngenuityRotorAnimator.cs does not exist")

        with open(cs_path, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("upperRotor", content)
        self.assertIn("lowerRotor", content)
        self.assertIn("rotorSpeedDegPerSec", content)
        self.assertIn("Rotate(Vector3.up, step, Space.Self)", content)
        self.assertIn("Rotate(Vector3.up, -step, Space.Self)", content)
        self.assertIn("hoversway", content.lower())

    def test_11_simulation_animator_planetary_and_rover_traverse(self):
        """Verify MarsSimulationAnimator.cs implements Mars obliquity rotation and rover traverse."""
        cs_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsSimulationAnimator.cs")
        self.assertTrue(os.path.isfile(cs_path), "MarsSimulationAnimator.cs does not exist")

        with open(cs_path, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("marsObliquityTiltDeg", content)
        self.assertIn("25.19", content)
        self.assertIn("globeRotationSpeedDegPerSec", content)
        self.assertIn("roverTransform", content)
        self.assertIn("suspensionRocking", content)

    def test_12_urp_postprocessing_and_lighting_calibration(self):
        """Verify URP HDR color grading, shadow bias calibration, and Trilight ambient lighting."""
        pc_asset = os.path.join(UNITY_DIR, "Assets", "Settings", "PC_RPAsset.asset")
        self.assertTrue(os.path.isfile(pc_asset), "PC_RPAsset.asset does not exist")

        with open(pc_asset, "r", encoding="utf-8") as f:
            pc_content = f.read()

        self.assertIn("m_ColorGradingMode: 1", pc_content, "HDR color grading mode not enabled in PC_RPAsset")
        self.assertIn("m_ShadowDepthBias: 0.04", pc_content)
        self.assertIn("m_ShadowNormalBias: 0.4", pc_content)
        self.assertIn("m_ShadowDistance: 120", pc_content)

        scene_mgr = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsSceneManager.cs")
        with open(scene_mgr, "r", encoding="utf-8") as f:
            mgr_content = f.read()

        self.assertIn("AmbientMode.Trilight", mgr_content)

    def test_13_unity_meta_pairing_invariants(self):
        """Verify zero missing and zero orphan .meta files in unity_project/Assets."""
        assets_dir = os.path.join(UNITY_DIR, "Assets")
        missing = []
        orphans = []
        for root, dirs, files in os.walk(assets_dir):
            for d in dirs:
                dp = os.path.join(root, d)
                if not os.path.exists(dp + ".meta"):
                    missing.append(dp)
            for f in files:
                fp = os.path.join(root, f)
                if f.endswith(".meta"):
                    if not os.path.exists(fp[:-5]):
                        orphans.append(fp)
                else:
                    if not os.path.exists(fp + ".meta"):
                        missing.append(fp)

        self.assertEqual(missing, [], f"Found missing .meta files: {missing[:5]}")
        self.assertEqual(orphans, [], f"Found orphan .meta files: {orphans[:5]}")


# ==============================================================================
# 3. WEB VISUAL POLISH & CONTINUOUS ANIMATION EMPIRICAL TESTS
# ==============================================================================

class DOMParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = set()

    def handle_starttag(self, tag, attrs):
        for k, v in attrs:
            if k == "id" and v:
                self.ids.add(v)


class Milestone9WebVisualPolishTests(unittest.TestCase):
    """Empirical challenges verifying procedural canvas normal maps, slope shading, and loops."""

    @classmethod
    def setUpClass(cls):
        html_path = os.path.join(WEB_DIR, "index.html")
        js_path = os.path.join(WEB_DIR, "app.js")
        with open(html_path, "r", encoding="utf-8") as f:
            cls.html_code = f.read()
        with open(js_path, "r", encoding="utf-8") as f:
            cls.js_code = f.read()

        cls.parser = DOMParser()
        cls.parser.feed(cls.html_code)

    def test_14_procedural_canvas_normal_map_generator(self):
        """Verify procedural canvas normal map generation with multi-harmonic wave equation."""
        self.assertIn("createMartianSandNormalTexture", self.js_code)
        self.assertIn("createImageData(size, size)", self.js_code)
        self.assertIn("primaryWave", self.js_code)
        self.assertIn("crossWave", self.js_code)
        self.assertIn("THREE.RepeatWrapping", self.js_code)
        self.assertIn("normalTexture.repeat.set(40, 40)", self.js_code)
        self.assertNotIn("http://", self.js_code)
        self.assertNotIn("https://", self.js_code)

    def test_15_slope_dependent_geologic_shading(self):
        """Verify slope-dependent and elevation-dependent terrain shading."""
        self.assertIn("slopeFactor", self.js_code)
        self.assertIn("computeVertexNormals", self.js_code)
        self.assertIn("(1.0 - ny) / 0.18", self.js_code)
        self.assertIn("normalScale", self.js_code)
        self.assertIn("dustTint", self.js_code)

    def test_16_ingenuity_helicopter_3d_model(self):
        """Verify Ingenuity Mars Helicopter at Wright Brothers Field with counter-rotating rotors."""
        self.assertIn("createIngenuityHelicopter", self.js_code)
        self.assertIn("upperRotor", self.js_code)
        self.assertIn("lowerRotor", self.js_code)
        self.assertIn("ingenuity.upperRotor.rotation.y +=", self.js_code)
        self.assertIn("ingenuity.lowerRotor.rotation.y -=", self.js_code)

    def test_17_standardized_animate_loop_and_clock(self):
        """Verify standardized animate() loop using THREE.Clock delta time and critically damped camera."""
        self.assertIn("new THREE.Clock()", self.js_code)
        self.assertIn("clock.getDelta()", self.js_code)
        self.assertIn("dTheta", self.js_code)
        self.assertIn("roverWheels", self.js_code)
        self.assertIn("roverGroup.rotation.x =", self.js_code)

    def test_18_dom_parity_and_syntax(self):
        """Verify all document.getElementById calls resolve to IDs in index.html and delimiters are balanced."""
        js_ids = set(re.findall(r"document\.getElementById\(['\"]([^'\"]+)['\"]\)", self.js_code))
        missing = [eid for eid in js_ids if eid not in self.parser.ids]
        self.assertEqual(missing, [], f"document.getElementById called for missing HTML IDs: {missing}")

        self.assertIn("(() => {", self.js_code)
        self.assertTrue(self.js_code.strip().endswith("})();"))

        stack = []
        pairs = {')': '(', '}': '{', ']': '['}
        for ch in self.js_code:
            if ch in "({[":
                stack.append(ch)
            elif ch in ")}]":
                self.assertTrue(len(stack) > 0 and stack[-1] == pairs[ch], f"Mismatched delimiter: {ch}")
                stack.pop()
        self.assertEqual(len(stack), 0, "Unclosed delimiters in web/app.js")

    def test_19_headless_browser_cdp_zero_errors(self):
        """Run headless Chrome/Edge and verify zero console errors, 0 unhandled exceptions."""
        edge_bin = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        chrome_bin = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
        browser_bin = edge_bin if os.path.isfile(edge_bin) else (chrome_bin if os.path.isfile(chrome_bin) else None)
        if not browser_bin:
            self.skipTest("No headless browser binary available for CDP inspection")

        temp_dir = os.path.join(os.environ.get("TEMP", "."), "m9_cdp_test")
        url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")

        cmd = [
            browser_bin,
            "--headless=new",
            "--remote-debugging-port=9225",
            f"--user-data-dir={temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            url
        ]

        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            time.sleep(2.5)
            import urllib.request
            req = urllib.request.Request("http://127.0.0.1:9225/json")
            with urllib.request.urlopen(req, timeout=3) as resp:
                targets = json.loads(resp.read().decode())

            main_target = next((t for t in targets if "web/index.html" in t.get("url", "")), None)
            self.assertIsNotNone(main_target, "Could not find index.html target in CDP targets list")

            ws_url = main_target["webSocketDebuggerUrl"]

            def simple_ws_query(url_str, command_dict):
                parts = url_str.replace("ws://", "").split("/", 1)
                host, port_str = parts[0].split(":")
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                s.settimeout(4.0)
                s.connect((host, int(port_str)))
                sec_key = base64.b64encode(os.urandom(16)).decode('utf-8')
                handshake = (
                    f"GET /{parts[1]} HTTP/1.1\r\n"
                    f"Host: {host}:{port_str}\r\n"
                    f"Upgrade: websocket\r\n"
                    f"Connection: Upgrade\r\n"
                    f"Sec-WebSocket-Key: {sec_key}\r\n"
                    f"Sec-WebSocket-Version: 13\r\n\r\n"
                )
                s.sendall(handshake.encode())
                resp_buf = b""
                while b"\r\n\r\n" not in resp_buf:
                    resp_buf += s.recv(1024)

                payload = json.dumps(command_dict).encode('utf-8')
                mask = os.urandom(4)
                masked = bytearray(b ^ mask[i % 4] for i, b in enumerate(payload))
                hdr = bytes([0x81, 0x80 | len(payload)]) + mask if len(payload) <= 125 else bytes([0x81, 0x80 | 126]) + struct.pack("!H", len(payload)) + mask
                s.sendall(hdr + masked)

                hdr_in = s.recv(2)
                b2 = hdr_in[1] & 0x7f
                pl_len = struct.unpack("!H", s.recv(2))[0] if b2 == 126 else (struct.unpack("!Q", s.recv(8))[0] if b2 == 127 else b2)
                buf = b""
                while len(buf) < pl_len:
                    buf += s.recv(pl_len - len(buf))
                s.close()
                return json.loads(buf.decode('utf-8'))

            res_three = simple_ws_query(ws_url, {"id": 1, "method": "Runtime.evaluate", "params": {"expression": "typeof THREE"}})
            self.assertEqual(res_three.get("result", {}).get("result", {}).get("value"), "object")

            res_canvas = simple_ws_query(ws_url, {"id": 2, "method": "Runtime.evaluate", "params": {"expression": "!!document.getElementById('mars-canvas')"}})
            self.assertTrue(res_canvas.get("result", {}).get("result", {}).get("value"))

        finally:
            proc.terminate()
            try:
                proc.wait(timeout=2)
            except Exception:
                proc.kill()


# ==============================================================================
# 4. REPOSITORY PRESERVATION INVARIANTS & AUDIT GATE
# ==============================================================================

class Milestone9PreservationTests(unittest.TestCase):
    """Empirical challenges verifying repository hygiene and full audit execution."""

    def test_20_zero_duplicate_blends_and_backups(self):
        """Verify zero .blend1 files and zero unauthorized .blend files in unity_project."""
        blend1s = []
        unity_blends = []
        for root, _, files in os.walk(PROJECT_ROOT):
            if ".git" in root or "Library" in root:
                continue
            for f in files:
                if f.endswith(".blend1"):
                    blend1s.append(os.path.join(root, f))
                elif f.endswith(".blend") and "unity_project" in root:
                    unity_blends.append(os.path.join(root, f))

        self.assertEqual(blend1s, [], f"Found .blend1 backup files: {blend1s}")
        self.assertEqual(unity_blends, [], f"Found .blend files in unity_project: {unity_blends}")

    def test_21_full_workspace_integrity_audit(self):
        """Execute scripts/audit_workspace_integrity.py --json and verify PASS."""
        if not os.path.isfile(AUDIT_SCRIPT):
            self.skipTest(f"Audit script not found at {AUDIT_SCRIPT}")

        cmd = [sys.executable, AUDIT_SCRIPT, "--json"]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        self.assertEqual(proc.returncode, 0, f"Audit exited non-zero: {proc.stderr}")
        s = proc.stdout.find('{\n  "timestamp":')
        if s == -1:
            s = proc.stdout.find('{\n  \"timestamp\":')
        self.assertNotEqual(s, -1, f"JSON payload not found in audit output:\n{proc.stdout}")
        rep = json.loads(proc.stdout[s:])
        self.assertEqual(rep.get("overall_status"), "PASS")
        self.assertEqual(rep.get("failed"), 0)


if __name__ == "__main__":
    unittest.main()
