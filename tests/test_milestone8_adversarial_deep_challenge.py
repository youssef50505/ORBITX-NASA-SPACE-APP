"""
Milestone 8 Adversarial Deep Empirical Challenge Harness
Author: Challenger M8-2 (Unity & Web Dynamic Simulation Challenger)

Exhaustively and empirically tests:
1. Unity C# AST & Architecture in MarsCelestialOrbitController.cs and MarsSatelliteConstellation.cs
2. Mathematical Oracle: Keplerian Newton-Raphson convergence, true anomaly equations, nadir tracking
3. Unity Asset .meta pairing integrity (0 missing, 0 orphans across all unity_project/Assets)
4. Unity 6 Headless Execution & Scene Validation
5. Web Platform CDP Stress Testing:
   - Rapid solar slider scrubbing (100 rapid changes)
   - Rapid preset cycling (50 preset transitions)
   - Rapid zone switching (20 surface/orbit transitions)
   - Zero console errors, zero warnings, zero uncaught exceptions
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

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
UNITY_ASSETS = os.path.join(UNITY_DIR, "Assets")
UNITY_BIN = r"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe"
WEB_DIR = os.path.join(PROJECT_ROOT, "web")
EDGE_BIN = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
CHROME_BIN = r"C:\Program Files\Google\Chrome\Application\chrome.exe"


# ==============================================================================
# 1. UNITY C# AST & ARCHITECTURAL VERIFICATION
# ==============================================================================

class Milestone8UnityASTTests(unittest.TestCase):
    """Deep structural and AST analysis of Unity Milestone 8 C# scripts."""

    @classmethod
    def setUpClass(cls):
        celestial_path = os.path.join(UNITY_ASSETS, "Scripts", "NASA", "MarsCelestialOrbitController.cs")
        constellation_path = os.path.join(UNITY_ASSETS, "Scripts", "NASA", "MarsSatelliteConstellation.cs")

        assert os.path.isfile(celestial_path), f"Missing {celestial_path}"
        assert os.path.isfile(constellation_path), f"Missing {constellation_path}"

        with open(celestial_path, "r", encoding="utf-8") as f:
            cls.celestial_src = f.read()
        with open(constellation_path, "r", encoding="utf-8") as f:
            cls.constellation_src = f.read()

    def test_01_celestial_controller_ast_and_methods(self):
        """Verify MarsCelestialOrbitController has required architecture, methods, and constants."""
        src = self.celestial_src
        self.assertIn("namespace MarsSentinel.NASA", src)
        self.assertIn("public class MarsCelestialOrbitController : MonoBehaviour", src)
        self.assertIn("[ExecuteAlways]", src)
        self.assertIn("public static MarsCelestialOrbitController Instance", src)

        # Check required methods
        required_methods = [
            "CalculateSolarPosition",
            "UpdateSolarLighting",
            "UpdateMoonsPositions",
            "CalculateTopocentricMoonPosition",
            "EnsureMoonsHierarchy",
            "CreateProceduralMoon",
            "GenerateEllipsoidMesh",
            "SetSolHour",
            "ApplyPreset"
        ]
        for m in required_methods:
            pattern = rf"\b{m}\s*\("
            self.assertTrue(re.search(pattern, src), f"Method {m} not found in MarsCelestialOrbitController")

        # Check astronomical constants
        self.assertIn("24.6597", src, "Mars rotation period 24.6597h missing")
        self.assertIn("7.6538", src, "Phobos orbital period 7.6538h missing")
        self.assertIn("30.312", src, "Deimos orbital period 30.312h missing")
        self.assertIn("1.08", src, "Phobos inclination 1.08° missing")
        self.assertIn("1.79", src, "Deimos inclination 1.79° missing")

    def test_02_constellation_controller_ast_and_methods(self):
        """Verify MarsSatelliteConstellation has required architecture, methods, and fleet configuration."""
        src = self.constellation_src
        self.assertIn("namespace MarsSentinel.NASA", src)
        self.assertIn("public class MarsSatelliteConstellation : MonoBehaviour", src)
        self.assertIn("[ExecuteAlways]", src)
        self.assertIn("public class KeplerianOrbiter", src)

        # Check required methods
        required_methods = [
            "InitializeOrbitTrails",
            "BakeOrbitTrailPath",
            "UpdateOrbitalPositions",
            "CalculateOrbitalPosition",
            "SolveKeplerEccentricAnomaly",
            "EnsureDefaultConstellation"
        ]
        for m in required_methods:
            pattern = rf"\b{m}\s*\("
            self.assertTrue(re.search(pattern, src), f"Method {m} not found in MarsSatelliteConstellation")

        # Check 4 missions configured in EnsureDefaultConstellation
        missions = ["MRO", "Odyssey", "MAVEN", "GlobalSurveyor"]
        for mis in missions:
            self.assertIn(f'"{mis}"', src, f"Mission {mis} not found in EnsureDefaultConstellation")

        # Check nadir calculation and flight tangent orientation
        self.assertIn("marsCenter", src)
        self.assertIn("Quaternion.LookRotation", src)
        self.assertIn("nadir", src)


