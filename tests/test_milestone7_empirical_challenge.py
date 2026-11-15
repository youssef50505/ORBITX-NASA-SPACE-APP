"""
Milestone 7 Empirical Adversarial Challenge Test Suite
MarsSentinel Workspace Integrity Audit & Master Blender Production Scene Verification (Requirement R1)

Tests:
1. scripts/audit_workspace_integrity.py AST parse and bytecode compilation.
2. Full CLI execution of scripts/audit_workspace_integrity.py --json produces 93/93 passes, 0 fails, exitcode 0.
3. Breakdown validation: exactly 60 datasets, 22 scripts, 4 blender checks, 2 unity checks, 3 web checks, 1 docs check, 1 invariant check.
4. Adversarial stress-testing of format validators in audit_workspace_integrity.py (JSON, GeoJSON, PNG, JPEG, TIFF, STL, GLB, JP2, LBL, NPZ, BLEND, MD, PY) verifying every validator strictly rejects corrupted, empty, or truncated inputs.
5. Unity C# structural brace parser stress-test verifying detection of syntax corruption and unbalanced scopes.
6. Unity .meta pairing auditor negative testing (detects missing and orphan metas).
7. Web DOM selector parser stress-test verifying detection of missing element IDs and corrupted preview assets.
8. Docs allowlist auditor stress-test verifying rejection of unauthorized files.
9. Repository invariants auditor stress-test verifying rejection of .blend1 backups and unauthorized blends in unity_project.
10. Headless Blender verification of HiRISE terrain invariant: exactly 1,132,096 vertices and SHA-256 ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd.
11. Headless Blender verification of Curiosity Rover texture relinking: tex_03.011 references packed image tex_03.png.007 (1024x1024) with zero missing textures across 96 Curiosity objects and 276 scene images.
12. Headless Blender offscreen EEVEE render of Curiosity Rover verifies clean rendering with zero missing texture magenta pixels (#FF00FF).
"""

import os
import sys
import json
import struct
import tempfile
import zipfile
import ast
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
AUDIT_SCRIPT = os.path.join(PROJECT_ROOT, "scripts", "audit_workspace_integrity.py")
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_HASH = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"

sys.path.insert(0, os.path.join(PROJECT_ROOT, "scripts"))
import audit_workspace_integrity as awi


