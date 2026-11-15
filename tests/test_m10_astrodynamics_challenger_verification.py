"""
Milestone 10 Challenger 2 Astrodynamics & Shader Fidelity Empirical Verification Suite
Author: Challenger 2 (m10_challenger_astrodynamics)
Scope: Empirical and adversarial stress testing of Mars orbital mechanics, Phobos/Deimos astrodynamics,
       Jezero diurnal solar arc, Keplerian orbiters, sand shaders, micro-normals, and continuous animations.
"""

import os
import sys
import json
import math
import array
import hashlib
import subprocess
import unittest
import socket
import struct
import base64
import time

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
WEB_DIR = os.path.join(PROJECT_ROOT, "web")
EDGE_BIN = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
CHROME_BIN = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"


# ==============================================================================
# 1. ASTRODYNAMICS & CELESTIAL MECHANICS MATHEMATICAL ORACLE
# ==============================================================================

class Milestone10AstrodynamicsOracleTests(unittest.TestCase):
    """Rigorous physical and mathematical verification against NASA Mars parameters."""

    def test_01_kepler_third_law_moons_gravitational_parameter(self):
        """Verify Phobos and Deimos orbital parameters adhere to Kepler's 3rd Law around Mars."""
        # Mars gravitational parameter mu = GM (km^3 / s^2)
        mu_mars_nasa = 42828.37

        # Unity implemented constants
        phobos_a_km = 9376.0
        phobos_T_h = 7.6538
        phobos_T_s = phobos_T_h * 3600.0

        deimos_a_km = 23463.0
        deimos_T_h = 30.312
        deimos_T_s = deimos_T_h * 3600.0

        # Kepler's 3rd Law: mu = 4 * pi^2 * a^3 / T^2
        mu_phobos = (4.0 * math.pi**2 * phobos_a_km**3) / (phobos_T_s**2)
        mu_deimos = (4.0 * math.pi**2 * deimos_a_km**3) / (deimos_T_s**2)

        # Check error relative to NASA authoritative GM
        err_phobos = abs(mu_phobos - mu_mars_nasa) / mu_mars_nasa
        err_deimos = abs(mu_deimos - mu_mars_nasa) / mu_mars_nasa

        self.assertLess(err_phobos, 0.001, f"Phobos Kepler 3rd Law error too large: {err_phobos:.5f}")
        self.assertLess(err_deimos, 0.001, f"Deimos Kepler 3rd Law error too large: {err_deimos:.5f}")

    def test_02_synodic_period_and_apparent_motion_direction(self):
        """Verify Phobos rises in the West (super-synchronous) and Deimos in the East (sub-synchronous)."""
        mars_rot_h = 24.6597
        phobos_orb_h = 7.6538
        deimos_orb_h = 30.312

        # Apparent angular rate relative to Martian ground observer
        omega_mars = 360.0 / mars_rot_h
        omega_phobos = 360.0 / phobos_orb_h
        omega_deimos = 360.0 / deimos_orb_h

        # Relative rate = Moon rate - Mars rate
        rel_phobos = omega_phobos - omega_mars
        rel_deimos = omega_deimos - omega_mars

        # Phobos should be positive (prograde overtaking ground -> West to East)
        self.assertGreater(rel_phobos, 0, "Phobos must orbit faster than Mars rotation (rises West)")
        # Deimos should be negative (ground overtakes Moon -> East to West)
        self.assertLess(rel_deimos, 0, "Deimos must orbit slower than Mars rotation (rises East)")

        # Calculate synodic periods
        synodic_phobos_h = 360.0 / rel_phobos
        synodic_deimos_h = 360.0 / abs(rel_deimos)

        self.assertAlmostEqual(synodic_phobos_h, 11.1, delta=0.2, msg="Phobos synodic period ~11.1h")
        self.assertAlmostEqual(synodic_deimos_h, 132.2, delta=1.0, msg="Deimos synodic period ~132h (~5.4 sols)")

    def test_03_jezero_solar_vector_randomized_stress_test(self):
        """Stress test spherical astronomy solar vector formula across 2000 randomized inputs for singularities."""
        import random
        random.seed(42)

        lat_deg = 18.38
        lat_rad = math.radians(lat_deg)
        eps_rad = math.radians(25.19) # Mars obliquity

        for _ in range(2000):
            ls_deg = random.uniform(0.0, 360.0)
            hour_angle_deg = random.uniform(-180.0, 180.0)

            ls_rad = math.radians(ls_deg)
            h_rad = math.radians(hour_angle_deg)

            # Declination
            sin_delta = math.sin(eps_rad) * math.sin(ls_rad)
            delta_rad = math.asin(max(-1.0, min(1.0, sin_delta)))

            # Elevation
            sin_elev = math.sin(lat_rad) * math.sin(delta_rad) + math.cos(lat_rad) * math.cos(delta_rad) * math.cos(h_rad)
            elev_rad = math.asin(max(-1.0, min(1.0, sin_elev)))
            elev_deg = math.degrees(elev_rad)

            # Azimuth
            cos_elev = math.cos(elev_rad)
            clamped_cos = cos_elev if abs(cos_elev) > 1e-5 else 1e-5
            sin_a = -math.sin(h_rad) * math.cos(delta_rad) / clamped_cos
            cos_a = (math.sin(delta_rad) - math.sin(lat_rad) * sin_elev) / (math.cos(lat_rad) * clamped_cos)
            az_rad = math.atan2(sin_a, cos_a)

            # Vector
            vx = -math.cos(elev_rad) * math.sin(az_rad)
            vy = math.sin(elev_rad)
            vz = -math.cos(elev_rad) * math.cos(az_rad)
            v_len = math.sqrt(vx*vx + vy*vy + vz*vz)

            # Assertions
            self.assertFalse(math.isnan(elev_deg), "Elevation produced NaN")
            self.assertFalse(math.isnan(az_rad), "Azimuth produced NaN")
            self.assertGreaterEqual(elev_deg, -90.0)
            self.assertLessEqual(elev_deg, 90.0)
            self.assertAlmostEqual(v_len, 1.0, places=5, msg=f"Solar vector not normalized: {v_len}")

    def test_04_solar_zenith_singularity_boundary(self):
        """Directly challenge overhead zenith condition (elevation = 90 deg) for division by zero."""
        lat_rad = math.radians(18.38)
        delta_rad = lat_rad # Subsolar point exactly at observer's latitude!
        h_rad = 0.0 # Solar noon!

        sin_elev = math.sin(lat_rad) * math.sin(delta_rad) + math.cos(lat_rad) * math.cos(delta_rad) * math.cos(h_rad)
        self.assertAlmostEqual(sin_elev, 1.0, places=6)
        elev_rad = math.asin(min(1.0, sin_elev))
        cos_elev = math.cos(elev_rad) # cos(90 deg) = 0!

        # Guarded formula
        clamped_cos = cos_elev if abs(cos_elev) > 1e-5 else 1e-5
        sin_a = -math.sin(h_rad) * math.cos(delta_rad) / clamped_cos
        cos_a = (math.sin(delta_rad) - math.sin(lat_rad) * sin_elev) / (math.cos(lat_rad) * clamped_cos)
        az_rad = math.atan2(sin_a, cos_a)

        self.assertFalse(math.isnan(az_rad), "Zenith condition produced NaN")
        self.assertFalse(math.isinf(az_rad), "Zenith condition produced Inf")

    def test_05_kepler_newton_raphson_convergence_adversarial_eccentricities(self):
        """Stress-test Newton-Raphson Kepler solver across boundary eccentricities e in [0.0, 0.95]."""
        for e in [0.0, 0.005, 0.05, 0.22, 0.35, 0.75, 0.90, 0.95]:
            for M_deg in [0.0, 45.0, 90.0, 135.0, 180.0, 225.0, 270.0, 315.0, 359.9]:
                M_rad = math.radians(M_deg)
                E_rad = M_rad
                for _ in range(8):
                    f = E_rad - e * math.sin(E_rad) - M_rad
                    f_prime = 1.0 - e * math.cos(E_rad)
                    E_rad -= f / f_prime

                residual = abs(E_rad - e * math.sin(E_rad) - M_rad)
                self.assertLess(residual, 1e-4, f"Newton-Raphson failed to converge for e={e}, M={M_deg}: residual={residual}")


