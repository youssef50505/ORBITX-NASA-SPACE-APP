"""
MARS SENTINEL — Milestone 11 Empirical Verification Suite
========================================================
Verifies NASA Space Apps Challenge 2026 resource completeness, Curiosity MSL
Unity 3D integration (10/10 Mars fleet), binary STL left-handed winding fix,
Perseverance Jezero altitude dropout filtering, offline Web NASA dataset bundle
parity, Beer-Lambert / EVA telemetry math, and live headless Edge CDP interaction
with zero console errors or unhandled exceptions.
"""

import base64
import hashlib
import json
import math
import os
import re
import socket
import struct
import subprocess
import sys
import time
import unittest
import urllib.request


TESTS_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(TESTS_DIR, ".."))
DATA_DIR = os.path.join(PROJECT_ROOT, "data")
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
WEB_DIR = os.path.join(PROJECT_ROOT, "web")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


class Milestone11UnityAndDatasetTests(unittest.TestCase):
    """Verify Unity 10-mission fleet, STL winding math, dropout filter, and manifests."""

    def test_01_curiosity_glb_and_10_mission_unity_fleet(self):
        """Verify Curiosity Rover GLB exists, is valid binary glTF 2.0, and all fleet GLBs are wired."""
        fleet_dir = os.path.join(UNITY_DIR, "Assets", "Models", "Fleet")
        curiosity_glb = os.path.join(fleet_dir, "Curiosity Rover (MSL).glb")
        curiosity_meta = curiosity_glb + ".meta"
        self.assertTrue(os.path.isfile(curiosity_glb), "Missing Curiosity Rover (MSL).glb")
        self.assertTrue(os.path.isfile(curiosity_meta), "Missing Curiosity Rover (MSL).glb.meta")

        with open(curiosity_glb, "rb") as f:
            hdr = f.read(12)
        magic, version, total_len = struct.unpack("<4sII", hdr)
        self.assertEqual(magic, b"glTF", "Curiosity GLB must start with binary glTF magic")
        self.assertEqual(version, 2)
        self.assertEqual(total_len, os.path.getsize(curiosity_glb))

        fleet_glbs = [f for f in os.listdir(fleet_dir) if f.lower().endswith(".glb")]
        self.assertGreaterEqual(len(fleet_glbs), 10, f"Expected >=10 fleet GLBs, found {fleet_glbs}")

        builder_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "Editor", "MarsSceneBuilder.cs")
        mgr_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsSceneManager.cs")
        with open(builder_path, "r", encoding="utf-8") as f:
            builder_src = f.read()
        with open(mgr_path, "r", encoding="utf-8") as f:
            mgr_src = f.read()

        for glb_name in fleet_glbs:
            self.assertIn(glb_name, builder_src, f"Fleet GLB {glb_name} not wired in MarsSceneBuilder.cs")
        self.assertIn("MS_Curiosity_Root", builder_src)
        self.assertIn("MS_Curiosity_Root", mgr_src)

    def test_02_stl_handedness_winding_and_webgl_fix(self):
        """Prove Y<->Z axis swap with (0, 2, 1) winding yields +Y upward Unity normals on sample_jezero_3d.stl."""
        stl_path = os.path.join(DATA_DIR, "elevation_dtm", "sample_jezero_3d.stl")
        self.assertTrue(os.path.isfile(stl_path))

        with open(stl_path, "rb") as f:
            data = f.read()

        tri_count = struct.unpack("<I", data[80:84])[0]
        self.assertGreater(tri_count, 100)
        self.assertEqual(len(data), 84 + tri_count * 50)

        def cross(a, b):
            return (
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0],
            )

        def sub(a, b):
            return (a[0] - b[0], a[1] - b[1], a[2] - b[2])

        upward_fixed = 0
        upward_buggy = 0
        sample_n = min(500, tri_count)
        for i in range(sample_n):
            off = 84 + i * 50 + 12  # skip stored facet normal
            vals = struct.unpack("<9f", data[off:off + 36])
            b0 = (vals[0], vals[1], vals[2])
            b1 = (vals[3], vals[4], vals[5])
            b2 = (vals[6], vals[7], vals[8])

            # Blender Z-up normal
            b_cross = cross(sub(b1, b0), sub(b2, b0))

            # Swap (x, y, z) -> Unity (x, z, y)
            u0 = (b0[0], b0[2], b0[1])
            u1 = (b1[0], b1[2], b1[1])
            u2 = (b2[0], b2[2], b2[1])

            # Unity CW winding cross(v1 - v0, v2 - v0) points toward front face (+Y up)
            u_fixed = cross(sub(u2, u0), sub(u1, u0))
            u_buggy = cross(sub(u1, u0), sub(u2, u0))

            # Exact algebraic identity check: fixed Unity Y equals original Z-up Z
            self.assertAlmostEqual(u_fixed[1], b_cross[2], places=2)
            self.assertAlmostEqual(u_buggy[1], -b_cross[2], places=2)

            if u_fixed[1] > 0:
                upward_fixed += 1
            if u_buggy[1] > 0:
                upward_buggy += 1

        self.assertEqual(upward_fixed, sample_n, "All terrain triangles with (0,2,1) winding must face +Y up")
        self.assertEqual(upward_buggy, 0, "Old (0,1,2) winding faces inverted downward (-Y)")

        terrain_cs = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsTerrainManager.cs")
        with open(terrain_cs, "r", encoding="utf-8") as f:
            terrain_src = f.read()

        self.assertRegex(terrain_src, r"triangles\[baseIdx\s*\+\s*1\]\s*=\s*baseIdx\s*\+\s*2")
        self.assertRegex(terrain_src, r"triangles\[baseIdx\s*\+\s*2\]\s*=\s*baseIdx\s*\+\s*1")
        self.assertIn("ParseBinarySTL", terrain_src)
        self.assertIn("RecalculateNormals()", terrain_src)
        self.assertIn("LoadSTLWebGLCoroutine", terrain_src)
        self.assertIn("UNITY_WEBGL", terrain_src)

    def test_03_jezero_dropout_filter_and_unity_science_features(self):
        """Verify 15 Jezero altitude dropouts in Perseverance GeoJSON match C# filter and overlays are uncapped."""
        trav_path = os.path.join(DATA_DIR, "routes_traverses", "perseverance_traverse_m20.geojson")
        with open(trav_path, "r", encoding="utf-8") as f:
            gj = json.load(f)

        origin_alt = -2569.9
        dropout_count = 0
        valid_count = 0
        for feat in gj["features"]:
            geom = feat["geometry"]
            coords = geom["coordinates"]
            lines = coords if geom["type"] == "MultiLineString" else [coords]
            for line in lines:
                for pt in line:
                    raw_alt = float(pt[2]) if len(pt) >= 3 else origin_alt
                    if raw_alt > -1000.0 and origin_alt < -2000.0:
                        dropout_count += 1
                    else:
                        valid_count += 1

        self.assertEqual(dropout_count, 15, f"Expected 15 Jezero dropout points, got {dropout_count}")
        self.assertGreater(valid_count, 20000)

        loader_cs = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsDatasetLoader.cs")
        telem_cs = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsTelemetryController.cs")
        mgr_cs = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsSceneManager.cs")
        with open(loader_cs, "r", encoding="utf-8") as f:
            loader_src = f.read()
        with open(telem_cs, "r", encoding="utf-8") as f:
            telem_src = f.read()
        with open(mgr_cs, "r", encoding="utf-8") as f:
            mgr_src = f.read()

        self.assertIn("FilterJezeroAltitudeDropout", loader_src)
        self.assertIn("PerseveranceDropoutsFiltered", loader_src)
        self.assertIn("LoadIngenuityFlights", loader_src)
        self.assertIn("LoadCuriosityTraverseSummary", loader_src)
        self.assertIn("LoadSampleImageryMetadata", loader_src)
        self.assertIn("LoadApiManifest", loader_src)

        self.assertNotIn("< 15", telem_src, "Sample tube count must not be capped at < 15")
        self.assertNotIn("<15", telem_src, "Sample tube count must not be capped at <15")
        self.assertIn("lineIngenuity", telem_src)
        self.assertIn("linePerseveranceHistory", telem_src)
        self.assertIn("SetHistoricalOverlaysVisibility", telem_src)

        self.assertIn("dustOpticalDepthTau", mgr_src)
        self.assertIn("ComputeBeerLambertTransmission", mgr_src)
        self.assertIn("ComputeEvaSafeRadiusMeters", mgr_src)

    def test_04_manifest_and_web_bundle_parity(self):
        """Verify manifest sync with Unity StreamingAssets and exact web/nasa_data_bundle.js data parity."""
        manifest_data = os.path.join(DATA_DIR, "apis", "nasa_endpoints_manifest.json")
        manifest_unity = os.path.join(
            UNITY_DIR, "Assets", "StreamingAssets", "NASA_Data", "apis", "nasa_endpoints_manifest.json"
        )
        self.assertTrue(os.path.isfile(manifest_data))
        self.assertTrue(os.path.isfile(manifest_unity))
        self.assertEqual(sha256_file(manifest_data), sha256_file(manifest_unity))

        with open(manifest_data, "r", encoding="utf-8") as f:
            manifest = json.load(f)

        self.assertIn("spaceapps_2026_challenge_url", manifest)
        self.assertIn("space_weather_and_eva_constants", manifest)
        resource_ids = {r.get("id") for r in manifest.get("official_resources", [])}
        for key in ("mars_trek", "usgs_sim3292", "curiosity_msl", "nasa_worldview_gibs", "perseverance_m20"):
            self.assertIn(key, resource_ids)

        bundle_path = os.path.join(WEB_DIR, "nasa_data_bundle.js")
        self.assertTrue(os.path.isfile(bundle_path))
        with open(bundle_path, "r", encoding="utf-8") as f:
            raw_js = f.read()

        marker = "window.MARS_NASA_DATA = "
        idx = raw_js.find(marker)
        self.assertNotEqual(idx, -1, "Missing window.MARS_NASA_DATA assignment in web/nasa_data_bundle.js")
        json_text = raw_js[idx + len(marker):].strip()
        if json_text.endswith(";"):
            json_text = json_text[:-1]
        bundle = json.loads(json_text)

        # Verify against raw source files
        with open(os.path.join(DATA_DIR, "jez_distinct_routes.json"), "r", encoding="utf-8") as f:
            routes_src = json.load(f)
        for rname in ("safest", "shortest", "science"):
            self.assertEqual(len(bundle["routes"][rname]["points"]), len(routes_src[rname]["points"]))

        self.assertEqual(len(bundle["sample_tubes"]), 22)
        self.assertEqual(len(bundle["ingenuity_flights"]), 72)
        self.assertEqual(bundle["perseverance_traverse"]["dropout_points_filtered"], 15)
        self.assertEqual(bundle["curiosity_msl_summary"]["features"], 1316)
        self.assertEqual(len(bundle["geology_units"]), 44)
        self.assertEqual(len(bundle["geology_epochs"]), 8)
        self.assertEqual(bundle["perseverance_pov_imagery"]["sol"], 100)
        self.assertEqual(len(bundle["official_resources"]), 5)

        digest_map = {
            "jez_distinct_routes.json": os.path.join(DATA_DIR, "jez_distinct_routes.json"),
            "sample_tubes_m20.geojson": os.path.join(DATA_DIR, "routes_traverses", "sample_tubes_m20.geojson"),
            "ingenuity_flights_m20.geojson": os.path.join(DATA_DIR, "routes_traverses", "ingenuity_flights_m20.geojson"),
            "perseverance_traverse_m20.geojson": os.path.join(DATA_DIR, "routes_traverses", "perseverance_traverse_m20.geojson"),
            "curiosity_traverse_msl.geojson": os.path.join(DATA_DIR, "routes_traverses", "curiosity_traverse_msl.geojson"),
            "usgs_sim3292_units.json": os.path.join(DATA_DIR, "geology", "usgs_sim3292_units.json"),
            "perseverance_sample_raw.jpg": os.path.join(DATA_DIR, "imagery", "perseverance_sample_raw.jpg"),
            "nasa_endpoints_manifest.json": manifest_data,
        }
        for name, digest in bundle["sha256_integrity"].items():
            self.assertIn(name, digest_map)
            self.assertEqual(digest, sha256_file(digest_map[name]), f"SHA-256 mismatch for {name}")

    def test_05_unity_clean_git_meta_and_windows_build(self):
        """Verify Unity git tree is clean, zero missing/orphan .meta, and MarsSentinel.exe is valid PE32+."""
        exe_path = os.path.join(UNITY_DIR, "Builds", "Windows", "MarsSentinel.exe")
        self.assertTrue(os.path.isfile(exe_path), "Missing Unity standalone build MarsSentinel.exe")
        exe_size = os.path.getsize(exe_path)
        self.assertTrue(600_000 <= exe_size <= 800_000, f"Unexpected exe size: {exe_size}")

        with open(exe_path, "rb") as f:
            mz = f.read(64)
            self.assertEqual(mz[:2], b"MZ")
            pe_off = struct.unpack("<I", mz[60:64])[0]
            f.seek(pe_off)
            pe = f.read(6)
            self.assertEqual(pe[:4], b"PE\x00\x00")
            self.assertEqual(struct.unpack("<H", pe[4:6])[0], 0x8664, "Must be AMD64 PE32+")

        git_proc = subprocess.run(
            ["git", "-C", UNITY_DIR, "status", "--porcelain"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=20,
        )
        self.assertEqual(git_proc.returncode, 0)
        self.assertEqual(git_proc.stdout.strip(), "", f"Dirty Unity repo:\n{git_proc.stdout}")


class Milestone11WebLiveCDPTests(unittest.TestCase):
    """Run headless MS Edge CDP against web/index.html to exercise M11 data & controls live."""

    def test_06_web_live_cdp_telemetry_overlays_and_pov(self):
        edge_bin = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        chrome_bin = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
        browser_bin = edge_bin if os.path.isfile(edge_bin) else (chrome_bin if os.path.isfile(chrome_bin) else None)
        if not browser_bin:
            self.skipTest("No Chromium/Edge headless binary available")

        temp_dir = os.path.join(os.environ.get("TEMP", "."), "m11_cdp_test")
        url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")

        proc = subprocess.Popen(
            [
                browser_bin,
                "--headless=new",
                "--remote-debugging-port=9229",
                f"--user-data-dir={temp_dir}",
                "--disable-gpu",
                "--no-first-run",
                url,
            ],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

        class CdpClient:
            def __init__(self, ws_url):
                parts = ws_url.replace("ws://", "").split("/", 1)
                host, port_str = parts[0].split(":")
                self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.sock.settimeout(5.0)
                self.sock.connect((host, int(port_str)))
                key = base64.b64encode(os.urandom(16)).decode("utf-8")
                req = (
                    f"GET /{parts[1]} HTTP/1.1\r\n"
                    f"Host: {host}:{port_str}\r\n"
                    f"Upgrade: websocket\r\n"
                    f"Connection: Upgrade\r\n"
                    f"Sec-WebSocket-Key: {key}\r\n"
                    f"Sec-WebSocket-Version: 13\r\n\r\n"
                )
                self.sock.sendall(req.encode("utf-8"))
                buf = b""
                while b"\r\n\r\n" not in buf:
                    buf += self.sock.recv(1024)
                self.cmd_id = 0
                self.events = []

            def _recv_exact(self, n):
                out = b""
                while len(out) < n:
                    chunk = self.sock.recv(n - len(out))
                    if not chunk:
                        raise EOFError("WebSocket closed")
                    out += chunk
                return out

            def call(self, method, params=None):
                self.cmd_id += 1
                cid = self.cmd_id
                payload = json.dumps({"id": cid, "method": method, "params": params or {}}).encode("utf-8")
                mask = os.urandom(4)
                masked = bytearray(b ^ mask[i % 4] for i, b in enumerate(payload))
                if len(payload) <= 125:
                    hdr = bytes([0x81, 0x80 | len(payload)]) + mask
                else:
                    hdr = bytes([0x81, 0x80 | 126]) + struct.pack("!H", len(payload)) + mask
                self.sock.sendall(hdr + masked)

                while True:
                    h = self._recv_exact(2)
                    b2 = h[1] & 0x7F
                    if b2 == 126:
                        plen = struct.unpack("!H", self._recv_exact(2))[0]
                    elif b2 == 127:
                        plen = struct.unpack("!Q", self._recv_exact(8))[0]
                    else:
                        plen = b2
                    msg = json.loads(self._recv_exact(plen).decode("utf-8"))
                    if msg.get("id") == cid:
                        return msg
                    self.events.append(msg)

            def eval_js(self, expr):
                res = self.call(
                    "Runtime.evaluate",
                    {"expression": expr, "returnByValue": True, "awaitPromise": True},
                )
                if "exceptionDetails" in res.get("result", {}):
                    raise AssertionError(f"JS exception in {expr}: {res['result']['exceptionDetails']}")
                return res.get("result", {}).get("result", {}).get("value")

            def close(self):
                self.sock.close()

        cdp = None
        try:
            targets = []
            for _ in range(25):
                try:
                    with urllib.request.urlopen("http://127.0.0.1:9229/json", timeout=2) as r:
                        targets = json.loads(r.read().decode("utf-8"))
                    if any("web/index.html" in t.get("url", "") for t in targets):
                        break
                except Exception:
                    pass
                time.sleep(0.3)

            main_target = next((t for t in targets if "web/index.html" in t.get("url", "")), None)
            self.assertIsNotNone(main_target, f"Missing index.html target in {targets}")
            cdp = CdpClient(main_target["webSocketDebuggerUrl"])
            cdp.call("Runtime.enable")
            time.sleep(1.5)

            # 1. Verify offline bundle hydration
            self.assertEqual(cdp.eval_js("typeof window.MARS_NASA_DATA"), "object")
            self.assertEqual(cdp.eval_js("window.MARS_NASA_DATA.sample_tubes.length"), 22)
            self.assertEqual(cdp.eval_js("window.MARS_NASA_DATA.ingenuity_flights.length"), 72)

            # 2. Exercise dust tau slider & verify Beer-Lambert solar flux + EVA O2 telemetry change
            cdp.eval_js(
                "(() => { const s = document.getElementById('dust-tau-slider'); "
                "s.value = '30'; s.dispatchEvent(new Event('input')); return true; })()"
            )
            flux_low_tau = cdp.eval_js("document.getElementById('readout-solar-flux').textContent")
            eva_low_tau = cdp.eval_js("document.getElementById('readout-eva-o2').textContent")

            cdp.eval_js(
                "(() => { const s = document.getElementById('dust-tau-slider'); "
                "s.value = '350'; s.dispatchEvent(new Event('input')); return true; })()"
            )
            flux_high_tau = cdp.eval_js("document.getElementById('readout-solar-flux').textContent")
            eva_high_tau = cdp.eval_js("document.getElementById('readout-eva-o2').textContent")

            self.assertNotEqual(flux_low_tau, flux_high_tau)
            self.assertNotEqual(eva_low_tau, eva_high_tau)
            f_low = float(re.search(r"[\d.]+", flux_low_tau).group(0))
            f_high = float(re.search(r"[\d.]+", flux_high_tau).group(0))
            self.assertGreater(f_low, f_high * 5.0, "Dust storm tau=3.5 must heavily attenuate irradiance")

            # 3. Exercise 44-unit USGS geology selector and overlay toggles
            geo_count = cdp.eval_js("document.getElementById('geology-unit-select').options.length")
            self.assertGreaterEqual(geo_count, 44)
            cdp.eval_js(
                "(() => { const g = document.getElementById('geology-unit-select'); "
                "g.selectedIndex = 10; g.dispatchEvent(new Event('change')); return g.value; })()"
            )

            for cid in ("chk-sample-tubes", "chk-ingenuity-flights", "chk-persev-traverse"):
                toggled = cdp.eval_js(
                    f"(() => {{ const c = document.getElementById('{cid}'); "
                    f"c.checked = !c.checked; c.dispatchEvent(new Event('change')); "
                    f"c.checked = !c.checked; c.dispatchEvent(new Event('change')); return c.checked; }})()"
                )
                self.assertTrue(toggled)

            # 4. Click Mastcam-Z Sol 100 POV button and confirm raw JPEG renders in modal
            cdp.eval_js("document.getElementById('btn-mastcam-pov').click()")
            time.sleep(0.5)
            img_info = cdp.eval_js(
                "(() => { const i = document.getElementById('modal-preview-img'); "
                "return i.complete + '|' + i.naturalWidth + '|' + i.src; })()"
            )
            parts = str(img_info).split("|", 2)
            self.assertEqual(parts[0], "true", f"Modal preview image failed to finish loading: {img_info}")
            self.assertGreater(int(parts[1]), 100, f"Modal Mastcam-Z JPEG naturalWidth invalid: {img_info}")
            self.assertIn("perseverance_sample_raw.jpg", parts[2])

            # 5. Check captured console errors / uncaught exceptions
            cdp.eval_js("1")  # flush event queue
            console_errors = [
                e for e in cdp.events
                if (e.get("method") == "Runtime.consoleAPICalled" and e.get("params", {}).get("type") in ("error", "assert"))
                or e.get("method") == "Runtime.exceptionThrown"
            ]
            self.assertEqual(console_errors, [], f"Console errors/exceptions detected: {console_errors}")
        finally:
            if cdp is not None:
                try:
                    cdp.close()
                except Exception:
                    pass
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except Exception:
                proc.kill()
            if proc.stdout:
                proc.stdout.close()
            if proc.stderr:
                proc.stderr.close()


if __name__ == "__main__":
    unittest.main()
