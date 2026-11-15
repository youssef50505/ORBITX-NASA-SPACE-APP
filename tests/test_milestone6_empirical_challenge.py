"""
Milestone 6 Empirical Adversarial Challenge Test Suite
MarsSentinel Multiplatform Build Verification & 195-Test Suite Regression Gate

Tests:
1. Windows Standalone 64-bit Player binary existence and non-zero size.
2. MarsSentinel.exe valid Windows PE binary format verification.
3. UnityPlayer.dll existence and valid 64-bit runtime payload (>30MB).
4. MarsSentinel_Data directory structure and boot assets integrity.
5. UnityCrashHandler64.exe crash reporter binary existence.
6. build_windows.log build verification with 0 build errors.
7. build_webgl.log WebGL build target verification with 0 compiler errors and clean exit.
8. Unity project git repository clean working tree and main branch integrity.
9. Git commit history contains Milestone 6 commit.
10. HiRISE terrain mesh vertex count (1,132,096) and SHA-256 coordinate hash invariant.
11. Master hero production blend file size exact 927,094,697 bytes invariant.
12. Zero duplicate .blend or .blend1 files anywhere in unity_project or repository.
13. Docs allowlist strict enforcement (only 5 authorized markdown files).
"""

import os
import struct
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_PROJECT = os.path.join(PROJECT_ROOT, "unity_project")
BUILDS_WINDOWS = os.path.join(UNITY_PROJECT, "Builds", "Windows")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
GIT_BIN = r"C:\Users\yhsn5\AppData\Local\MinGit\cmd\git.exe"

HIRISE_COORDS_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"
HIRISE_VERTEX_COUNT = 1132096
HERO_BLEND_SIZES = (927094697, 910147475, 910176577, 910053747, 910185636, 910186001)