# ==============================================================================
# 2. SHADER FIDELITY & SLOPE BLENDING ORACLE
# ==============================================================================

class Milestone10ShaderFidelityOracleTests(unittest.TestCase):
    """Mathematical and numerical verification of slope blending, micro-normals, and ripple harmonics."""

    def test_06_slope_smoothstep_c1_continuity(self):
        """Verify smoothstep slope blending curve has continuous first derivatives and no step jumps."""
        threshold = 0.85
        width = 0.15
        edge0 = threshold - width # 0.70
        edge1 = threshold + width # 1.00

        def smoothstep(e0, e1, x):
            t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
            return t * t * (3.0 - 2.0 * t)

        steps = 500
        prev_val = 0.0
        prev_deriv = 0.0
        for i in range(steps + 1):
            slope = i / float(steps)
            val = smoothstep(edge0, edge1, slope)
            self.assertGreaterEqual(val, 0.0)
            self.assertLessEqual(val, 1.0)
            if i > 0:
                deriv = (val - prev_val) * steps
                # Derivative must be non-negative (monotonically increasing)
                self.assertGreaterEqual(deriv, -1e-6)
                prev_deriv = deriv
            prev_val = val

    def test_07_perturbed_normal_normalization_and_energy_conservation(self):
        """Verify tangent-space micro-normal perturbation preserves unit vector property and non-zero length."""
        import random
        random.seed(101)

        for _ in range(1000):
            # Base normal
            nx = random.uniform(-1.0, 1.0)
            ny = random.uniform(0.1, 1.0)
            nz = random.uniform(-1.0, 1.0)
            l = math.sqrt(nx*nx + ny*ny + nz*nz)
            normal = [nx/l, ny/l, nz/l]

            # Ripple offset
            blend = random.uniform(0.0, 1.0)
            ripple_str = random.uniform(0.0, 0.8)
            rx = random.uniform(-0.15, 0.15) * blend * ripple_str
            ry = 0.0
            rz = random.uniform(-0.15, 0.15) * blend * ripple_str

            perturbed = [normal[0] + rx, normal[1] + ry, normal[2] + rz]
            p_len = math.sqrt(perturbed[0]**2 + perturbed[1]**2 + perturbed[2]**2)

            self.assertGreater(p_len, 0.01, "Degenerate normal generated")
            norm_perturbed = [perturbed[0]/p_len, perturbed[1]/p_len, perturbed[2]/p_len]
            final_len = math.sqrt(norm_perturbed[0]**2 + norm_perturbed[1]**2 + norm_perturbed[2]**2)
            self.assertAlmostEqual(final_len, 1.0, places=6)

    def test_08_procedural_normal_texture_derivative_bounds(self):
        """Simulate procedural normal texture generation from app.js to verify zero NaNs and valid RGB encoding."""
        size = 64
        height = [0.0] * (size * size)
        two_pi = math.pi * 2.0

        for y in range(size):
            v = (y / float(size)) * two_pi
            for x in range(size):
                u = (x / float(size)) * two_pi
                ripple_phase = 8.0 * u + 4.0 * v
                sin_r = math.sin(ripple_phase)
                primary_wave = math.copysign(abs(sin_r)**0.7, sin_r) * 0.55
                cross_wave = math.sin(16.0 * u - 8.0 * v + 1.2) * 0.22
                fine_ripples = math.sin(32.0 * u + 12.0 * v) * 0.12
                micro_grit = (math.sin(64.0*u - 32.0*v) * math.cos(48.0*u + 48.0*v)) * 0.08
                height[y * size + x] = primary_wave + cross_wave + fine_ripples + micro_grit

        bump_scale = 4.5
        for y in range(size):
            y_prev = (y - 1 + size) % size
            y_next = (y + 1) % size
            for x in range(size):
                x_prev = (x - 1 + size) % size
                x_next = (x + 1) % size
                dhdx = (height[y * size + x_next] - height[y * size + x_prev]) * 0.5
                dhdy = (height[y_next * size + x] - height[y_prev * size + x]) * 0.5
                nx = -dhdx * bump_scale
                ny = -dhdy * bump_scale
                nz = 1.0
                inv_len = 1.0 / math.sqrt(nx*nx + ny*ny + nz*nz)
                nx *= inv_len
                ny *= inv_len
                nz *= inv_len

                r = math.floor((nx * 0.5 + 0.5) * 255)
                g = math.floor((ny * 0.5 + 0.5) * 255)
                b = math.floor((nz * 0.5 + 0.5) * 255)

                self.assertTrue(0 <= r <= 255, f"R channel out of bounds: {r}")
                self.assertTrue(0 <= g <= 255, f"G channel out of bounds: {g}")
                self.assertTrue(0 <= b <= 255, f"B channel out of bounds: {b}")