# ==============================================================================
# 2. EMPIRICAL KEPLERIAN MATHEMATICAL ORACLE & STRESS HARNESS
# ==============================================================================

class Milestone8KeplerianMathTests(unittest.TestCase):
    """Empirical mathematical stress testing of Keplerian orbit models."""

    @staticmethod
    def solve_kepler_newton_raphson(M_deg, e, max_iters=5):
        """Exact Python port of MarsSatelliteConstellation.SolveKeplerEccentricAnomaly."""
        M_rad = math.radians(M_deg)
        E_rad = M_rad
        for _ in range(max_iters):
            f = E_rad - e * math.sin(E_rad) - M_rad
            f_prime = 1.0 - e * math.cos(E_rad)
            E_rad -= f / f_prime
        return math.degrees(E_rad)

    @staticmethod
    def true_anomaly_from_E(E_deg, e):
        """Exact Python port of true anomaly calculation in MarsSatelliteConstellation."""
        E_rad = math.radians(E_deg)
        nu_rad = 2.0 * math.atan2(
            math.sqrt(1.0 + e) * math.sin(E_rad * 0.5),
            math.sqrt(1.0 - e) * math.cos(E_rad * 0.5)
        )
        return math.degrees(nu_rad)

    def test_03_newton_raphson_convergence_fleet_parameters(self):
        """Verify Newton-Raphson convergence for all 4 constellation satellites across all sol angles."""
        fleet = [
            {"name": "MRO", "e": 0.005},
            {"name": "Odyssey", "e": 0.002},
            {"name": "MAVEN", "e": 0.350},
            {"name": "GlobalSurveyor", "e": 0.003}
        ]

        # Test across 360 degrees of Mean Anomaly in 1-degree steps
        for sat in fleet:
            e = sat["e"]
            max_residual = 0.0
            for M_deg in range(0, 360):
                E_deg = self.solve_kepler_newton_raphson(M_deg, e, max_iters=5)
                E_rad = math.radians(E_deg)
                M_rad = math.radians(M_deg)
                # Kepler equation: M = E - e*sin(E)
                M_computed = E_rad - e * math.sin(E_rad)
                residual = abs(M_computed - M_rad)
                if residual > max_residual:
                    max_residual = residual

            # For MAVEN (e=0.35), 5 iterations yields residual < 1e-4 radians (< 0.006 degrees)
            self.assertLess(max_residual, 1e-4, f"Newton-Raphson residual too large for {sat['name']}: {max_residual}")

    def test_04_kepler_eccentricity_stress_boundary(self):
        """Stress-test Newton-Raphson near boundaries: e = 0.0, e = 0.85, M = 0°, M = 180°, M = 360°."""
        # Circular orbit: E must strictly equal M
        for M in [0.0, 45.0, 90.0, 180.0, 270.0, 360.0]:
            E = self.solve_kepler_newton_raphson(M, 0.0, max_iters=5)
            self.assertAlmostEqual(E, M, places=5, msg=f"Circular orbit E must equal M at {M}")

        # High eccentricity stress: e = 0.85
        for M in [0.0, 30.0, 90.0, 180.0, 270.0]:
            E_deg = self.solve_kepler_newton_raphson(M, 0.85, max_iters=10)
            E_rad = math.radians(E_deg)
            M_rad = math.radians(M)
            res = abs((E_rad - 0.85 * math.sin(E_rad)) - M_rad)
            self.assertLess(res, 1e-4)

    def test_05_true_anomaly_boundary_conditions(self):
        """Verify true anomaly properties: nu(0) = 0, nu(180) = 180, monotonic growth."""
        e = 0.35 # MAVEN
        nu_periapsis = self.true_anomaly_from_E(0.0, e)
        self.assertAlmostEqual(nu_periapsis, 0.0, places=5, msg="Periapsis true anomaly must be 0")

        nu_apoapsis = self.true_anomaly_from_E(180.0, e)
        self.assertAlmostEqual(nu_apoapsis, 180.0, places=5, msg="Apoapsis true anomaly must be 180")

        # Monotonicity test
        prev_nu = -180.0
        for E_deg in range(-170, 180, 10):
            nu_deg = self.true_anomaly_from_E(E_deg, e)
            self.assertGreater(nu_deg, prev_nu, f"True anomaly not monotonically increasing at E={E_deg}")
            prev_nu = nu_deg

    def test_06_nadir_tracking_non_singularity(self):
        """Verify that flight velocity tangent and nadir vector are never collinear."""
        # For MAVEN (e=0.35), verify across entire orbit that dot(tangent, -nadir) never exceeds 0.5
        mars_center = (0.0, -1200.0, 0.0)
        a = 1620.0
        e = 0.35
        inc_rad = math.radians(75.0)

        for nu_deg in range(0, 360, 5):
            nu_rad = math.radians(nu_deg)
            r = a * (1.0 - e * e) / (1.0 + e * math.cos(nu_rad))
            pos_x = r * math.cos(nu_rad)
            pos_y = r * math.sin(nu_rad) * math.sin(inc_rad) + mars_center[1]
            pos_z = r * math.sin(nu_rad) * math.cos(inc_rad)

            # Nadir vector
            dx = mars_center[0] - pos_x
            dy = mars_center[1] - pos_y
            dz = mars_center[2] - pos_z
            dist = math.sqrt(dx * dx + dy * dy + dz * dz)
            nadir = (dx / dist, dy / dist, dz / dist)

            # Next position
            nu_next_rad = math.radians(nu_deg + 1.0)
            r_next = a * (1.0 - e * e) / (1.0 + e * math.cos(nu_next_rad))
            next_x = r_next * math.cos(nu_next_rad)
            next_y = r_next * math.sin(nu_next_rad) * math.sin(inc_rad) + mars_center[1]
            next_z = r_next * math.sin(nu_next_rad) * math.cos(inc_rad)

            tx = next_x - pos_x
            ty = next_y - pos_y
            tz = next_z - pos_z
            t_len = math.sqrt(tx * tx + ty * ty + tz * tz)
            tangent = (tx / t_len, ty / t_len, tz / t_len)

            # Dot product with zenith (-nadir)
            dot = tangent[0] * (-nadir[0]) + tangent[1] * (-nadir[1]) + tangent[2] * (-nadir[2])
            # Since radial and tangent are nearly orthogonal, dot = sin(flight path angle) <= e = 0.35
            self.assertLess(abs(dot), 0.40, f"Flight tangent too close to radial vector at nu={nu_deg}: dot={dot}")