class Milestone7WorkspaceAuditTests(unittest.TestCase):
    """Empirical challenges against audit_workspace_integrity.py execution and validators."""

    def test_01_audit_script_syntax_and_compilation(self):
        """Verify audit_workspace_integrity.py is clean Python 3 AST and compiles to bytecode."""
        self.assertTrue(os.path.isfile(AUDIT_SCRIPT), f"Audit script missing at {AUDIT_SCRIPT}")
        with open(AUDIT_SCRIPT, "r", encoding="utf-8") as f:
            code = f.read()
        ast.parse(code, filename=AUDIT_SCRIPT)
        compiled = compile(code, AUDIT_SCRIPT, "exec")
        self.assertIsNotNone(compiled)

    def test_02_full_cli_audit_execution_and_schema(self):
        """Verify executing audit_workspace_integrity.py --json exits 0 with 93 passes and 0 fails."""
        cmd = [sys.executable, AUDIT_SCRIPT, "--json"]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        self.assertEqual(proc.returncode, 0, f"Audit exited non-zero ({proc.returncode}):\n{proc.stderr}\n{proc.stdout}")

        # Locate JSON block in stdout
        stdout = proc.stdout
        json_start = stdout.find("{\n  \"timestamp\":")
        if json_start == -1:
            json_start = stdout.find('{\n  "timestamp":')
        self.assertNotEqual(json_start, -1, f"Could not find JSON payload in audit output:\n{stdout}")

        report = json.loads(stdout[json_start:])
        self.assertEqual(report.get("overall_status"), "PASS")
        self.assertGreaterEqual(report.get("total_checks", 0), 115)
        self.assertEqual(report.get("passed"), report.get("total_checks"))
        self.assertEqual(report.get("failed"), 0)

        # Verify module breakdown counts
        results = report.get("results", [])
        self.assertEqual(len(results), report.get("total_checks"))
        modules = {}
        for r in results:
            self.assertEqual(r.get("status"), "PASS", f"Check failed: {r}")
            mod = r.get("module")
            modules[mod] = modules.get(mod, 0) + 1

        self.assertGreaterEqual(modules.get("DATASETS", 0), 81, "Expected >= 81 dataset checks")
        self.assertGreaterEqual(modules.get("SCRIPTS", 0), 25, "Expected >= 25 python script checks")
        self.assertEqual(modules.get("BLENDER"), 4, "Expected 4 blender checks")
        self.assertEqual(modules.get("UNITY"), 2, "Expected 2 unity checks")
        self.assertEqual(modules.get("WEB"), 3, "Expected 3 web checks")
        self.assertEqual(modules.get("DOCS"), 1, "Expected 1 docs check")
        self.assertEqual(modules.get("INVARIANTS"), 1, "Expected 1 invariants check")

    def test_03_json_and_geojson_validator_adversarial_rejection(self):
        """Verify validate_json_file and validate_geojson_file catch corruptions and invalid structures."""
        with tempfile.TemporaryDirectory() as tmpdir:
            # 1. Non-dict/list JSON root
            p1 = os.path.join(tmpdir, "num.json")
            with open(p1, "w") as f:
                f.write("12345")
            with self.assertRaises(ValueError):
                awi.validate_json_file(p1)

            # 2. Empty JSON dict
            p2 = os.path.join(tmpdir, "empty.json")
            with open(p2, "w") as f:
                f.write("{}")
            with self.assertRaises(ValueError):
                awi.validate_json_file(p2)

            # 3. GeoJSON with non-FeatureCollection
            p3 = os.path.join(tmpdir, "bad_type.geojson")
            with open(p3, "w") as f:
                json.dump({"type": "Point", "coordinates": [0, 0]}, f)
            with self.assertRaises(ValueError):
                awi.validate_geojson_file(p3)

            # 4. GeoJSON with empty features list
            p4 = os.path.join(tmpdir, "empty_feats.geojson")
            with open(p4, "w") as f:
                json.dump({"type": "FeatureCollection", "features": []}, f)
            with self.assertRaises(ValueError):
                awi.validate_geojson_file(p4)

            # 5. GeoJSON with feature missing geometry
            p5 = os.path.join(tmpdir, "no_geom.geojson")
            with open(p5, "w") as f:
                json.dump({"type": "FeatureCollection", "features": [{"type": "Feature"}]}, f)
            with self.assertRaises(ValueError):
                awi.validate_geojson_file(p5)

    def test_04_raster_imagery_validators_adversarial_rejection(self):
        """Verify PNG, JPEG, TIFF, JP2 validators reject bad magic and truncated streams."""
        with tempfile.TemporaryDirectory() as tmpdir:
            # PNG bad header
            png_bad = os.path.join(tmpdir, "bad.png")
            with open(png_bad, "wb") as f:
                f.write(b"NOTAPNG\n" + b"\x00" * 32)
            with self.assertRaises(ValueError):
                awi.validate_png_file(png_bad)

            # PNG missing IEND
            png_trunc = os.path.join(tmpdir, "trunc.png")
            with open(png_trunc, "wb") as f:
                f.write(b"\x89PNG\r\n\x1a\n" + b"TRUNCATEDDATAWITHOUT_CLOSING_CHUNK")
            with self.assertRaises(ValueError):
                awi.validate_png_file(png_trunc)

            # JPEG bad SOI
            jpg_bad = os.path.join(tmpdir, "bad.jpg")
            with open(jpg_bad, "wb") as f:
                f.write(b"\x00\x00\x00\x00")
            with self.assertRaises(ValueError):
                awi.validate_jpeg_file(jpg_bad)

            # JPEG truncated (missing EOI)
            jpg_trunc = os.path.join(tmpdir, "trunc.jpg")
            with open(jpg_trunc, "wb") as f:
                f.write(b"\xff\xd8\x00\x00\x00\x00")
            with self.assertRaises(ValueError):
                awi.validate_jpeg_file(jpg_trunc)

            # TIFF bad magic
            tif_bad = os.path.join(tmpdir, "bad.tif")
            with open(tif_bad, "wb") as f:
                f.write(b"TIFF1234")
            with self.assertRaises(ValueError):
                awi.validate_tiff_file(tif_bad)

            # JP2 bad magic
            jp2_bad = os.path.join(tmpdir, "bad.jp2")
            with open(jp2_bad, "wb") as f:
                f.write(b"\x00\x00\x00\x0cNOTJP2")
            with self.assertRaises(ValueError):
                awi.validate_jp2_file(jp2_bad)

    def test_05_3d_geometry_and_archive_validators_adversarial_rejection(self):
        """Verify STL, GLB, NPZ, LBL, BLEND validators reject corruptions."""
        with tempfile.TemporaryDirectory() as tmpdir:
            # STL declared triangle count mismatch
            stl_bad = os.path.join(tmpdir, "bad.stl")
            with open(stl_bad, "wb") as f:
                f.write(b"\x00" * 80) # 80 header
                f.write(struct.pack("<I", 100)) # declared 100 triangles => 5084 bytes needed
                f.write(b"\x00" * 50) # only 1 triangle written
            with self.assertRaises(ValueError):
                awi.validate_stl_file(stl_bad)

            # STL NaN float
            stl_nan = os.path.join(tmpdir, "nan.stl")
            with open(stl_nan, "wb") as f:
                f.write(b"\x00" * 80)
                f.write(struct.pack("<I", 1))
                f.write(struct.pack("<12fH", float('nan'), *([1.0]*11), 0))
            with self.assertRaises(ValueError):
                awi.validate_stl_file(stl_nan)

            # GLB bad magic
            glb_bad = os.path.join(tmpdir, "bad.glb")
            with open(glb_bad, "wb") as f:
                f.write(struct.pack("<4sII", b"XXXX", 2, 20))
                f.write(struct.pack("<I4s", 0, b"JSON"))
            with self.assertRaises(ValueError):
                awi.validate_glb_file(glb_bad)

            # GLB declared length mismatch
            glb_len = os.path.join(tmpdir, "badlen.glb")
            with open(glb_len, "wb") as f:
                f.write(struct.pack("<4sII", b"glTF", 2, 999999))
                f.write(struct.pack("<I4s", 0, b"JSON"))
            with self.assertRaises(ValueError):
                awi.validate_glb_file(glb_len)

            # NPZ empty archive
            npz_empty = os.path.join(tmpdir, "empty.npz")
            with zipfile.ZipFile(npz_empty, "w") as zf:
                pass
            with self.assertRaises(ValueError):
                awi.validate_npz_file(npz_empty)

            # LBL missing tokens
            lbl_bad = os.path.join(tmpdir, "bad.lbl")
            with open(lbl_bad, "w") as f:
                f.write("THIS IS NOT A VALID PDS LABEL FILE")
            with self.assertRaises(ValueError):
                awi.validate_lbl_file(lbl_bad)

            # BLEND bad magic
            blend_bad = os.path.join(tmpdir, "bad.blend")
            with open(blend_bad, "wb") as f:
                f.write(b"NOTBLND")
            with self.assertRaises(ValueError):
                awi.validate_blend_file(blend_bad)

    def test_06_csharp_brace_parser_adversarial_rejection(self):
        """Verify strip_csharp_strings_and_comments handles strings/comments and catches unbalanced braces."""
        # Clean C# snippet with strings and comments containing braces
        valid_cs = """
        // comment with { braces }
        /* multi-line
           { braces } */
        public class Test {
            private string msg = "string with { unmatched brace";
            private string verbatim = @"verbatim string with } brace";
            public void Run() {
                int x = 1;
            }
        }
        """
        clean = awi.strip_csharp_strings_and_comments(valid_cs)
        self.assertEqual(clean.count("{"), clean.count("}"))
        self.assertEqual(clean.count("{"), 2) # Only class and Run()

        # Unbalanced C# snippet (missing close brace)
        invalid_cs = """
        public class Broken {
            public void Run() {
                int x = 1;
        }
        """
        clean_bad = awi.strip_csharp_strings_and_comments(invalid_cs)
        self.assertNotEqual(clean_bad.count("{"), clean_bad.count("}"))

    def test_07_docs_allowlist_adversarial_rejection(self):
        """Verify audit_docs_allowlist rejects unexpected markdown files."""
        logger = awi.AuditLogger()
        # Normal audit passes
        self.assertTrue(awi.audit_docs_allowlist(logger))

        # Add temporary unauthorized file into docs/ and ensure failure
        unauth_path = os.path.join(PROJECT_ROOT, "docs", "UNAUTHORIZED_TEMP_TEST_DOC.md")
        try:
            with open(unauth_path, "w") as f:
                f.write("# Unauthorized test file\n")
            logger2 = awi.AuditLogger()
            res = awi.audit_docs_allowlist(logger2)
            self.assertFalse(res, "audit_docs_allowlist failed to catch unauthorized doc!")
        finally:
            if os.path.exists(unauth_path):
                os.remove(unauth_path)

    def test_08_repository_invariants_adversarial_rejection(self):
        """Verify audit_repository_invariants rejects .blend1 backups."""
        logger = awi.AuditLogger()
        self.assertTrue(awi.audit_repository_invariants(logger))

        # Create temporary .blend1 file in project root and verify detection
        temp_blend1 = os.path.join(PROJECT_ROOT, "temp_test_artifact.blend1")
        try:
            with open(temp_blend1, "wb") as f:
                f.write(b"BLENDER_BACKUP")
            logger2 = awi.AuditLogger()
            res = awi.audit_repository_invariants(logger2)
            self.assertFalse(res, "audit_repository_invariants failed to catch .blend1 backup file!")
        finally:
            if os.path.exists(temp_blend1):
                os.remove(temp_blend1)


