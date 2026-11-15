"""
Milestone 10 Challenger 1 Cross-Platform Empirical & Adversarial Challenge Suite
Author: Challenger 1 (m10_challenger_crossplatform)
Scope: Empirical and adversarial stress testing of cross-platform build artifacts,
       standalone Windows player PE32+ executable integrity, Unity 6 asset metadata pairing,
       Web runtime DOM & CDP resilience under extreme boundary inputs, and master dataset invariants.
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
import urllib.request

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
STANDALONE_BUILD_DIR = os.path.join(UNITY_DIR, "Builds", "Windows")
STANDALONE_EXE = os.path.join(STANDALONE_BUILD_DIR, "MarsSentinel.exe")
WEB_DIR = os.path.join(PROJECT_ROOT, "web")
EDGE_BIN = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
CHROME_BIN = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"


# ==============================================================================
# SECTION 1: MASTER DATASET & SCENE INVARIANTS
# ==============================================================================

class MasterDatasetAndSceneInvariantTests(unittest.TestCase):
    """Empirical verification of master scene datablocks and authoritative NASA datasets."""

    def test_01_hirise_dtm_vertex_count_and_coordinate_sha256(self):
        """Verify NASA_HiRISE_DTM_Jezero_FieldSite_4m vertex count and SHA-256 coordinate hash."""
        self.assertTrue(os.path.isfile(HERO_BLEND), f"Master blend not found: {HERO_BLEND}")
        self.assertTrue(os.path.isfile(BLENDER_BIN), f"Blender binary not found: {BLENDER_BIN}")

        py_cmd = (
            "import bpy, hashlib, array; "
            "obj = bpy.data.objects['NASA_HiRISE_DTM_Jezero_FieldSite_4m']; "
            "v_cnt = len(obj.data.vertices); "
            "coords = array.array('f', [0.0]) * (v_cnt * 3); "
            "obj.data.vertices.foreach_get('co', coords); "
            "h = hashlib.sha256(coords.tobytes()).hexdigest(); "
            "print(f'M10_VERTS={v_cnt}'); "
            "print(f'M10_SHA256={h}'); "
            "print(f'M10_PACKED={sum(1 for img in bpy.data.images if img.packed_file is not None)}')"
        )

        res = subprocess.run(
            [BLENDER_BIN, "-b", HERO_BLEND, "--python-expr", py_cmd],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=45
        )
        self.assertEqual(res.returncode, 0, f"Blender evaluation failed: {res.stderr}")

        verts = None
        sha = None
        packed = None
        for line in res.stdout.splitlines():
            if line.startswith("M10_VERTS="):
                verts = int(line.split("=")[1])
            elif line.startswith("M10_SHA256="):
                sha = line.split("=")[1].strip()
            elif line.startswith("M10_PACKED="):
                packed = int(line.split("=")[1])

        self.assertEqual(verts, HIRISE_EXPECTED_VERTS, f"HiRISE vertex count mismatch: {verts} != {HIRISE_EXPECTED_VERTS}")
        self.assertEqual(sha, HIRISE_EXPECTED_SHA256, f"HiRISE SHA256 mismatch: {sha} != {HIRISE_EXPECTED_SHA256}")
        self.assertEqual(packed, 276, f"Packed image datablocks mismatch: {packed} != 276")

    def test_02_nasa_authoritative_datasets_integrity(self):
        """Verify authoritative NASA datasets in data/ are non-empty and well-formed."""
        data_dir = os.path.join(PROJECT_ROOT, "data")
        self.assertTrue(os.path.isdir(data_dir), f"data directory missing: {data_dir}")

        geojson_dir = os.path.join(data_dir, "routes_traverses")
        for fn in ["perseverance_traverse_m20.geojson", "curiosity_traverse_msl.geojson", "ingenuity_flights_m20.geojson", "sample_tubes_m20.geojson"]:
            fp = os.path.join(geojson_dir, fn)
            self.assertTrue(os.path.isfile(fp), f"Missing GeoJSON: {fn}")
            with open(fp, "r", encoding="utf-8") as f:
                gj = json.load(f)
                self.assertIn("type", gj, f"Invalid GeoJSON: {fn}")

        geology_dir = os.path.join(data_dir, "geology")
        for fn in ["usgs_sim3292_units.json", "usgs_sim3292_stratigraphy.json"]:
            fp = os.path.join(geology_dir, fn)
            self.assertTrue(os.path.isfile(fp), f"Missing geology dataset: {fn}")
            with open(fp, "r", encoding="utf-8") as f:
                geo = json.load(f)
                self.assertTrue(len(geo) > 0, f"Empty geology dataset: {fn}")


# ==============================================================================
# SECTION 2: STANDALONE WINDOWS EXECUTABLE & PE32+ INTEGRITY
# ==============================================================================

class StandaloneWindowsBinaryIntegrityTests(unittest.TestCase):
    """Stress-test standalone executable headers, binary structure, and deployment assets."""

    def test_03_standalone_executable_presence_and_pe32_headers(self):
        """Verify MarsSentinel.exe is a valid PE32+ (AMD64 x86_64) Windows GUI binary."""
        self.assertTrue(os.path.isfile(STANDALONE_EXE), f"Standalone binary not found: {STANDALONE_EXE}")
        exe_size = os.path.getsize(STANDALONE_EXE)
        self.assertGreaterEqual(exe_size, 600000, f"Executable too small: {exe_size} bytes")
        self.assertLessEqual(exe_size, 800000, f"Executable unexpectedly large: {exe_size} bytes")

        with open(STANDALONE_EXE, "rb") as f:
            dos_header = f.read(64)
            self.assertEqual(dos_header[:2], b"MZ", "Missing DOS 'MZ' signature")

            e_lfanew = struct.unpack("<I", dos_header[60:64])[0]
            f.seek(e_lfanew)
            pe_sig = f.read(4)
            self.assertEqual(pe_sig, b"PE\x00\x00", "Missing PE signature")

            coff_header = f.read(20)
            machine, num_sections, timedatestamp, ptr_sym, num_sym, opt_hdr_size, characteristics = struct.unpack("<HHIIIHH", coff_header)

            # 0x8664 = IMAGE_FILE_MACHINE_AMD64
            self.assertEqual(machine, 0x8664, f"Unexpected machine architecture: {hex(machine)}, expected 0x8664 (x86_64)")
            self.assertGreaterEqual(num_sections, 4, f"Too few PE sections: {num_sections}")

            opt_hdr = f.read(opt_hdr_size)
            magic = struct.unpack("<H", opt_hdr[:2])[0]
            # 0x020B = PE32+ (64-bit)
            self.assertEqual(magic, 0x020B, f"Unexpected optional header magic: {hex(magic)}, expected 0x020B (PE32+)")

            # Subsystem at offset 68 in PE32+ optional header
            subsystem = struct.unpack("<H", opt_hdr[68:70])[0]
            # 2 = IMAGE_SUBSYSTEM_WINDOWS_GUI
            self.assertEqual(subsystem, 2, f"Expected Windows GUI subsystem (2), got {subsystem}")

    def test_04_standalone_runtime_dependencies_and_assemblies(self):
        """Verify presence and validity of Unity runtime DLLs, crash handler, and managed assemblies."""
        required_root_files = [
            "UnityPlayer.dll",
            "UnityCrashHandler64.exe",
            "dstorage.dll",
            "dstoragecore.dll"
        ]
        for fn in required_root_files:
            fp = os.path.join(STANDALONE_BUILD_DIR, fn)
            self.assertTrue(os.path.isfile(fp), f"Missing standalone runtime file: {fn}")
            self.assertGreater(os.path.getsize(fp), 0, f"Empty file: {fn}")

        d3d12_dll = os.path.join(STANDALONE_BUILD_DIR, "D3D12", "D3D12Core.dll")
        self.assertTrue(os.path.isfile(d3d12_dll), f"Missing D3D12Core.dll at: {d3d12_dll}")

        managed_dir = os.path.join(STANDALONE_BUILD_DIR, "MarsSentinel_Data", "Managed")
        self.assertTrue(os.path.isdir(managed_dir), f"Managed assemblies folder missing: {managed_dir}")

        required_managed_dlls = [
            "Assembly-CSharp.dll",
            "UnityEngine.CoreModule.dll",
            "Unity.RenderPipelines.Universal.Runtime.dll",
            "mscorlib.dll",
            "netstandard.dll"
        ]
        for dll in required_managed_dlls:
            fp = os.path.join(managed_dir, dll)
            self.assertTrue(os.path.isfile(fp), f"Missing managed assembly: {dll}")

    def test_05_standalone_streamingassets_nasa_data_mirrored(self):
        """Verify NASA datasets are correctly staged in Standalone StreamingAssets."""
        streaming_nasa = os.path.join(STANDALONE_BUILD_DIR, "MarsSentinel_Data", "StreamingAssets", "NASA_Data")
        self.assertTrue(os.path.isdir(streaming_nasa), f"StreamingAssets NASA_Data missing: {streaming_nasa}")

        subdirs = ["routes_traverses", "geology", "elevation_dtm", "imagery"]
        for sd in subdirs:
            p = os.path.join(streaming_nasa, sd)
            self.assertTrue(os.path.isdir(p), f"Missing StreamingAssets subfolder: {sd}")
            self.assertGreater(len(os.listdir(p)), 0, f"StreamingAssets subfolder is empty: {sd}")


# ==============================================================================
# SECTION 3: UNITY 6 ASSET METADATA PAIRING & CLEAN GIT STATE
# ==============================================================================

class Unity6AssetMetadataIntegrityTests(unittest.TestCase):
    """Stress-test Unity project Asset/Meta invariants and Git working tree clean state."""

    def test_06_unity_assets_meta_file_bijective_pairing(self):
        """Verify 100% bijective pairing: 0 orphan .meta files, 0 missing .meta files."""
        assets_dir = os.path.join(UNITY_DIR, "Assets")
        self.assertTrue(os.path.isdir(assets_dir), f"Unity Assets folder missing: {assets_dir}")

        all_files = set()
        meta_files = set()

        for root, dirs, files in os.walk(assets_dir):
            for d in dirs:
                full_dir = os.path.join(root, d)
                meta_path = full_dir + ".meta"
                self.assertTrue(os.path.isfile(meta_path), f"Folder missing .meta: {full_dir}")

            for f in files:
                full_path = os.path.join(root, f)
                if f.endswith(".meta"):
                    meta_files.add(full_path)
                else:
                    all_files.add(full_path)

        missing_metas = []
        for af in all_files:
            expected_meta = af + ".meta"
            if not os.path.isfile(expected_meta):
                missing_metas.append(af)

        orphan_metas = []
        for mf in meta_files:
            target = mf[:-5]
            if not os.path.exists(target):
                orphan_metas.append(mf)

        self.assertEqual(missing_metas, [], f"Assets missing .meta files: {missing_metas}")
        self.assertEqual(orphan_metas, [], f"Orphan .meta files: {orphan_metas}")

    def test_07_unity_project_clean_git_working_tree(self):
        """Verify unity_project local Git working tree is completely clean."""
        res = subprocess.run(
            ["git", "-C", UNITY_DIR, "status", "--porcelain"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        self.assertEqual(res.returncode, 0, f"git status failed in unity_project: {res.stderr}")
        self.assertEqual(res.stdout.strip(), "", f"Dirty git working tree in unity_project:\n{res.stdout}")


# ==============================================================================
# SECTION 4: WEB RUNTIME ADVERSARIAL STRESS & CDP BOUNDARY INPUTS
# ==============================================================================

class WebRuntimeAdversarialStressTests(unittest.TestCase):
    """Adversarial stress testing of web platform under extreme boundary inputs via headless CDP."""

    @classmethod
    def setUpClass(cls):
        cls.html_path = os.path.join(WEB_DIR, "index.html")
        cls.js_path = os.path.join(WEB_DIR, "app.js")
        cls.css_path = os.path.join(WEB_DIR, "styles.css")
        with open(cls.html_path, "r", encoding="utf-8") as f:
            cls.html_code = f.read()
        with open(cls.js_path, "r", encoding="utf-8") as f:
            cls.js_code = f.read()

    def test_08_web_hud_elements_and_zone_bindings_exist(self):
        """Verify all interactive zone buttons, solar time buttons, and HUD displays exist."""
        required_zones = ["surface", "orbit", "agriculture", "habitat", "tactical"]
        for z in required_zones:
            pattern = f'data-zone="{z}"'
            self.assertIn(pattern, self.html_code, f"Missing zone button: {z}")

        required_times = ["SUNRISE", "DAY", "SUNSET", "TWILIGHT", "NIGHT"]
        for t in required_times:
            pattern = f'data-time="{t}"'
            self.assertIn(pattern, self.html_code, f"Missing time button: {t}")

        self.assertIn('id="mars-canvas"', self.html_code)
        self.assertIn('id="celestial-status-pill"', self.html_code)
        self.assertIn('id="celestial-phase-text"', self.html_code)

    def test_09_headless_browser_extreme_boundary_stress(self):
        """Subject web runtime to 150 extreme random inputs (invalid keys, extreme wheel, resize) via CDP."""
        browser_bin = EDGE_BIN if os.path.isfile(EDGE_BIN) else (CHROME_BIN if os.path.isfile(CHROME_BIN) else None)
        if not browser_bin:
            self.skipTest("No headless browser binary available for CDP inspection")

        port = 9245
        temp_dir = os.path.join(os.environ.get("TEMP", "."), "cdp_stress_m9")
        url = "file:///" + os.path.abspath(self.html_path).replace("\\", "/")

        cmd = [
            browser_bin,
            "--headless=new",
            f"--remote-debugging-port={port}",
            f"--user-data-dir={temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            url
        ]

        proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            # Poll for readiness up to 15 seconds to avoid fixed-sleep race conditions
            targets = None
            last_err = None
            start_poll = time.time()
            while time.time() - start_poll < 15.0:
                try:
                    req = urllib.request.Request(f"http://127.0.0.1:{port}/json")
                    with urllib.request.urlopen(req, timeout=2) as resp:
                        data = resp.read().decode()
                        targets = json.loads(data)
                        if targets:
                            break
                except Exception as e:
                    last_err = e
                    time.sleep(0.5)

            self.assertIsNotNone(targets, f"Headless browser failed to respond to CDP JSON query; last err: {last_err}")

            main_target = next((t for t in targets if "web/index.html" in t.get("url", "")), None)
            self.assertIsNotNone(main_target, "Could not find index.html target in CDP targets list")
            ws_url = main_target["webSocketDebuggerUrl"]

            parts = ws_url.replace("ws://", "").split("/", 1)
            host, port_str = parts[0].split(":")
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(15.0)
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

            def cdp_send(cmd_dict):
                payload = json.dumps(cmd_dict).encode('utf-8')
                mask = os.urandom(4)
                masked = bytearray(b ^ mask[i % 4] for i, b in enumerate(payload))
                if len(payload) <= 125:
                    hdr = bytes([0x81, 0x80 | len(payload)]) + mask
                elif len(payload) <= 65535:
                    hdr = bytes([0x81, 0x80 | 126]) + struct.pack("!H", len(payload)) + mask
                else:
                    hdr = bytes([0x81, 0x80 | 127]) + struct.pack("!Q", len(payload)) + mask
                s.sendall(hdr + masked)

            def cdp_recv():
                hdr_in = s.recv(2)
                if not hdr_in:
                    return None
                b2 = hdr_in[1] & 0x7f
                if b2 == 126:
                    pl_len = struct.unpack("!H", s.recv(2))[0]
                elif b2 == 127:
                    pl_len = struct.unpack("!Q", s.recv(8))[0]
                else:
                    pl_len = b2
                buf = b""
                while len(buf) < pl_len:
                    buf += s.recv(pl_len - len(buf))
                return json.loads(buf.decode('utf-8'))

            cdp_send({"id": 1, "method": "Console.enable"})
            cdp_recv()

            adversarial_script = """
            (() => {
                const errors = [];
                const warnings = [];

                window.addEventListener('error', e => errors.push(e.message || String(e)));
                window.addEventListener('unhandledrejection', e => errors.push(String(e.reason)));

                const origErr = console.error;
                console.error = (...args) => {
                    errors.push(args.map(String).join(' '));
                    origErr.apply(console, args);
                };

                const origWarn = console.warn;
                console.warn = (...args) => {
                    warnings.push(args.map(String).join(' '));
                    origWarn.apply(console, args);
                };

                const canvas = document.getElementById('mars-canvas');
                const zones = ['surface', 'orbit', 'agriculture', 'habitat', 'tactical', 'nonexistent_zone', ''];
                const times = ['SUNRISE', 'DAY', 'SUNSET', 'TWILIGHT', 'NIGHT', 'MIDNIGHT', ''];
                const invalidKeys = ['0', '7', '8', '9', 'a', 'z', 'F1', 'Escape', 'Enter', ' '];

                let ops = 0;
                for (let i = 0; i < 150; i++) {
                    // 1. Zone navigation with both valid and invalid targets
                    const z = zones[i % zones.length];
                    const zBtn = document.querySelector(`.zone-btn[data-zone="${z}"]`);
                    if (zBtn) zBtn.click();

                    // 2. Solar time buttons
                    const t = times[i % times.length];
                    const tBtn = document.querySelector(`.time-btn[data-time="${t}"]`);
                    if (tBtn) tBtn.click();

                    // 3. Extreme wheel zoom delta (+10,000, -10,000, 0)
                    if (canvas) {
                        const wheelDelta = (i % 3 === 0) ? 10000 : ((i % 3 === 1) ? -10000 : 0);
                        canvas.dispatchEvent(new WheelEvent('wheel', {
                            deltaY: wheelDelta,
                            clientX: 200,
                            clientY: 200,
                            bubbles: true
                        }));

                        // 4. Boundary pointer gestures (negative coords, offscreen)
                        canvas.dispatchEvent(new PointerEvent('pointerdown', {
                            clientX: -50, clientY: -50, buttons: 1, pointerType: 'mouse', bubbles: true
                        }));
                        window.dispatchEvent(new PointerEvent('pointermove', {
                            clientX: 5000, clientY: 5000, buttons: 1, pointerType: 'mouse', bubbles: true
                        }));
                        window.dispatchEvent(new PointerEvent('pointerup', {
                            clientX: 5000, clientY: 5000, buttons: 0, pointerType: 'mouse', bubbles: true
                        }));
                    }

                    // 5. Invalid / arbitrary keyboard events
                    const k = invalidKeys[i % invalidKeys.length];
                    window.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true }));
                    window.dispatchEvent(new KeyboardEvent('keyup', { key: k, bubbles: true }));

                    // 6. Window resize event dispatch
                    window.dispatchEvent(new Event('resize'));

                    ops++;
                }

                return {
                    ops: ops,
                    errors: errors,
                    warnings: warnings
                };
            })();
            """

            cdp_send({"id": 2, "method": "Runtime.evaluate", "params": {"expression": adversarial_script, "returnByValue": True}})
            res = None
            start_t = time.time()
            while time.time() - start_t < 15.0:
                msg = cdp_recv()
                if msg and msg.get("id") == 2:
                    res = msg
                    break

            self.assertIsNotNone(res, "Timed out waiting for Runtime.evaluate CDP response")
            eval_val = res.get("result", {}).get("result", {}).get("value", {})

            ops = eval_val.get("ops", 0)
            errors = eval_val.get("errors", [])
            warnings = eval_val.get("warnings", [])

            self.assertEqual(ops, 150, f"Expected 150 ops completed, got {ops}")
            self.assertEqual(errors, [], f"Encountered console errors during stress test: {errors}")
            self.assertEqual(warnings, [], f"Encountered console warnings during stress test: {warnings}")

            s.close()

        finally:
            proc.terminate()
            try:
                proc.wait(timeout=2)
            except Exception:
                proc.kill()


# ==============================================================================
# SECTION 5: DOCS ALLOWLIST INVARIANT
# ==============================================================================

class DocsAllowlistInvariantTests(unittest.TestCase):
    """Enforce strict docs allowlist invariant."""

    def test_10_docs_allowlist_strict_compliance(self):
        """Verify docs/ contains only authorized markdown files and zero unauthorized artifacts."""
        docs_dir = os.path.join(PROJECT_ROOT, "docs")
        self.assertTrue(os.path.isdir(docs_dir), f"docs directory missing: {docs_dir}")

        allowed = {
            "AGENTS.md",
            "ARABIC_REPORT.md",
            "ARABIC_SUMMARY_REPORT.md",
            "PROJECT_BLUEPRINT_AND_KNOWLEDGE_BASE.md",
            "SYSTEM_GUIDE.md"
        }

        actual = set(os.listdir(docs_dir))
        self.assertEqual(actual, allowed, f"docs allowlist violation! Expected: {allowed}, found: {actual}")


if __name__ == "__main__":
    unittest.main()