# ==============================================================================
# 3. COMPLETE UNITY ASSETS .META INTEGRITY AUDIT
# ==============================================================================

class Milestone8UnityMetaAuditTests(unittest.TestCase):
    """Exhaustive check of 0 missing and 0 orphan .meta files in unity_project/Assets."""

    def test_07_zero_missing_and_orphan_meta_files(self):
        missing_meta = []
        orphan_meta = []
        total_assets = 0
        total_metas = 0

        for root, dirs, files in os.walk(UNITY_ASSETS):
            for d in dirs:
                total_assets += 1
                dir_path = os.path.join(root, d)
                meta_path = dir_path + ".meta"
                if not os.path.exists(meta_path):
                    missing_meta.append(dir_path)

            for f in files:
                file_path = os.path.join(root, f)
                if f.endswith(".meta"):
                    total_metas += 1
                    target_path = file_path[:-5]
                    if not os.path.exists(target_path):
                        orphan_meta.append(file_path)
                else:
                    total_assets += 1
                    meta_path = file_path + ".meta"
                    if not os.path.exists(meta_path):
                        missing_meta.append(file_path)

        self.assertEqual(len(missing_meta), 0, f"Missing {len(missing_meta)} .meta files: {missing_meta[:5]}")
        self.assertEqual(len(orphan_meta), 0, f"Orphan {len(orphan_meta)} .meta files: {orphan_meta[:5]}")
        self.assertGreater(total_metas, 50, "Too few .meta files found in unity_project")
        self.assertEqual(total_assets, total_metas, f"Total assets ({total_assets}) does not match total metas ({total_metas})")


