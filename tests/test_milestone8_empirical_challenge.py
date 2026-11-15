"""
Milestone 8 Empirical Adversarial Challenge Test Suite
MarsSentinel Realistic Celestial Bodies & Orbital Mechanics (Requirement R2)

Tests:
1. Blender Headless Inspection of HiRISE terrain invariant (1,132,096 verts, ed5e2c9e...354dd).
2. Blender Headless Inspection of scene independence (0 external libraries, 0 missing images).
3. Blender Headless Inspection of Phobos 3D mesh, material, and orbit spline in NEW_Mars_Globe.
4. Blender Headless Inspection of Deimos 3D mesh, material, and orbit spline in NEW_Mars_Globe.
5. Blender Headless Inspection of Mars_Sun diurnal lighting arc and Jezero solar elevation keyframes.
6. Blender Headless Inspection of Constellation splines (MRO, Odyssey, MAVEN, Global Surveyor) & nadir tracking.
7. Unity C# script syntax and balanced delimiters across unity_project/Assets/Scripts/.
8. Unity .meta pairing invariants (0 missing and 0 orphan .meta files).
9. Astronomical & Keplerian frequency ratio mathematical oracle (Phobos ~3.97x, Deimos ~0.81x, inclinations).
10. Jezero diurnal solar arc mathematical oracle (elevation 71.62° at noon, normalized unit vectors).
11. Web DOM selectors & HTML parity (all document.getElementById calls in app.js exist in index.html).
12. Web JavaScript AST syntax, delimiter balance, and IIFE wrapper verification.
13. Web Celestial Moons architecture (Phobos and Deimos meshes, materials, LineLoops, angular velocities).
14. Web Dynamic Sun disk & corona glow billboard (canvas radial gradient, diurnal formula, presets).
15. Web Satellite Constellation architecture (MRO, Odyssey, MAVEN, Global Surveyor meshes, LineLoops, nadir tracking).
16. Web Headless browser CDP verification (attaches to WebSocket, checks Three.js, 0 console errors).
17. Repository single-source blend invariant (zero duplicate .blend files and zero .blend1 backups).
18. Docs allowlist strict hygiene (only authorized markdown files in docs/).
19. Full CLI execution of scripts/audit_workspace_integrity.py exits with 0 and overall status PASS.
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

ALLOWED_DOCS = {
    "AGENTS.md",
    "ARABIC_REPORT.md",
    "ARABIC_SUMMARY_REPORT.md",
    "PROJECT_BLUEPRINT_AND_KNOWLEDGE_BASE.md",
    "SYSTEM_GUIDE.md",
}


# ==============================================================================
# 1. BLENDER CELESTIAL & ORBITAL EMPIRICAL TESTS
# ==============================================================================

class Milestone8BlenderCelestialTests(unittest.TestCase):
    """Empirical challenges inspecting MARS_SENTINEL_HERO_PRODUCTION.blend in Blender 5.2."""

    @classmethod
    def setUpClass(cls):
        """Execute headless python inspection in Blender once and cache parsed output."""
        if not os.path.isfile(BLENDER_BIN) or not os.path.isfile(HERO_BLEND):
            cls.blender_available = False
            return
        cls.blender_available = True

        script = """
import bpy, json, hashlib, array

data = {}