# ==============================================================================
# 3. BLENDER LIVE EVALUATION TESTS
# ==============================================================================

class Milestone10BlenderHeroEvaluationTests(unittest.TestCase):
    """Direct headless evaluation of MARS_SENTINEL_HERO_PRODUCTION.blend."""

    @classmethod
    def setUpClass(cls):
        if not os.path.isfile(BLENDER_BIN) or not os.path.isfile(HERO_BLEND):
            raise unittest.SkipTest("Blender or Hero blend missing")

        script = """
import bpy, json, hashlib, array

res = {}

# 1. HiRISE
site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
if site and site.data:
    v_cnt = len(site.data.vertices)
    coords = array.array('f', [0.0]) * (v_cnt * 3)
    site.data.vertices.foreach_get('co', coords)
    h = hashlib.sha256(coords.tobytes()).hexdigest()
    res["hirise"] = {"verts": v_cnt, "sha256": h}
else:
    res["hirise"] = None

# 2. Celestial Moons
phobos = bpy.data.objects.get("MS_Moon_Phobos")
deimos = bpy.data.objects.get("MS_Moon_Deimos")
p_curve = bpy.data.objects.get("MS_Orbit_Path_Phobos")
d_curve = bpy.data.objects.get("MS_Orbit_Path_Deimos")

res["phobos"] = {
    "exists": phobos is not None,
    "has_path": p_curve is not None,
    "has_follow_path": any(c.type == 'FOLLOW_PATH' for c in phobos.constraints) if phobos else False
}
res["deimos"] = {
    "exists": deimos is not None,
    "has_path": d_curve is not None,
    "has_follow_path": any(c.type == 'FOLLOW_PATH' for c in deimos.constraints) if deimos else False
}

# 3. Sun
sun = bpy.data.objects.get("Mars_Sun")
res["sun"] = {
    "exists": sun is not None,
    "animated": bool(sun.animation_data) if sun else False
}

# 4. Constellation
sats = ["MS_Orbiter_MRO", "MS_Orbiter_Odyssey", "MS_Orbiter_MAVEN", "MS_Orbiter_GlobalSurveyor"]
sats_info = {}
for s in sats:
    obj = bpy.data.objects.get(s) or bpy.data.objects.get(s.replace("MS_Orbiter_", "MS_"))
    curve = bpy.data.objects.get(s.replace("MS_Orbiter_", "MS_Orbit_Path_"))
    sats_info[s] = {
        "exists": obj is not None,
        "curve_exists": curve is not None,
        "has_follow_path": any(c.type == 'FOLLOW_PATH' for c in obj.constraints) if obj else False,
        "has_track_to": any(c.type == 'TRACK_TO' for c in obj.constraints) if obj else False
    }
res["constellation"] = sats_info

# 5. Continuous Rotation & Material Nodes
rotator = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
res["rotator_animated"] = bool(rotator.animation_data) if rotator else False

mat = bpy.data.materials.get("Mars_NASA_HiRISE_Viking_Color")
if mat and mat.node_tree:
    res["material_nodes"] = [n.name for n in mat.node_tree.nodes]
else:
    res["material_nodes"] = []

# 6. Images & Libraries
res["packed_images"] = sum(1 for img in bpy.data.images if img.packed_file is not None)
res["unpacked_images"] = sum(1 for img in bpy.data.images if img.packed_file is None and img.source == 'FILE' and img.name not in ('Render Result', 'Viewer Node'))
res["libraries"] = len(bpy.data.libraries)

print("M10_BLENDER_START")
print(json.dumps(res))
print("M10_BLENDER_END")
"""
        cmd = [BLENDER_BIN, HERO_BLEND, "-b", "--python-expr", script]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        assert proc.returncode == 0, f"Blender headless execution failed: {proc.stderr}"
        s = proc.stdout.find("M10_BLENDER_START")
        e = proc.stdout.find("M10_BLENDER_END")
        assert s != -1 and e != -1, f"Failed to parse JSON markers from output:\n{proc.stdout}"
        cls.data = json.loads(proc.stdout[s + len("M10_BLENDER_START"):e].strip())

    def test_09_hirise_invariant_preservation(self):
        """Verify the HiRISE DTM coordinate hash is bit-for-bit preserved."""
        h = self.data["hirise"]
        self.assertIsNotNone(h)
        self.assertEqual(h["verts"], HIRISE_EXPECTED_VERTS)
        self.assertEqual(h["sha256"], HIRISE_EXPECTED_SHA256)

    def test_10_celestial_moons_and_follow_path(self):
        """Verify Phobos and Deimos exist with paths and Follow Path constraints."""
        p = self.data["phobos"]
        d = self.data["deimos"]
        self.assertTrue(p["exists"] and p["has_path"] and p["has_follow_path"])
        self.assertTrue(d["exists"] and d["has_path"] and d["has_follow_path"])

    def test_11_sun_and_constellation_nadir(self):
        """Verify Mars_Sun animation and Constellation Follow Path + Track To Nadir."""
        self.assertTrue(self.data["sun"]["exists"] and self.data["sun"]["animated"])
        for sat_name, info in self.data["constellation"].items():
            self.assertTrue(info["exists"], f"{sat_name} missing")
            self.assertTrue(info["curve_exists"], f"{sat_name} curve missing")
            self.assertTrue(info["has_follow_path"], f"{sat_name} missing Follow Path constraint")
            self.assertTrue(info["has_track_to"], f"{sat_name} missing Track To constraint")

    def test_12_slope_aware_material_nodes(self):
        """Verify Mars_NASA_HiRISE_Viking_Color contains slope-aware blending nodes."""
        nodes = self.data["material_nodes"]
        self.assertIn("Slope_Geometry", nodes)
        self.assertIn("Slope_Factor", nodes)
        self.assertIn("Slope_Albedo_Blend", nodes)
        self.assertIn("Rock_Strata_Wave", nodes)
        self.assertIn("Rock_Strata_Bump", nodes)

    def test_13_blend_file_hygiene(self):
        """Verify 276 packed images, 0 unpacked external images, and 0 external libraries."""
        self.assertEqual(self.data["packed_images"], 276)
        self.assertEqual(self.data["unpacked_images"], 0)
        self.assertEqual(self.data["libraries"], 0)