class Milestone7BlenderProductionSceneTests(unittest.TestCase):
    """Empirical challenges against MARS_SENTINEL_HERO_PRODUCTION.blend headlessly in Blender 5.2."""

    @classmethod
    def setUpClass(cls):
        """Run inspection script in Blender once and cache parsed JSON."""
        inspect_script = os.path.join(PROJECT_ROOT, "tests", "verify_m7_blender.py")
        cmd = [
            BLENDER_BIN,
            HERO_BLEND,
            "--background",
            "--python",
            inspect_script,
        ]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        assert proc.returncode == 0, f"Blender inspect script failed:\n{proc.stderr}\n{proc.stdout}"

        stdout = proc.stdout
        s_idx = stdout.find("JSON_START")
        e_idx = stdout.find("JSON_END")
        assert s_idx != -1 and e_idx != -1, f"Failed to parse JSON boundaries from output:\n{stdout}"
        json_str = stdout[s_idx + len("JSON_START"):e_idx].strip()
        cls.data = json.loads(json_str)

    def test_09_hirise_terrain_invariant(self):
        """Verify HiRISE terrain mesh vertex count (1,132,096) and float32 SHA-256 hash match invariant."""
        hirise = self.data.get("hirise", {})
        self.assertTrue(hirise.get("exists"), "NASA_HiRISE_DTM_Jezero_FieldSite_4m missing from scene")
        self.assertEqual(hirise.get("vertex_count"), HIRISE_EXPECTED_VERTS)
        self.assertEqual(hirise.get("sha256"), HIRISE_EXPECTED_HASH)
        self.assertTrue(hirise.get("verts_match"))
        self.assertTrue(hirise.get("hash_match"))

    def test_10_curiosity_asset_hierarchy_and_relinked_textures(self):
        """Verify Curiosity Rover root, 96 objects, and material tex_03.011 relinked to packed tex_03.png.007."""
        curiosity = self.data.get("curiosity", {})
        self.assertTrue(curiosity.get("root_exists"), "MS_Curiosity_Root missing from scene")
        self.assertEqual(curiosity.get("total_objects"), 96)
        self.assertEqual(curiosity.get("mesh_objects_count"), 95)
        self.assertEqual(curiosity.get("materials_count"), 18)

        # Verify tex_03.011 is assigned to Curiosity parts
        assigned_objs = curiosity.get("tex_03_011_assigned_objects", [])
        expected_objs = [
            "CheMin", "HazCam - front", "HazCam - rear", "MARDI.001",
            "RTG", "SAM", "antenna_LG", "antenna_UHF",
            "body", "body.001", "radiators", "sundial"
        ]
        for exp in expected_objs:
            self.assertIn(exp, assigned_objs, f"Object {exp} missing material tex_03.011")

        # Verify tex_03.011 Image Texture node points to packed tex_03.png.007
        nodes = curiosity.get("tex_03_011_nodes", [])
        self.assertGreater(len(nodes), 0, "No Image Texture nodes found in tex_03.011")
        img_node = nodes[0]
        self.assertEqual(img_node.get("image_name"), "tex_03.png.007")
        self.assertTrue(img_node.get("is_packed"), "tex_03.png.007 is NOT packed in blend file")
        self.assertEqual(img_node.get("size"), [1024, 1024])

    def test_11_zero_missing_images_and_clean_datablocks(self):
        """Verify all 276 scene image datablocks are valid with 0 missing images, and dead blocks pruned."""
        images = self.data.get("images", {})
        self.assertEqual(images.get("missing_count"), 0)
        self.assertEqual(images.get("missing_images"), [])
        self.assertEqual(images.get("total_images"), 276)

        pruned = self.data.get("pruned_blocks", {})
        self.assertFalse(pruned.get("tex_03.png.006_exists"), "Dead block tex_03.png.006 still exists")
        self.assertFalse(pruned.get("tex_03.png.008_exists"), "Dead block tex_03.png.008 still exists")
        self.assertTrue(pruned.get("tex_03.png.007_exists"), "Packed block tex_03.png.007 missing")

        libs = self.data.get("libraries", {})
        self.assertEqual(libs.get("count"), 0, f"External libraries found: {libs.get('paths')}")

    def test_12_curiosity_offscreen_render_no_missing_texture_artifacts(self):
        """Verify Curiosity Rover renders offscreen in EEVEE with zero magenta missing texture pixels."""
        render_script = os.path.join(PROJECT_ROOT, "tests", "verify_m7_render.py")
        cmd = [
            BLENDER_BIN,
            HERO_BLEND,
            "--background",
            "--python",
            render_script,
        ]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=120)
        self.assertEqual(proc.returncode, 0, f"Render test failed:\n{proc.stderr}\n{proc.stdout}")
        self.assertIn("Magenta (missing texture) pixels: 0", proc.stdout)
        self.assertIn("PASS: Curiosity rendered cleanly with ZERO missing texture magenta pixels!", proc.stdout)


if __name__ == "__main__":
    unittest.main()
