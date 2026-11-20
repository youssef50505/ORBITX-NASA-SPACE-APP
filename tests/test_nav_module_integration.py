import hashlib
import json
import os
import shutil
import subprocess
import sys
import unittest

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
NAV_DIR = os.path.join(ROOT, "nav-module")
WEB_DIR = os.path.join(ROOT, "web")
DATA_DIR = os.path.join(ROOT, "data")
UNITY_DIR = os.path.join(ROOT, "unity_project")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


class TestNavModuleAndUnityOverhaulIntegration(unittest.TestCase):
    def test_01_nav_module_python_unit_tests_pass(self):
        test_file = os.path.join(NAV_DIR, "test_nav.py")
        self.assertTrue(os.path.isfile(test_file), "nav-module/test_nav.py missing")
        proc = subprocess.run(
            [sys.executable, "-B", test_file],
            cwd=NAV_DIR,
            capture_output=True,
            text=True,
            timeout=30,
        )
        self.assertEqual(proc.returncode, 0, f"nav-module tests failed:\n{proc.stdout}\n{proc.stderr}")

    def test_02_ctx_routes_json_and_streaming_assets_parity(self):
        routes_src = os.path.join(NAV_DIR, "routes.json")
        routes_unity = os.path.join(UNITY_DIR, "Assets", "StreamingAssets", "NASA_Data", "ctx_nav_routes.json")
        grid_src = os.path.join(DATA_DIR, "jez_ctx_20m_grid.json")
        grid_unity = os.path.join(UNITY_DIR, "Assets", "StreamingAssets", "NASA_Data", "jez_ctx_20m_grid.json")

        for p in (routes_src, routes_unity, grid_src, grid_unity):
            self.assertTrue(os.path.isfile(p), f"Missing required CTX nav asset: {p}")

        self.assertEqual(sha256_file(routes_src), sha256_file(routes_unity))
        self.assertEqual(sha256_file(grid_src), sha256_file(grid_unity))

        with open(routes_src, "r", encoding="utf-8") as f:
            data = json.load(f)
        self.assertGreaterEqual(len(data.get("presets", [])), 3)
        for p in data["presets"]:
            sh = p["shortest"]
            sf = p["safest"]
            self.assertLessEqual(sf["maxSlopeDeg"], sh["maxSlopeDeg"])
            self.assertGreaterEqual(sf["distanceMeters"], sh["distanceMeters"])

    def test_03_web_nav_sun_engine_algorithms(self):
        engine_path = os.path.join(WEB_DIR, "nav_sun_engine.js")
        self.assertTrue(os.path.isfile(engine_path))
        node_exe = shutil.which("node")
        if not node_exe:
            self.skipTest("node not installed in PATH")

        js_check = r"""
const eng = require(process.argv[1]);
const R = 30, C = 30, h = 80;
const z = new Float64Array(R * C);
for (let r = 0; r < R; r++) {
  for (let c = 0; c < C; c++) {
    z[r * C + c] = 160 * Math.exp(-(((r - 15) ** 2) + ((c - 15) ** 2)) / (2 * 4 * 4));
  }
}
const g = eng.NAV.prep(z, R, C, h);
eng.SUN.prep(g);
const s = 15 * C + 2, t = 15 * C + 27;
const pShort = eng.NAV.astar(g, s, t, 'shortest');
const pSafe = eng.NAV.astar(g, s, t, 'safest');
if (!pShort || !pSafe) throw new Error('A* failed to find path');
const stShort = eng.NAV.stats(g, pShort, 'shortest');
const stSafe = eng.NAV.stats(g, pSafe, 'safest');
if (!(stSafe.maxSlopeDeg < stShort.maxSlopeDeg)) throw new Error('Safest must have lower max slope');
const sunNoon = eng.SUN.pos(18.4447, 90, 12);
if (!(sunNoon.el > 50)) throw new Error('Expected high noon solar elevation');
const losBlocked = eng.SUN.los(g, s, t, 2.0);
if (losBlocked.ok !== false) throw new Error('Expected hill to block RF LOS');
const flatSite = eng.SUN.siteCheck(g, 2 * C + 2, 18.4447, 90);
if (!flatSite.pass) throw new Error('Flat corner site should pass suitability check');
console.log(JSON.stringify({ stShort, stSafe, sunNoon, losBlocked, flatSite }));
"""
        proc = subprocess.run(
            [node_exe, "-e", js_check, engine_path],
            capture_output=True,
            text=True,
            timeout=20,
        )
        self.assertEqual(proc.returncode, 0, f"JS nav_sun_engine test failed:\n{proc.stdout}\n{proc.stderr}")

    def test_04_unity_overhaul_scripts_and_shader_symbols(self):
        nasa_dir = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA")
        checks = {
            "MarsCtxNavSolarEngine.cs": ["ComputeAStar", "DouglasPeucker", "ComputeSolarPos", "CheckRadioLos", "EvaluateSiteSuitability"],
            "MarsSurvivalMissionController.cs": ["OxygenPercent", "PowerPercent", "RadiationTraumaPercent", "SpawnMissionSamples", "TriggerMedEvac"],
            "MarsCinematicVfxAndPost.cs": ["TonemappingMode.ACES", "EmitDustPuff", "EmitScienceBurst", "renderPostProcessing"],
            "MarsRoverVehicleController.cs": ["Rover_NASA_Perseverance_GLB", "ToggleAutopilot", "SphereCast", "6-point Rocker-Bogie"],
            "MarsAstronautController.cs": ["SphereCast", "landSquashTimer", "EmitDustPuff"],
            "MarsSceneManager.cs": ["SetFullHudVisible", "UpdateSurvivalBarsAndObjectiveMarker", "SurvivalVitalsPanel"],
        }
        for filename, symbols in checks.items():
            path = os.path.join(nasa_dir, filename)
            self.assertTrue(os.path.isfile(path), f"Missing Unity script: {filename}")
            with open(path, "r", encoding="utf-8") as f:
                src = f.read()
            for sym in symbols:
                self.assertIn(sym, src, f"Expected symbol '{sym}' in {filename}")

        shader_path = os.path.join(UNITY_DIR, "Assets", "Shaders", "MartianSandSlopeBlend.shader")
        with open(shader_path, "r", encoding="utf-8") as f:
            sh = f.read()
        self.assertIn("Fbm3", sh)
        self.assertIn("MixFog", sh)


if __name__ == "__main__":
    unittest.main(verbosity=2)