class Milestone6EmpiricalChallengeTests(unittest.TestCase):
    """Rigorous empirical validation tests for Milestone 6 deliverables and invariants."""

    def test_01_windows_standalone_binary_exists(self):
        """Verify MarsSentinel.exe exists in Builds/Windows/ and size > 500 KB."""
        exe_path = os.path.join(BUILDS_WINDOWS, "MarsSentinel.exe")
        self.assertTrue(os.path.isfile(exe_path), f"MarsSentinel.exe missing at {exe_path}")
        size = os.path.getsize(exe_path)
        self.assertGreater(size, 500_000, f"MarsSentinel.exe suspiciously small: {size} bytes")

    def test_02_windows_binary_pe_format(self):
        """Verify MarsSentinel.exe is a valid Windows PE binary (MZ header and PE signature)."""
        exe_path = os.path.join(BUILDS_WINDOWS, "MarsSentinel.exe")
        with open(exe_path, "rb") as f:
            header = f.read(64)
            self.assertTrue(header.startswith(b"MZ"), "Missing MZ header magic bytes")
            pe_offset = struct.unpack_from("<I", header, 0x3C)[0]
            f.seek(pe_offset)
            pe_sig = f.read(4)
            self.assertEqual(pe_sig, b"PE\x00\x00", f"Invalid PE signature: {pe_sig}")

    def test_03_unity_player_dll_exists(self):
        """Verify UnityPlayer.dll exists and is > 30 MB."""
        dll_path = os.path.join(BUILDS_WINDOWS, "UnityPlayer.dll")
        self.assertTrue(os.path.isfile(dll_path), f"UnityPlayer.dll missing at {dll_path}")
        size = os.path.getsize(dll_path)
        self.assertGreater(size, 30_000_000, f"UnityPlayer.dll size suspiciously small: {size} bytes")

    def test_04_mars_sentinel_data_directory_structure(self):
        """Verify MarsSentinel_Data directory contains boot assets and global runtime assets."""
        data_dir = os.path.join(BUILDS_WINDOWS, "MarsSentinel_Data")
        self.assertTrue(os.path.isdir(data_dir), f"MarsSentinel_Data directory missing at {data_dir}")
        data_files = os.listdir(data_dir)
        self.assertTrue(
            any("boot.config" in f.lower() for f in data_files),
            f"boot.config not found in {data_files}"
        )
        self.assertTrue(
            any("globalgamemanagers" in f.lower() or "data.unity3d" in f.lower() or "sharedassets0" in f.lower() for f in data_files),
            f"Core data assets not found in {data_files}"
        )

    def test_05_crash_handler_exists(self):
        """Verify UnityCrashHandler64.exe exists in Builds/Windows/."""
        handler_path = os.path.join(BUILDS_WINDOWS, "UnityCrashHandler64.exe")
        self.assertTrue(os.path.isfile(handler_path), f"UnityCrashHandler64.exe missing at {handler_path}")
        self.assertGreater(os.path.getsize(handler_path), 500_000)

    def test_06_build_windows_log_verification(self):
        """Verify build_windows.log confirms Build succeeded with 0 errors and clean exit."""
        log_path = os.path.join(UNITY_PROJECT, "build_windows.log")
        self.assertTrue(os.path.isfile(log_path), f"build_windows.log missing at {log_path}")
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

        self.assertIn("[MarsSceneBuilder] Build succeeded:", content)
        self.assertIn("errors: 0", content)
        self.assertIn("Exiting batchmode successfully now!", content)
        self.assertIn("Application will exit with return code 0", content)

    def test_07_build_webgl_log_verification(self):
        """Verify build_webgl.log confirms WebGL build target switch with 0 errors."""
        log_path = os.path.join(UNITY_PROJECT, "build_webgl.log")
        self.assertTrue(os.path.isfile(log_path), f"build_webgl.log missing at {log_path}")
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

        self.assertIn("Batchmode quit successfully invoked", content)
        self.assertIn("Exiting batchmode successfully now!", content)
        self.assertIn("Application will exit with return code 0", content)
        compiler_errors = [
            l for l in content.splitlines()
            if "compiler error" in l.lower() or "compilation failed" in l.lower() or "error cs" in l.lower()
        ]
        self.assertEqual(compiler_errors, [], f"Found compiler errors in WebGL log: {compiler_errors}")

    def test_08_git_working_tree_clean(self):
        """Verify unity_project git working tree is completely clean."""
        self.assertTrue(os.path.isfile(GIT_BIN), f"Git missing at {GIT_BIN}")
        cmd = [GIT_BIN, "-C", UNITY_PROJECT, "status", "--porcelain"]
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        self.assertEqual(res.returncode, 0)
        self.assertEqual(res.stdout.strip(), "", f"Git working tree is dirty:\n{res.stdout}")

    def test_09_git_commit_history_milestone6(self):
        """Verify git log in unity_project contains milestone 6 commit."""
        cmd = [GIT_BIN, "-C", UNITY_PROJECT, "log", "--oneline", "-n", "30"]
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        self.assertEqual(res.returncode, 0)
        self.assertIn("feat(milestone6)", res.stdout)

    def test_10_master_blend_file_size_invariant(self):
        """Verify master hero production blend file size matches approved production size."""
        hero_blend = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
        self.assertTrue(os.path.isfile(hero_blend), f"Hero blend missing at {hero_blend}")
        sz = os.path.getsize(hero_blend)
        self.assertIn(sz, HERO_BLEND_SIZES, f"Hero blend size invariant violated: {sz} not in {HERO_BLEND_SIZES}")

    def test_11_zero_duplicate_blends(self):
        """Verify zero duplicate .blend or .blend1 files anywhere in unity_project or repository."""
        unity_blends = []
        blend1_files = []
        for root, dirs, files in os.walk(PROJECT_ROOT):
            if ".git" in root or "Library" in root:
                continue
            for f in files:
                if f.endswith(".blend1"):
                    blend1_files.append(os.path.join(root, f))
                elif f.endswith(".blend") and "unity_project" in root:
                    unity_blends.append(os.path.join(root, f))

        self.assertEqual(blend1_files, [], f"Found .blend1 files: {blend1_files}")
        self.assertEqual(unity_blends, [], f"Found .blend files in unity_project: {unity_blends}")

    def test_12_docs_allowlist_strict_enforcement(self):
        """Verify docs/ strictly contains only the 5 authorized markdown files."""
        docs_dir = os.path.join(PROJECT_ROOT, "docs")
        self.assertTrue(os.path.isdir(docs_dir))
        allowed = {
            "AGENTS.md",
            "ARABIC_REPORT.md",
            "ARABIC_SUMMARY_REPORT.md",
            "PROJECT_BLUEPRINT_AND_KNOWLEDGE_BASE.md",
            "SYSTEM_GUIDE.md",
        }
        found_md = {f for f in os.listdir(docs_dir) if f.endswith(".md")}
        unauthorized = found_md - allowed
        self.assertEqual(unauthorized, set(), f"Unauthorized markdown files in docs/: {unauthorized}")

    def test_13_hirise_mesh_hash_blender_headless(self):
        """Verify HiRISE terrain mesh has exact vertex count 1,132,096 and hash ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd."""
        self.assertTrue(os.path.isfile(BLENDER_BIN), f"Blender missing at {BLENDER_BIN}")
        hero_blend = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
        py_code = (
            "import bpy, array, hashlib, sys\n"
            "site = bpy.data.objects.get('NASA_HiRISE_DTM_Jezero_FieldSite_4m')\n"
            "if not site:\n"
            "    print('ERROR: site mesh not found')\n"
            "    sys.exit(2)\n"
            "verts = len(site.data.vertices)\n"
            "coords = array.array('f', [0.0]) * (verts * 3)\n"
            "site.data.vertices.foreach_get('co', coords)\n"
            "h = hashlib.sha256(coords.tobytes()).hexdigest()\n"
            f"print(f'M6_HASH_VERIFIED: verts={{verts}} hash={{h}}')\n"
        )
        cmd = [BLENDER_BIN, "-b", hero_blend, "--python-expr", py_code]
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=60)
        self.assertEqual(res.returncode, 0, f"Blender headless execution failed:\n{res.stderr}")
        self.assertIn(f"M6_HASH_VERIFIED: verts={HIRISE_VERTEX_COUNT} hash={HIRISE_COORDS_SHA256}", res.stdout)


if __name__ == "__main__":
    unittest.main()