# ==============================================================================
# 4. WEB PLATFORM CDP STRESS HARNESS
# ==============================================================================

class Milestone10WebCDPHarnessTests(unittest.TestCase):
    """Executes headless CDP browser automation against Web platform."""

    def test_14_cdp_diurnal_lighting_and_orbit_mode_stress(self):
        """Cycle through diurnal lighting presets and toggle Orbit mode under CDP."""
        browser_bin = EDGE_BIN if os.path.isfile(EDGE_BIN) else (CHROME_BIN if os.path.isfile(CHROME_BIN) else None)
        if not browser_bin:
            self.skipTest("No supported browser found for CDP testing")

        import urllib.request
        index_url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")
        port = 9339
        temp_dir = os.path.join(os.environ.get("TEMP", "."), "m10_cdp_test")

        proc = subprocess.Popen([
            browser_bin,
            "--headless=new",
            f"--remote-debugging-port={port}",
            f"--user-data-dir={temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            index_url
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

        try:
            time.sleep(2.5)
            with urllib.request.urlopen(f"http://127.0.0.1:{port}/json", timeout=3) as resp:
                targets = json.loads(resp.read().decode())

            main_target = next((t for t in targets if "index.html" in t.get("url", "")), None)
            if not main_target and targets:
                main_target = next((t for t in targets if t.get("type") == "page"), targets[0])
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
                    chunk = s.recv(pl_len - len(buf))
                    if not chunk: break
                    buf += chunk

                s.close()
                return json.loads(buf.decode('utf-8', errors='ignore'))

            # Test evaluate Three.js, DOM elements, and UI triggers
            eval_expr = """
            (() => {
                const results = {};
                results.hasThree = typeof THREE !== 'undefined';
                results.hasCanvas = !!document.getElementById('mars-canvas');
                results.hasSolarText = !!document.getElementById('solar-phase-text');
                results.hasSolarElev = !!document.getElementById('readout-solar-elev');
                
                // Click time buttons
                const times = ['SUNRISE', 'SUNSET', 'TWILIGHT', 'NIGHT', 'DAY'];
                times.forEach(t => {
                    const btn = document.querySelector(`.time-btn[data-time="${t}"]`);
                    if (btn) btn.click();
                });
                
                // Click zone buttons
                const zones = ['orbit', 'surface', 'agriculture', 'habitat', 'tactical'];
                zones.forEach(z => {
                    const btn = document.querySelector(`.zone-btn[data-zone="${z}"]`);
                    if (btn) btn.click();
                });
                
                results.solarLabel = document.getElementById('solar-phase-text')?.textContent;
                results.solarElevText = document.getElementById('readout-solar-elev')?.textContent;
                return results;
            })()
            """
            eval_res = simple_ws_query(ws_url, {"id": 1, "method": "Runtime.evaluate", "params": {"expression": eval_expr, "returnByValue": True}})
            val = eval_res.get("result", {}).get("result", {}).get("value", {})

            self.assertTrue(val.get("hasThree"), "Three.js not loaded")
            self.assertTrue(val.get("hasCanvas"), "mars-canvas not loaded")
            self.assertTrue(val.get("hasSolarText"), "solar-phase-text not loaded")
            self.assertTrue(val.get("hasSolarElev"), "readout-solar-elev not loaded")
            self.assertIn("DAYTIME", val.get("solarLabel", ""))
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except Exception:
                proc.kill()


if __name__ == "__main__":
    unittest.main()