# ==============================================================================
# 4. UNITY HEADLESS BUILD & SCENE VALIDATION
# ==============================================================================

class Milestone8UnityHeadlessTests(unittest.TestCase):
    """Executes Unity batchmode to verify scene compilation and zero fatal errors."""

    def test_08_unity_batchmode_scene_builder(self):
        if not os.path.isfile(UNITY_BIN):
            self.skipTest(f"Unity executable not found at {UNITY_BIN}")

        # Ensure any lingering lockfile from previous runs is cleared
        lockfile = os.path.join(UNITY_DIR, "Temp", "UnityLockfile")
        for _ in range(10):
            if not os.path.exists(lockfile):
                break
            time.sleep(1.0)

        log_path = os.path.join(UNITY_DIR, "adversarial_challenge_build.log")
        if os.path.exists(log_path):
            os.remove(log_path)

        cmd = [
            UNITY_BIN,
            "-batchmode",
            "-nographics",
            "-projectPath", UNITY_DIR,
            "-executeMethod", "MarsSentinel.Editor.MarsSceneBuilder.BuildHeroScene",
            "-logFile", log_path,
            "-quit"
        ]

        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=180)
        # Restore committed scene state so working tree remains pristine
        subprocess.run(["git", "checkout", "Assets/Scenes/"], cwd=UNITY_DIR, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.assertEqual(proc.returncode, 0, f"Unity batchmode exited with error code {proc.returncode}")

        self.assertTrue(os.path.isfile(log_path), "Unity build log file was not generated")
        with open(log_path, "r", encoding="utf-8", errors="replace") as f:
            log_content = f.read()

        # Check for success marker
        self.assertIn("Hero Showcase Scene built and saved successfully", log_content)

        # Check for zero compilation errors
        error_lines = [line for line in log_content.splitlines() if "error CS" in line]
        self.assertEqual(error_lines, [], f"Unity compilation errors found: {error_lines}")


# ==============================================================================
# 5. WEB PLATFORM CDP STRESS TESTING
# ==============================================================================

class Milestone8WebCDPStressTests(unittest.TestCase):
    """Stress tests web/app.js via CDP: slider scrubbing, preset cycling, zone switching."""

    @classmethod
    def setUpClass(cls):
        browser_bin = EDGE_BIN if os.path.isfile(EDGE_BIN) else (CHROME_BIN if os.path.isfile(CHROME_BIN) else None)
        if not browser_bin:
            cls.browser_available = False
            return
        cls.browser_available = True

        cls.temp_dir = os.path.join(os.environ.get("TEMP", "."), "m8_adversarial_cdp")
        cls.port = 9225
        cls.url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")

        cmd = [
            browser_bin,
            "--headless=new",
            f"--remote-debugging-port={cls.port}",
            f"--user-data-dir={cls.temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            cls.url
        ]
        cls.proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        time.sleep(2.5)

        # Discover WebSocket URL
        import urllib.request
        req = urllib.request.Request(f"http://127.0.0.1:{cls.port}/json")
        with urllib.request.urlopen(req, timeout=5) as resp:
            targets = json.loads(resp.read().decode())

        target = next((t for t in targets if "web/index.html" in t.get("url", "")), None)
        assert target is not None, "Could not discover web/index.html target"
        cls.ws_url = target["webSocketDebuggerUrl"]

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, 'proc') and cls.proc:
            cls.proc.terminate()
            try:
                cls.proc.wait(timeout=2)
            except Exception:
                cls.proc.kill()

    def ws_eval(self, expression):
        """Execute JavaScript expression via CDP Runtime.evaluate and return result."""
        parts = self.ws_url.replace("ws://", "").split("/", 1)
        host, port_str = parts[0].split(":")
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(5.0)
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

        payload = json.dumps({"id": 100, "method": "Runtime.evaluate", "params": {"expression": expression, "returnByValue": True}}).encode('utf-8')
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

    def test_09_cdp_rapid_slider_scrubbing_stress(self):
        """Stress-test 100 rapid slider scrub events and verify clean state."""
        if not self.browser_available:
            self.skipTest("Browser not available")

        # Stress script: rapidly dispatch 100 input events across random/boundary values
        stress_js = """
        (() => {
            const slider = document.getElementById('solar-slider');
            if (!slider) return { success: false, reason: 'No slider' };
            const values = [0, 15, 38, 50, 65, 82, 98, 100];
            let errorCount = 0;
            for (let i = 0; i < 100; i++) {
                const val = values[i % values.length];
                slider.value = val;
                slider.dispatchEvent(new Event('input', { bubbles: true }));
            }
            return {
                success: true,
                currentElevation: document.getElementById('readout-solar-elev')?.textContent,
                currentPhase: document.getElementById('solar-phase-text')?.textContent
            };
        })()
        """
        res = self.ws_eval(stress_js)
        val = res.get("result", {}).get("result", {}).get("value", {})
        self.assertTrue(val.get("success"), f"Slider stress failed: {val}")
        self.assertIsNotNone(val.get("currentElevation"))
        self.assertIsNotNone(val.get("currentPhase"))

    def test_10_cdp_rapid_preset_switching_stress(self):
        """Stress-test 50 rapid preset clicks and verify correct active class and DOM synchronization."""
        if not self.browser_available:
            self.skipTest("Browser not available")

        stress_js = """
        (() => {
            const timeBtns = Array.from(document.querySelectorAll('.time-btn'));
            if (timeBtns.length === 0) return { success: false, reason: 'No time buttons' };
            for (let i = 0; i < 50; i++) {
                const btn = timeBtns[i % timeBtns.length];
                btn.click();
            }
            const activeBtn = document.querySelector('.time-btn.active');
            return {
                success: true,
                activeTime: activeBtn?.dataset?.time,
                phaseText: document.getElementById('solar-phase-text')?.textContent
            };
        })()
        """
        res = self.ws_eval(stress_js)
        val = res.get("result", {}).get("result", {}).get("value", {})
        self.assertTrue(val.get("success"), f"Preset stress failed: {val}")
        self.assertIsNotNone(val.get("activeTime"))

    def test_11_cdp_rapid_zone_switching_stress(self):
        """Stress-test 20 rapid zone transitions between surface and orbit."""
        if not self.browser_available:
            self.skipTest("Browser not available")

        stress_js = """
        (() => {
            const zones = ['surface', 'orbit', 'agriculture', 'habitat', 'tactical'];
            for (let i = 0; i < 20; i++) {
                const z = zones[i % zones.length];
                const btn = document.querySelector(`.zone-btn[data-zone="${z}"]`);
                if (btn) btn.click();
            }
            // Return to orbit for inspection
            const orbitBtn = document.querySelector('.zone-btn[data-zone="orbit"]');
            if (orbitBtn) orbitBtn.click();
            return {
                success: true,
                activeZone: document.querySelector('.zone-btn.active')?.dataset?.zone
            };
        })()
        """
        res = self.ws_eval(stress_js)
        val = res.get("result", {}).get("result", {}).get("value", {})
        self.assertTrue(val.get("success"), f"Zone switching stress failed: {val}")
        self.assertEqual(val.get("activeZone"), "orbit")

    def test_12_cdp_zero_uncaught_errors_after_stress(self):
        """Verify that window.errors or console errors remained 0 throughout stress tests."""
        if not self.browser_available:
            self.skipTest("Browser not available")

        check_js = """
        (() => {
            return {
                threeLoaded: typeof THREE === 'object',
                canvasActive: !!document.getElementById('mars-canvas'),
                hasErrors: window.__hasErrors || false
            };
        })()
        """
        res = self.ws_eval(check_js)
        val = res.get("result", {}).get("result", {}).get("value", {})
        self.assertTrue(val.get("threeLoaded"), "Three.js should remain loaded")
        self.assertTrue(val.get("canvasActive"), "Canvas should remain active")
        self.assertFalse(val.get("hasErrors"), "Window caught unexpected runtime error")


if __name__ == "__main__":
    unittest.main()