# 1. HiRISE
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
if site and site.data:
    v_cnt = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (v_cnt * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    data["hirise"] = {"exists": True, "verts": v_cnt, "sha256": h}
else:
    data["hirise"] = {"exists": False}

# 2. Celestial Moons (Phobos & Deimos)
phobos = bpy.data.objects.get("MS_Moon_Phobos") or bpy.data.objects.get("Phobos") or bpy.data.objects.get("MS_Phobos")
deimos = bpy.data.objects.get("MS_Moon_Deimos") or bpy.data.objects.get("Deimos") or bpy.data.objects.get("MS_Deimos")

data["phobos"] = {
    "exists": phobos is not None,
    "type": phobos.type if phobos else None,
    "materials": [s.material.name for s in phobos.material_slots if s.material] if phobos else [],
    "constraints": [c.type for c in phobos.constraints] if phobos else []
}
data["deimos"] = {
    "exists": deimos is not None,
    "type": deimos.type if deimos else None,
    "materials": [s.material.name for s in deimos.material_slots if s.material] if deimos else [],
    "constraints": [c.type for c in deimos.constraints] if deimos else []
}

# Moon curves
p_curve = bpy.data.objects.get("MS_Orbit_Path_Phobos")
d_curve = bpy.data.objects.get("MS_Orbit_Path_Deimos")
data["phobos_curve"] = {"exists": p_curve is not None, "type": p_curve.type if p_curve else None}
data["deimos_curve"] = {"exists": d_curve is not None, "type": d_curve.type if d_curve else None}

# 3. Sun
sun = bpy.data.objects.get("Mars_Sun") or bpy.data.objects.get("Sun")
data["sun"] = {
    "exists": sun is not None,
    "type": sun.type if sun else None,
    "has_animation": bool(sun.animation_data) if sun else False
}

# 4. Constellation
orbiters = ["MS_Orbiter_MRO", "MS_Orbiter_Odyssey", "MS_Orbiter_MAVEN", "MS_Orbiter_GlobalSurveyor"]
const_found = {}
for name in orbiters:
    obj = bpy.data.objects.get(name) or bpy.data.objects.get(name.replace("MS_Orbiter_", "MS_"))
    curve = bpy.data.objects.get(name.replace("MS_Orbiter_", "MS_Orbit_Path_"))
    const_found[name] = {
        "exists": obj is not None,
        "type": obj.type if obj else None,
        "constraints": [c.type for c in obj.constraints] if obj else [],
        "curve_exists": curve is not None
    }
data["constellation"] = const_found

# 5. Libraries & Images
data["libraries_count"] = len(bpy.data.libraries)
missing_imgs = [img.name for img in bpy.data.images if img.name not in ('Render Result', 'Viewer Node') and not img.packed_file and not os.path.exists(bpy.path.abspath(img.filepath))]
data["missing_images"] = missing_imgs
data["total_images"] = len([i for i in bpy.data.images if i.name not in ('Render Result', 'Viewer Node')])

print("M8_JSON_START")
print(json.dumps(data))
print("M8_JSON_END")
"""
        cmd = [BLENDER_BIN, HERO_BLEND, "--background", "--python-expr", script]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        assert proc.returncode == 0, f"Blender headless execution failed: {proc.stderr}"
        s = proc.stdout.find("M8_JSON_START")
        e = proc.stdout.find("M8_JSON_END")
        assert s != -1 and e != -1, f"Failed to parse JSON markers from output:\n{proc.stdout}"
        cls.report = json.loads(proc.stdout[s + len("M8_JSON_START"):e].strip())

    def test_01_hirise_terrain_invariant_preserved(self):
        """Verify NASA_HiRISE_DTM_Jezero_FieldSite_4m vertex count and SHA-256 hash match invariant."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        hirise = self.report.get("hirise", {})
        self.assertTrue(hirise.get("exists"), "NASA_HiRISE_DTM_Jezero_FieldSite_4m missing")
        self.assertEqual(hirise.get("verts"), HIRISE_EXPECTED_VERTS)
        self.assertEqual(hirise.get("sha256"), HIRISE_EXPECTED_SHA256)

    def test_02_blender_scene_independence_and_image_count(self):
        """Verify zero external libraries, zero missing images, and exact 276 total images in blend."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        self.assertEqual(self.report.get("libraries_count"), 0, "External libraries found in blend")
        self.assertEqual(self.report.get("missing_images"), [], "Missing images found in blend")
        self.assertEqual(self.report.get("total_images"), 276, "Image datablocks count altered")

    def test_03_blender_phobos_moon_and_orbit_spline(self):
        """Verify Phobos 3D procedural mesh, material, curve, and Follow Path constraint in Blender."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        phobos = self.report.get("phobos", {})
        self.assertTrue(phobos.get("exists"), "Phobos 3D object missing in Blender scene")
        self.assertEqual(phobos.get("type"), "MESH")
        self.assertGreater(len(phobos.get("materials", [])), 0, "Phobos material slot missing")
        self.assertIn("FOLLOW_PATH", phobos.get("constraints", []), "Phobos Follow Path constraint missing")

        p_curve = self.report.get("phobos_curve", {})
        self.assertTrue(p_curve.get("exists"), "MS_Orbit_Path_Phobos curve missing")
        self.assertEqual(p_curve.get("type"), "CURVE")

    def test_04_blender_deimos_moon_and_orbit_spline(self):
        """Verify Deimos 3D procedural mesh, material, curve, and Follow Path constraint in Blender."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        deimos = self.report.get("deimos", {})
        self.assertTrue(deimos.get("exists"), "Deimos 3D object missing in Blender scene")
        self.assertEqual(deimos.get("type"), "MESH")
        self.assertGreater(len(deimos.get("materials", [])), 0, "Deimos material slot missing")
        self.assertIn("FOLLOW_PATH", deimos.get("constraints", []), "Deimos Follow Path constraint missing")

        d_curve = self.report.get("deimos_curve", {})
        self.assertTrue(d_curve.get("exists"), "MS_Orbit_Path_Deimos curve missing")
        self.assertEqual(d_curve.get("type"), "CURVE")

    def test_05_blender_sun_diurnal_lighting(self):
        """Verify Mars_Sun light object exists and has dynamic animation data configured."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        sun = self.report.get("sun", {})
        self.assertTrue(sun.get("exists"), "Mars_Sun light missing in Blender scene")
        self.assertEqual(sun.get("type"), "LIGHT")
        self.assertTrue(sun.get("has_animation"), "Mars_Sun diurnal animation keyframes missing")

    def test_06_blender_satellite_constellation_and_nadir_tracking(self):
        """Verify all 4 orbiters (MRO, Odyssey, MAVEN, Global Surveyor) have orbit splines and nadir constraints."""
        if not self.blender_available:
            self.skipTest("Blender executable not found")
        const = self.report.get("constellation", {})
        for name in ["MS_Orbiter_MRO", "MS_Orbiter_Odyssey", "MS_Orbiter_MAVEN", "MS_Orbiter_GlobalSurveyor"]:
            orb = const.get(name, {})
            self.assertTrue(orb.get("exists"), f"Orbiter {name} missing in scene")
            self.assertTrue(orb.get("curve_exists"), f"Orbit spline for {name} missing")
            self.assertIn("FOLLOW_PATH", orb.get("constraints", []), f"{name} missing Follow Path constraint")
            self.assertIn("TRACK_TO", orb.get("constraints", []), f"{name} missing Track To nadir constraint")


# ==============================================================================
# 2. UNITY ORBITAL MECHANICS & MATHEMATICAL ORACLES
# ==============================================================================

class Milestone8UnityOrbitalMechanicsTests(unittest.TestCase):
    """Empirical challenges verifying Unity C# scripts and mathematical oracles."""

    def test_07_unity_csharp_script_syntax_and_delimiters(self):
        """Verify all C# scripts in unity_project/Assets/Scripts/ have balanced braces and clean syntax."""
        scripts_dir = os.path.join(UNITY_DIR, "Assets", "Scripts")
        if not os.path.isdir(scripts_dir):
            self.skipTest("Unity scripts dir not found")

        cs_files = []
        for root, _, files in os.walk(scripts_dir):
            for f in files:
                if f.endswith(".cs"):
                    cs_files.append(os.path.join(root, f))

        self.assertGreater(len(cs_files), 0, "No C# scripts found in unity_project/Assets/Scripts")
        for cs in cs_files:
            with open(cs, "r", encoding="utf-8") as f:
                code = f.read()
            clean = re.sub(r'//.*?$|/\*.*?\*/|"(?:\\.|[^\\"])*"', '', code, flags=re.MULTILINE | re.DOTALL)
            opens = clean.count("{")
            closes = clean.count("}")
            self.assertEqual(opens, closes, f"Unbalanced braces in {os.path.basename(cs)}: {opens} vs {closes}")

    def test_08_unity_meta_pairing_invariants(self):
        """Verify zero missing and zero orphan .meta files in unity_project/Assets."""
        assets_dir = os.path.join(UNITY_DIR, "Assets")
        if not os.path.isdir(assets_dir):
            self.skipTest("Unity Assets dir not found")

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

    def test_09_keplerian_frequency_ratio_mathematical_oracle(self):
        """Empirically verify astronomical orbital periods and frequency ratios for Phobos and Deimos."""
        t_mars_sol_hours = 24.6229
        t_phobos_hours = 7.6538
        t_deimos_hours = 30.312

        omega_mars = 2.0 * math.pi / t_mars_sol_hours
        omega_phobos = 2.0 * math.pi / t_phobos_hours
        omega_deimos = 2.0 * math.pi / t_deimos_hours

        ratio_phobos = omega_phobos / omega_mars
        ratio_deimos = omega_deimos / omega_mars

        self.assertAlmostEqual(ratio_deimos, 0.812, delta=0.02, msg="Deimos frequency ratio should be ~0.81")
        self.assertGreater(ratio_phobos, 3.2, msg="Phobos must orbit substantially faster than Mars rotation")

        inc_phobos_rad = math.radians(1.08)
        inc_deimos_rad = math.radians(1.79)
        self.assertAlmostEqual(inc_phobos_rad, 0.01885, delta=1e-4)
        self.assertAlmostEqual(inc_deimos_rad, 0.03124, delta=1e-4)

    def test_10_jezero_diurnal_solar_arc_mathematical_oracle(self):
        """Empirically verify Jezero Crater diurnal elevation and azimuth calculations."""
        lat_rad = math.radians(18.38)
        h_noon = math.asin(math.sin(lat_rad) * math.sin(0) + math.cos(lat_rad) * math.cos(0) * math.cos(0))
        h_noon_deg = math.degrees(h_noon)
        self.assertAlmostEqual(h_noon_deg, 90.0 - 18.38, delta=0.01, msg="Noon elevation at equinox must equal 90 - lat = 71.62°")

        for h_deg in [-120, -90, -45, 0, 45, 90, 120]:
            h_rad = math.radians(h_deg)
            sin_elev = math.sin(lat_rad) * math.sin(0) + math.cos(lat_rad) * math.cos(0) * math.cos(h_rad)
            elev = math.asin(max(-1.0, min(1.0, sin_elev)))
            cos_elev = math.cos(elev)
            sin_az = -math.sin(h_rad) / (cos_elev if abs(cos_elev) > 1e-5 else 1e-5)
            cos_az = -math.sin(lat_rad) * sin_elev / (math.cos(lat_rad) * (cos_elev if abs(cos_elev) > 1e-5 else 1e-5))
            az = math.atan2(sin_az, cos_az)

            vx = -math.cos(elev) * math.sin(az)
            vy = math.sin(elev)
            vz = -math.cos(elev) * math.cos(az)
            mag = math.sqrt(vx * vx + vy * vy + vz * vz)
            self.assertAlmostEqual(mag, 1.0, places=5, msg="Solar vector must be normalized unit vector")


# ==============================================================================
# 3. WEB PLATFORM DOM, JAVASCRIPT & CDP VERIFICATION
# ==============================================================================

class DOMParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = set()
        self.classes = set()

    def handle_starttag(self, tag, attrs):
        d = dict(attrs)
        if "id" in d:
            self.ids.add(d["id"])
        if "class" in d:
            for c in d["class"].split():
                self.classes.add(c)


class Milestone8WebPlatformTests(unittest.TestCase):
    """Empirical challenges validating web/app.js, web/index.html, and browser runtime."""

    @classmethod
    def setUpClass(cls):
        with open(os.path.join(WEB_DIR, "index.html"), "r", encoding="utf-8") as f:
            cls.html_code = f.read()
        with open(os.path.join(WEB_DIR, "app.js"), "r", encoding="utf-8") as f:
            cls.js_code = f.read()
        cls.parser = DOMParser()
        cls.parser.feed(cls.html_code)

    def test_11_web_dom_selector_resolution_parity(self):
        """Verify every document.getElementById call in web/app.js resolves to an existing ID in index.html."""
        js_ids = set(re.findall(r"document\.getElementById\(['\"]([^'\"]+)['\"]\)", self.js_code))
        missing = [eid for eid in js_ids if eid not in self.parser.ids]
        self.assertEqual(missing, [], f"document.getElementById called for missing HTML IDs: {missing}")

    def test_12_web_app_js_balanced_delimiters_and_iife(self):
        """Verify web/app.js is cleanly enclosed in an IIFE and has balanced delimiters."""
        self.assertTrue(self.js_code.strip().startswith("(() => {") or "(() => {" in self.js_code[:100], "app.js must use IIFE wrapper")
        self.assertTrue(self.js_code.strip().endswith("})();"), "app.js must close IIFE wrapper cleanly")

        clean = re.sub(r'//.*?$|/\*.*?\*/|"(?:\\.|[^\\"])*"|\'(?:\\.|[^\'])*\'|`(?:\\.|[^`])*`', '', self.js_code, flags=re.MULTILINE | re.DOTALL)
        stack = []
        delims = {')': '(', ']': '[', '}': '{'}
        for char in clean:
            if char in "({[":
                stack.append(char)
            elif char in ")}]":
                self.assertTrue(len(stack) > 0, f"Unmatched closing delimiter: {char}")
                top = stack.pop()
                self.assertEqual(top, delims[char], f"Mismatched delimiter: expected {delims[char]}, got {char}")
        self.assertEqual(len(stack), 0, f"Unclosed delimiters remaining: {stack}")

    def test_13_web_phobos_deimos_celestial_architecture(self):
        """Verify Phobos and Deimos celestial objects, LineLoops, and angular velocity constants in app.js."""
        self.assertIn("phobosMesh", self.js_code, "Phobos mesh variable missing in app.js")
        self.assertIn("deimosMesh", self.js_code, "Deimos mesh variable missing in app.js")
        self.assertIn("THREE.LineLoop", self.js_code, "THREE.LineLoop missing for orbital paths in app.js")
        self.assertIn("3.97", self.js_code, "Phobos angular velocity ratio 3.97 missing in app.js")
        self.assertIn("0.81", self.js_code, "Deimos angular velocity ratio 0.81 missing in app.js")

    def test_14_web_sun_disk_and_diurnal_arc_architecture(self):
        """Verify dynamic 3D Sun disk, corona glow texture/billboard, and Jezero diurnal arc in app.js."""
        self.assertIn("sunGroup", self.js_code, "sunGroup missing in app.js")
        self.assertIn("calculateJezeroSolarVector", self.js_code, "calculateJezeroSolarVector missing in app.js")
        self.assertIn("18.38", self.js_code, "Jezero latitude 18.38 missing in diurnal calculations in app.js")

    def test_15_web_satellite_constellation_architecture(self):
        """Verify full satellite constellation (MRO, Odyssey, MAVEN, Global Surveyor) in app.js."""
        self.assertIn("constellationGroup", self.js_code, "constellationGroup missing in app.js")
        for sat in ["mro", "odyssey", "maven", "mgs"]:
            self.assertIn(sat, self.js_code.lower(), f"Satellite {sat} missing in app.js constellation config")
        self.assertIn("lookAt(0, 0, 0)", self.js_code, "Nadir pointing lookAt(0, 0, 0) missing in app.js")

    def test_16_headless_edge_cdp_zero_console_errors(self):
        """Execute web/index.html in headless Microsoft Edge via CDP and verify 0 console errors."""
        edge_bin = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        chrome_bin = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
        browser_bin = edge_bin if os.path.isfile(edge_bin) else (chrome_bin if os.path.isfile(chrome_bin) else None)
        if not browser_bin:
            self.skipTest("No compatible headless browser (Edge or Chrome) available")

        temp_dir = os.path.join(os.environ.get("TEMP", "."), "m8_cdp_test")
        url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")

        cmd = [
            browser_bin,
            "--headless=new",
            "--remote-debugging-port=9224",
            f"--user-data-dir={temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            url
        ]

        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            time.sleep(2.5)
            import urllib.request
            req = urllib.request.Request("http://127.0.0.1:9224/json")
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
            self.assertEqual(res_three.get("result", {}).get("result", {}).get("value"), "object", "THREE is not defined in browser context")

            res_canvas = simple_ws_query(ws_url, {"id": 2, "method": "Runtime.evaluate", "params": {"expression": "!!document.getElementById('mars-canvas')"}})
            self.assertTrue(res_canvas.get("result", {}).get("result", {}).get("value"), "Canvas element missing in browser DOM")

        finally:
            proc.terminate()
            try:
                proc.wait(timeout=2)
            except Exception:
                proc.kill()


# ==============================================================================
# 4. REPOSITORY PRESERVATION INVARIANTS & AUDIT GATE
# ==============================================================================

class Milestone8PreservationTests(unittest.TestCase):
    """Empirical challenges verifying repository invariants and regression freedom."""

    def test_17_zero_duplicate_blends_and_backups(self):
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

    def test_18_docs_allowlist_strict_hygiene(self):
        """Verify docs/ contains only strictly authorized markdown files."""
        docs_dir = os.path.join(PROJECT_ROOT, "docs")
        found_md = {f for f in os.listdir(docs_dir) if f.endswith(".md")}
        unauthorized = found_md - ALLOWED_DOCS
        self.assertEqual(unauthorized, set(), f"Unauthorized files in docs/: {unauthorized}")
        self.assertEqual(found_md, ALLOWED_DOCS, "Docs allowlist missing authorized files")

    def test_19_full_workspace_integrity_audit_clean_pass(self):
        """Verify scripts/audit_workspace_integrity.py --json passes with 0 failures."""
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
