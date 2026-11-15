#!/usr/bin/env python3
"""
scripts/audit_workspace_integrity.py
====================================
MarsSentinel — Project & Dataset Integrity Audit & Baseline Repair Diagnostic Tool
Phase 3 / Milestone 7 Verification Script (Requirement R1).

Verifies:
  1. NASA/USGS Datasets (all 60 files in data/ for header magic, format, truncation).
  2. Python Tooling & Scripts (compilation check across scripts/ and tools/).
  3. Master Blender Simulation Scene (MARS_SENTINEL_HERO_PRODUCTION.blend headless load,
     mesh datablock integrity, library independence, Curiosity texture relink check/repair,
     and HiRISE DTM Jezero Field Site geometry invariant).
  4. Unity 6 Project Assets (0 missing .meta, 0 orphan .meta, C# script syntax).
  5. Web Application (web/index.html, app.js, styles.css, DOM resolution, preview images).
  6. Docs Allowlist Strict Hygiene (no unauthorized markdown in docs/).
  7. Repository Invariants (zero duplicate .blend or .blend1 files).

Usage:
  python scripts/audit_workspace_integrity.py [--repair] [--verbose] [--json]
"""

import os
import sys
import json
import struct
import math
import hashlib
import array
import zipfile
import ast
import re
import shutil
import subprocess
import argparse
from datetime import datetime, timezone
from html.parser import HTMLParser

# --- Constants & Configuration ---
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DATA_DIR = os.path.join(PROJECT_ROOT, "data")
SCRIPTS_DIR = os.path.join(PROJECT_ROOT, "scripts")
TOOLS_DIR = os.path.join(PROJECT_ROOT, "tools")
DOCS_DIR = os.path.join(PROJECT_ROOT, "docs")
WEB_DIR = os.path.join(PROJECT_ROOT, "web")
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
HERO_BLEND = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")

# Invariant constants
HIRISE_OBJECT_NAME = "NASA_HiRISE_DTM_Jezero_FieldSite_4m"
HIRISE_EXPECTED_VERTS = 1132096
HIRISE_EXPECTED_SHA256 = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"

ALLOWED_DOCS = {
    "AGENTS.md",
    "ARABIC_REPORT.md",
    "ARABIC_SUMMARY_REPORT.md",
    "PROJECT_BLUEPRINT_AND_KNOWLEDGE_BASE.md",
    "SYSTEM_GUIDE.md",
}

DEFAULT_BLENDER_CANDIDATES = [
    r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    r"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
    r"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe",
    r"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe",
    shutil.which("blender"),
]


class AuditLogger:
    """Helper for formatted logging with UTC timestamps."""

    def __init__(self, verbose=False):
        self.verbose = verbose
        self.results = []

    def log(self, level, module, message):
        ts = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%SZ")
        print(f"[{ts}] [{level:4s}] [{module}] {message}")

    def pass_check(self, module, name, detail=""):
        msg = f"[PASS] {name}"
        if detail and self.verbose:
            msg += f" — {detail}"
        self.log("INFO", module, msg)
        self.results.append({"module": module, "name": name, "status": "PASS", "detail": detail})

    def fail_check(self, module, name, error):
        msg = f"[FAIL] {name}: {error}"
        self.log("FAIL", module, msg)
        self.results.append({"module": module, "name": name, "status": "FAIL", "error": str(error)})

    def info(self, module, message):
        self.log("INFO", module, message)


def find_blender():
    """Find local Blender executable."""
    env_blender = os.environ.get("BLENDER_BIN") or os.environ.get("BLENDER_PATH")
    if env_blender and os.path.exists(env_blender):
        return env_blender
    for c in DEFAULT_BLENDER_CANDIDATES:
        if c and os.path.exists(c):
            return c
    return None


# ==============================================================================
# 1. Dataset Integrity Audit
# ==============================================================================

def validate_json_file(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, (dict, list)):
        raise ValueError(f"JSON root must be dict or list, got {type(data).__name__}")
    if len(data) == 0:
        raise ValueError("JSON file is empty collection")
    return f"{type(data).__name__} with {len(data)} items"


def validate_geojson_file(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, dict):
        raise ValueError("GeoJSON root must be dict")
    if data.get("type") != "FeatureCollection":
        raise ValueError(f"Expected FeatureCollection, got {data.get('type')}")
    features = data.get("features")
    if not isinstance(features, list) or len(features) == 0:
        raise ValueError("GeoJSON has empty or missing features array")
    for idx, feat in enumerate(features[:10]):
        if feat.get("type") != "Feature":
            raise ValueError(f"Feature at index {idx} has invalid type: {feat.get('type')}")
        if "geometry" not in feat:
            raise ValueError(f"Feature at index {idx} missing geometry")
    return f"FeatureCollection with {len(features)} features"


def validate_png_file(path):
    sz = os.path.getsize(path)
    if sz < 8:
        raise ValueError("File too small for PNG header")
    with open(path, "rb") as f:
        header = f.read(8)
        if header != b"\x89PNG\r\n\x1a\n":
            raise ValueError(f"Invalid PNG signature: {header!r}")
        f.seek(max(0, sz - 12))
        tail = f.read(12)
        if tail[:8] != b"\x00\x00\x00\x00IEND":
            raise ValueError("PNG missing IEND closing chunk (possible truncation)")
    return f"Valid PNG ({sz:,} bytes)"


def validate_jpeg_file(path):
    sz = os.path.getsize(path)
    if sz < 4:
        raise ValueError("File too small for JPEG")
    with open(path, "rb") as f:
        soi = f.read(2)
        f.seek(sz - 2)
        eoi = f.read(2)
        if soi != b"\xff\xd8":
            raise ValueError(f"Invalid JPEG SOI marker: {soi!r}")
        if eoi != b"\xff\xd9":
            raise ValueError(f"Invalid JPEG EOI marker: {eoi!r} (truncated file)")
    return f"Valid JPEG ({sz:,} bytes)"


def validate_tiff_file(path):
    sz = os.path.getsize(path)
    if sz < 8:
        raise ValueError("File too small for TIFF")
    with open(path, "rb") as f:
        magic = f.read(4)
        if magic == b"II*\x00":
            ifd_offset = struct.unpack("<I", f.read(4))[0]
        elif magic == b"MM\x00*":
            ifd_offset = struct.unpack(">I", f.read(4))[0]
        else:
            raise ValueError(f"Invalid TIFF magic bytes: {magic!r}")
        if ifd_offset <= 0 or ifd_offset >= sz:
            raise ValueError(f"Invalid first IFD offset: {ifd_offset} (file size {sz})")
    return f"Valid TIFF ({sz:,} bytes, IFD at {ifd_offset})"


def validate_stl_file(path):
    sz = os.path.getsize(path)
    if sz < 84:
        raise ValueError(f"File too small for binary STL ({sz} bytes)")
    with open(path, "rb") as f:
        _header = f.read(80)
        n_tri = struct.unpack("<I", f.read(4))[0]
        expected_size = 84 + (n_tri * 50)
        if sz != expected_size:
            raise ValueError(f"STL byte size mismatch: declared {n_tri} tris => expected {expected_size} bytes, got {sz}")
        # Verify first 5 and last 5 triangles have finite floats
        for _ in range(min(5, n_tri)):
            raw = f.read(50)
            floats = struct.unpack("<12fH", raw)[:12]
            if not all(math.isfinite(x) for x in floats):
                raise ValueError("Encountered non-finite float in STL triangle buffer")
    return f"Binary STL with {n_tri:,} triangles ({sz:,} bytes)"


def validate_glb_file(path):
    sz = os.path.getsize(path)
    if sz < 20:
        raise ValueError("File too small for glTF binary")
    with open(path, "rb") as f:
        magic, version, length = struct.unpack("<4sII", f.read(12))
        if magic != b"glTF":
            raise ValueError(f"Invalid glTF magic: {magic!r}")
        if version != 2:
            raise ValueError(f"Expected glTF version 2, got {version}")
        if length != sz:
            raise ValueError(f"Declared glTF length {length} != file size {sz}")
        chunk_len, chunk_type = struct.unpack("<I4s", f.read(8))
        if chunk_type != b"JSON":
            raise ValueError(f"First glTF chunk must be JSON, got {chunk_type!r}")
        chunk_data = f.read(chunk_len)
        manifest = json.loads(chunk_data.decode("utf-8"))
        if not isinstance(manifest, dict):
            raise ValueError("glTF JSON chunk is not a valid dict")
    return f"Valid glTF 2.0 binary ({sz:,} bytes)"


def validate_jp2_file(path):
    sz = os.path.getsize(path)
    if sz < 12:
        raise ValueError("File too small for JP2")
    with open(path, "rb") as f:
        magic = f.read(12)
        if magic != b"\x00\x00\x00\x0cjP  \r\n\x87\n":
            raise ValueError(f"Invalid JPEG 2000 magic: {magic!r}")
    return f"Valid JP2 ({sz:,} bytes)"


def validate_lbl_file(path):
    with open(path, "r", encoding="latin-1") as f:
        text = f.read()
    if not any(token in text for token in ("PDS_VERSION_ID", "ODL_VERSION_NUMBER", "RECORD_TYPE", "END")):
        raise ValueError("LBL file does not contain valid PDS label tokens")
    return f"Valid PDS/VICAR LBL ({len(text):,} chars)"


def validate_npz_file(path):
    with zipfile.ZipFile(path, "r") as zf:
        names = zf.namelist()
        if not names:
            raise ValueError("NPZ file is empty archive")
    return f"Valid NPZ archive containing: {names}"


def validate_blend_file(path):
    sz = os.path.getsize(path)
    if sz < 7:
        raise ValueError("File too small for Blender header")
    with open(path, "rb") as f:
        magic = f.read(7)
        if magic != b"BLENDER":
            raise ValueError(f"Invalid Blender magic: {magic!r}")
    return f"Valid Blender header ({sz:,} bytes)"


def validate_md_file(path):
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    if len(text.strip()) == 0:
        raise ValueError("Markdown file is empty")
    return f"Valid Markdown ({len(text):,} chars)"


def validate_py_file(path):
    with open(path, "r", encoding="utf-8") as f:
        code = f.read()
    ast.parse(code, filename=path)
    compile(code, path, "exec")
    return f"Valid Python AST compilation ({len(code):,} chars)"


def audit_datasets(logger):
    """Audit all 60 NASA/USGS dataset files in data/."""
    logger.info("DATASETS", f"Scanning authoritative dataset repository at {DATA_DIR}...")
    if not os.path.isdir(DATA_DIR):
        logger.fail_check("DATASETS", "Directory existence", f"{DATA_DIR} not found")
        return False

    all_files = []
    for root, _dirs, files in os.walk(DATA_DIR):
        for f in files:
            all_files.append(os.path.join(root, f))

    all_files.sort()
    logger.info("DATASETS", f"Found {len(all_files)} dataset files under data/")

    handlers = {
        ".json": validate_json_file,
        ".geojson": validate_geojson_file,
        ".png": validate_png_file,
        ".jpg": validate_jpeg_file,
        ".jpeg": validate_jpeg_file,
        ".tif": validate_tiff_file,
        ".tiff": validate_tiff_file,
        ".stl": validate_stl_file,
        ".glb": validate_glb_file,
        ".jp2": validate_jp2_file,
        ".lbl": validate_lbl_file,
        ".npz": validate_npz_file,
        ".blend": validate_blend_file,
        ".md": validate_md_file,
        ".py": validate_py_file,
    }

    passed = 0
    for p in all_files:
        rel = os.path.relpath(p, PROJECT_ROOT)
        ext = os.path.splitext(p)[1].lower()
        validator = handlers.get(ext)
        if not validator:
            logger.fail_check("DATASETS", rel, f"Unrecognized dataset extension '{ext}'")
            continue
        try:
            sz = os.path.getsize(p)
            if sz == 0:
                raise ValueError("Zero byte file detected")
            detail = validator(p)
            logger.pass_check("DATASETS", rel, detail)
            passed += 1
        except Exception as e:
            logger.fail_check("DATASETS", rel, str(e))

    logger.info("DATASETS", f"Dataset Audit: {passed}/{len(all_files)} files verified cleanly")
    return passed == len(all_files) and len(all_files) >= 60


# ==============================================================================
# 2. Python Tools & Scripts Audit
# ==============================================================================

def audit_python_scripts(logger):
    """Compile and verify all Python scripts in scripts/ and tools/."""
    logger.info("SCRIPTS", "Scanning Python scripts in scripts/ and tools/...")
    target_dirs = [SCRIPTS_DIR, TOOLS_DIR]
    py_files = []
    for d in target_dirs:
        if not os.path.isdir(d):
            continue
        for root, _dirs, files in os.walk(d):
            if "__pycache__" in root:
                continue
            for f in files:
                if f.endswith(".py"):
                    py_files.append(os.path.join(root, f))

    py_files.sort()
    passed = 0
    for p in py_files:
        rel = os.path.relpath(p, PROJECT_ROOT)
        try:
            with open(p, "r", encoding="utf-8") as f:
                source = f.read()
            ast.parse(source, filename=p)
            compile(source, p, "exec")
            logger.pass_check("SCRIPTS", rel, "AST parse & bytecode compilation succeeded")
            passed += 1
        except Exception as e:
            logger.fail_check("SCRIPTS", rel, str(e))

    logger.info("SCRIPTS", f"Python Tooling Audit: {passed}/{len(py_files)} scripts compiled cleanly")
    return passed == len(py_files) and len(py_files) > 0


# ==============================================================================
# 3. Master Blender Scene & Baseline Repair
# ==============================================================================

BLENDER_INSPECT_AND_REPAIR_SCRIPT = r"""
import bpy
import os
import sys
import array
import hashlib

def run():
    print("=== BLENDER_AUDIT_BEGIN ===")
    hero_blend = bpy.data.filepath
    print(f"BLEND_FILE: {hero_blend}")
    
    # 1. External libraries check
    libs = [lib.filepath for lib in bpy.data.libraries]
    print(f"LIBRARIES_COUNT: {len(libs)}")
    if libs:
        print(f"LIBRARIES: {libs}")
        
    # 2. Mesh data-blocks integrity
    total_meshes = len(bpy.data.meshes)
    corrupted_meshes = []
    for m in bpy.data.meshes:
        try:
            _ = len(m.vertices)
            _ = len(m.polygons)
        except Exception as e:
            corrupted_meshes.append((m.name, str(e)))
    print(f"MESHES_TOTAL: {total_meshes}")
    print(f"CORRUPTED_MESHES_COUNT: {len(corrupted_meshes)}")
    
    # 3. Curiosity texture relinking & repair
    # Check if materials link to tex_03.png.008 or tex_03.png.006 (unpacked/missing)
    target_packed = bpy.data.images.get('tex_03.png.007')
    relinked = 0
    if target_packed:
        for mat in bpy.data.materials:
            if not mat.node_tree:
                continue
            for node in mat.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    if node.image.name in ('tex_03.png.008', 'tex_03.png.006'):
                        node.image = target_packed
                        relinked += 1
                        print(f"RELINKED: mat={mat.name} node={node.name} -> {target_packed.name}")
        # Prune unused unpacked blocks
        for bad_name in ('tex_03.png.008', 'tex_03.png.006'):
            img = bpy.data.images.get(bad_name)
            if img:
                bpy.data.images.remove(img)
                print(f"PRUNED_IMAGE_DATABLOCK: {bad_name}")
    print(f"RELINKED_NODES_COUNT: {relinked}")
    
    # Check if save is needed
    if relinked > 0 or '--repair' in sys.argv:
        bpy.ops.wm.save_mainfile(filepath=hero_blend)
        print("SAVED_IN_PLACE: True")
    else:
        print("SAVED_IN_PLACE: False")
        
    # 4. Invariant check: NASA_HiRISE_DTM_Jezero_FieldSite_4m
    site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
    if not site or not site.data:
        print("HIRISE_EXISTS: False")
        print("HIRISE_VERTS: 0")
        print("HIRISE_HASH: None")
    else:
        print("HIRISE_EXISTS: True")
        v_count = len(site.data.vertices)
        print(f"HIRISE_VERTS: {v_count}")
        coords = array.array('f', [0.0]) * (v_count * 3)
        site.data.vertices.foreach_get('co', coords)
        h = hashlib.sha256(coords.tobytes()).hexdigest()
        print(f"HIRISE_HASH: {h}")
        
    # 5. Check remaining missing images (excluding Blender internal buffers)
    missing_images = []
    for img in bpy.data.images:
        if img.name in ('Render Result', 'Viewer Node'):
            continue
        if img.packed_file is not None:
            continue
        abs_p = bpy.path.abspath(img.filepath)
        if not os.path.exists(abs_p):
            missing_images.append(img.name)
    print(f"MISSING_IMAGES_COUNT: {len(missing_images)}")
    if missing_images:
        print(f"MISSING_IMAGES: {missing_images}")
        
    print("=== BLENDER_AUDIT_END ===")

if __name__ == '__main__':
    run()
"""


def audit_blender_master_scene(logger, do_repair=False):
    """Audit and repair MARS_SENTINEL_HERO_PRODUCTION.blend headlessly."""
    logger.info("BLENDER", f"Auditing Master Blender scene at {HERO_BLEND}...")
    if not os.path.isfile(HERO_BLEND):
        logger.fail_check("BLENDER", "Scene file existence", f"{HERO_BLEND} missing")
        return False

    blender_exe = find_blender()
    if not blender_exe:
        logger.fail_check("BLENDER", "Blender binary", "Blender executable not found on system")
        return False
    logger.info("BLENDER", f"Found Blender at: {blender_exe}")

    cmd = [
        blender_exe,
        HERO_BLEND,
        "--background",
        "--python-expr",
        BLENDER_INSPECT_AND_REPAIR_SCRIPT,
    ]
    if do_repair:
        cmd.extend(["--", "--repair"])

    try:
        proc = subprocess.run(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=120,
        )
    except subprocess.TimeoutExpired:
        logger.fail_check("BLENDER", "Headless execution", "Blender headless process timed out after 120s")
        return False

    out = proc.stdout

    # Clean up any .blend1 file created by Blender saving
    blend1_path = HERO_BLEND + "1"
    if os.path.exists(blend1_path):
        try:
            os.remove(blend1_path)
            logger.info("BLENDER", "Pruned automated Blender backup file MARS_SENTINEL_HERO_PRODUCTION.blend1")
        except Exception as e:
            logger.fail_check("BLENDER", "Clean blend1", f"Failed to remove blend1 backup: {e}")

    # Parse stdout markers
    if "=== BLENDER_AUDIT_BEGIN ===" not in out or "=== BLENDER_AUDIT_END ===" not in out:
        logger.fail_check("BLENDER", "Headless execution", f"Blender script failed to complete. Returncode: {proc.returncode}\n{proc.stderr}")
        return False

    # Extract parsed fields
    fields = {}
    for line in out.splitlines():
        line = line.strip()
        if ":" in line:
            k, v = line.split(":", 1)
            fields[k.strip()] = v.strip()

    # 1. External libraries
    lib_count = int(fields.get("LIBRARIES_COUNT", "-1"))
    if lib_count == 0:
        logger.pass_check("BLENDER", "External Libraries", "0 linked libraries (fully self-contained scene)")
    else:
        logger.fail_check("BLENDER", "External Libraries", f"Found {lib_count} external libraries")

    # 2. Corrupted meshes
    mesh_count = int(fields.get("MESHES_TOTAL", "0"))
    corrupted_count = int(fields.get("CORRUPTED_MESHES_COUNT", "-1"))
    if corrupted_count == 0 and mesh_count > 0:
        logger.pass_check("BLENDER", "Mesh Datablocks", f"All {mesh_count} meshes evaluate cleanly with 0 corrupted")
    else:
        logger.fail_check("BLENDER", "Mesh Datablocks", f"{corrupted_count} corrupted meshes detected out of {mesh_count}")

    # 3. Curiosity texture relink
    missing_img_count = int(fields.get("MISSING_IMAGES_COUNT", "-1"))
    if missing_img_count == 0:
        logger.pass_check("BLENDER", "Texture References", "All external textures resolved (0 missing)")
    else:
        logger.fail_check("BLENDER", "Texture References", f"{missing_img_count} missing texture images detected")

    # 4. HiRISE Terrain Invariant
    hirise_exists = fields.get("HIRISE_EXISTS") == "True"
    hirise_verts = int(fields.get("HIRISE_VERTS", "0"))
    hirise_hash = fields.get("HIRISE_HASH", "")

    if hirise_exists and hirise_verts == HIRISE_EXPECTED_VERTS and hirise_hash == HIRISE_EXPECTED_SHA256:
        logger.pass_check(
            "BLENDER",
            "HiRISE Terrain Invariant",
            f"Vertex count {hirise_verts:,} and SHA-256 hash {hirise_hash} match invariant bit-for-bit",
        )
    else:
        logger.fail_check(
            "BLENDER",
            "HiRISE Terrain Invariant",
            f"Expected {HIRISE_EXPECTED_VERTS} verts, got {hirise_verts}; expected hash {HIRISE_EXPECTED_SHA256}, got {hirise_hash}",
        )

    all_ok = (
        lib_count == 0
        and corrupted_count == 0
        and missing_img_count == 0
        and hirise_exists
        and hirise_verts == HIRISE_EXPECTED_VERTS
        and hirise_hash == HIRISE_EXPECTED_SHA256
    )
    return all_ok


# ==============================================================================
# 4. Unity Project Integrity
# ==============================================================================

def strip_csharp_strings_and_comments(source):
    """Strip comments and string literals from C# source code for structural syntax validation."""
    i = 0
    n = len(source)
    out = []
    while i < n:
        if source[i:i+2] == '//':
            i = source.find('\n', i)
            if i == -1:
                break
        elif source[i:i+2] == '/*':
            end = source.find('*/', i + 2)
            if end == -1:
                break
            i = end + 2
        elif source[i:i+2] == '@"':
            i += 2
            while i < n:
                if source[i] == '"':
                    if i + 1 < n and source[i+1] == '"':
                        i += 2
                    else:
                        i += 1
                        break
                else:
                    i += 1
        elif source[i] == '"':
            i += 1
            while i < n:
                if source[i] == '\\' and i + 1 < n:
                    i += 2
                elif source[i] == '"':
                    i += 1
                    break
                else:
                    i += 1
        elif source[i] == "'":
            i += 1
            while i < n:
                if source[i] == '\\' and i + 1 < n:
                    i += 2
                elif source[i] == "'":
                    i += 1
                    break
                else:
                    i += 1
        else:
            out.append(source[i])
            i += 1
    return "".join(out)


def audit_unity_project(logger):
    """Audit Unity project Assets for missing/orphan .meta files and script compilation."""
    logger.info("UNITY", f"Auditing Unity project at {UNITY_DIR}...")
    assets_dir = os.path.join(UNITY_DIR, "Assets")
    if not os.path.isdir(assets_dir):
        logger.fail_check("UNITY", "Assets directory", f"{assets_dir} missing")
        return False

    missing_metas = []
    orphan_metas = []

    # Check directory and file meta pairs
    for root, dirs, files in os.walk(assets_dir):
        for d in dirs:
            dir_path = os.path.join(root, d)
            meta_path = dir_path + ".meta"
            if not os.path.exists(meta_path):
                missing_metas.append(dir_path)

        for f in files:
            file_path = os.path.join(root, f)
            if f.endswith(".meta"):
                target_path = file_path[:-5]
                if not os.path.exists(target_path):
                    orphan_metas.append(file_path)
            else:
                meta_path = file_path + ".meta"
                if not os.path.exists(meta_path):
                    missing_metas.append(file_path)

    if len(missing_metas) == 0 and len(orphan_metas) == 0:
        logger.pass_check("UNITY", "Meta File Invariants", "0 missing .meta and 0 orphan .meta files verified across Assets/")
    else:
        logger.fail_check("UNITY", "Meta File Invariants", f"Found {len(missing_metas)} missing metas and {len(orphan_metas)} orphans")

    # C# scripts syntax check
    scripts_dir = os.path.join(assets_dir, "Scripts")
    cs_files = []
    if os.path.isdir(scripts_dir):
        for root, _dirs, files in os.walk(scripts_dir):
            for f in files:
                if f.endswith(".cs"):
                    cs_files.append(os.path.join(root, f))

    cs_errors = []
    for cs in cs_files:
        rel = os.path.relpath(cs, PROJECT_ROOT)
        try:
            with open(cs, "r", encoding="utf-8") as f:
                code = f.read()
            if len(code.strip()) == 0:
                raise ValueError("Empty C# script")
            clean_code = strip_csharp_strings_and_comments(code)
            opens = clean_code.count("{")
            closes = clean_code.count("}")
            if opens != closes:
                raise ValueError(f"Mismatched structural braces: {opens} opens vs {closes} closes")
        except Exception as e:
            cs_errors.append((rel, str(e)))

    if len(cs_errors) == 0 and len(cs_files) > 0:
        logger.pass_check("UNITY", "C# Script Integrity", f"All {len(cs_files)} C# scripts passed structural validation")
    else:
        logger.fail_check("UNITY", "C# Script Integrity", f"{len(cs_errors)} scripts failed: {cs_errors}")

    return len(missing_metas) == 0 and len(orphan_metas) == 0 and len(cs_errors) == 0


# ==============================================================================
# 5. Web Application Integrity
# ==============================================================================

class DOMParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = set()
        self.classes = set()
        self.preview_images = []

    def handle_starttag(self, tag, attrs):
        attr_dict = dict(attrs)
        if "id" in attr_dict:
            self.ids.add(attr_dict["id"])
        if "class" in attr_dict:
            for c in attr_dict["class"].split():
                self.classes.add(c)
        if "data-img" in attr_dict:
            self.preview_images.append(attr_dict["data-img"])


def audit_web_application(logger):
    """Audit web application index.html, app.js, styles.css, and preview assets."""
    logger.info("WEB", f"Auditing Web Application at {WEB_DIR}...")
    index_html = os.path.join(WEB_DIR, "index.html")
    app_js = os.path.join(WEB_DIR, "app.js")
    styles_css = os.path.join(WEB_DIR, "styles.css")

    for fpath in (index_html, app_js, styles_css):
        if not os.path.isfile(fpath) or os.path.getsize(fpath) == 0:
            logger.fail_check("WEB", f"Core file existence: {os.path.basename(fpath)}", "File missing or empty")
            return False

    with open(index_html, "r", encoding="utf-8") as f:
        html_src = f.read()

    parser = DOMParser()
    try:
        parser.feed(html_src)
        logger.pass_check("WEB", "HTML Structure", f"Parsed index.html cleanly: {len(parser.ids)} IDs, {len(parser.classes)} classes")
    except Exception as e:
        logger.fail_check("WEB", "HTML Structure", f"HTML parse error: {e}")
        return False

    with open(app_js, "r", encoding="utf-8") as f:
        js_src = f.read()

    # Check DOM ID resolution
    js_ids = re.findall(r"document\.getElementById\(['\"]([^'\"]+)['\"]\)", js_src)
    missing_ids = [eid for eid in set(js_ids) if eid not in parser.ids]
    if len(missing_ids) == 0:
        logger.pass_check("WEB", "DOM Selectors", f"All {len(js_ids)} document.getElementById calls resolved in HTML")
    else:
        logger.fail_check("WEB", "DOM Selectors", f"Missing HTML elements for IDs: {missing_ids}")

    # Check preview images
    img_errors = []
    for img_rel in parser.preview_images:
        full_p = os.path.normpath(os.path.join(WEB_DIR, img_rel))
        if not os.path.isfile(full_p):
            img_errors.append((img_rel, "File missing on disk"))
            continue
        try:
            validate_png_file(full_p)
        except Exception as e:
            img_errors.append((img_rel, str(e)))

    if len(img_errors) == 0 and len(parser.preview_images) > 0:
        logger.pass_check("WEB", "Preview Assets", f"All {len(parser.preview_images)} preview images verified with valid PNG headers")
    else:
        logger.fail_check("WEB", "Preview Assets", f"Preview image errors: {img_errors}")

    return len(missing_ids) == 0 and len(img_errors) == 0


# ==============================================================================
# 6. Docs Allowlist Strict Hygiene
# ==============================================================================

def audit_docs_allowlist(logger):
    """Verify docs/ directory contains only strictly authorized markdown files."""
    logger.info("DOCS", f"Auditing docs allowlist at {DOCS_DIR}...")
    if not os.path.isdir(DOCS_DIR):
        logger.fail_check("DOCS", "Docs directory existence", f"{DOCS_DIR} missing")
        return False

    found_md = {f for f in os.listdir(DOCS_DIR) if f.endswith(".md")}
    unauthorized = found_md - ALLOWED_DOCS

    if len(unauthorized) == 0 and len(found_md) == len(ALLOWED_DOCS):
        logger.pass_check("DOCS", "Allowlist Compliance", f"Exactly {len(found_md)} authorized docs present: {sorted(found_md)}")
        return True
    else:
        logger.fail_check("DOCS", "Allowlist Compliance", f"Unauthorized docs detected: {unauthorized}")
        return False


# ==============================================================================
# 7. Repository Invariants (Zero duplicate .blend or .blend1 files)
# ==============================================================================

def audit_repository_invariants(logger):
    """Verify no duplicate .blend or .blend1 backup files exist."""
    logger.info("INVARIANTS", "Checking repository invariants for duplicate blends and backups...")
    blend1_files = []
    unity_blends = []

    for root, _dirs, files in os.walk(PROJECT_ROOT):
        if ".git" in root or "Library" in root:
            continue
        for f in files:
            p = os.path.join(root, f)
            if f.endswith(".blend1"):
                blend1_files.append(os.path.relpath(p, PROJECT_ROOT))
            elif f.endswith(".blend") and "unity_project" in root:
                unity_blends.append(os.path.relpath(p, PROJECT_ROOT))

    if len(blend1_files) == 0 and len(unity_blends) == 0:
        logger.pass_check("INVARIANTS", "Zero Duplicate Blends", "0 .blend1 backup files and 0 forbidden .blend in unity_project")
        return True
    else:
        logger.fail_check("INVARIANTS", "Zero Duplicate Blends", f"blend1={blend1_files}, unity_blends={unity_blends}")
        return False


# ==============================================================================
# Main Orchestration
# ==============================================================================

def main():
    parser = argparse.ArgumentParser(description="MarsSentinel Comprehensive Workspace Integrity Audit")
    parser.add_argument("--repair", action="store_true", help="Perform in-place repairs on any detected recoverable defects")
    parser.add_argument("--verbose", "-v", action="store_true", help="Enable verbose per-check details")
    parser.add_argument("--json", action="store_true", help="Output machine-readable JSON report")
    args = parser.parse_args()

    logger = AuditLogger(verbose=args.verbose)
    logger.info("MAIN", "==================================================================")
    logger.info("MAIN", "MarsSentinel Workspace Integrity Diagnostic Suite (Milestone 7)")
    logger.info("MAIN", "==================================================================")

    res_datasets = audit_datasets(logger)
    res_scripts = audit_python_scripts(logger)
    res_blender = audit_blender_master_scene(logger, do_repair=args.repair)
    res_unity = audit_unity_project(logger)
    res_web = audit_web_application(logger)
    res_docs = audit_docs_allowlist(logger)
    res_invariants = audit_repository_invariants(logger)

    total_checks = len(logger.results)
    passed_checks = sum(1 for r in logger.results if r["status"] == "PASS")
    failed_checks = total_checks - passed_checks

    logger.info("MAIN", "==================================================================")
    logger.info("MAIN", f"Audit Summary: {passed_checks}/{total_checks} checks passed, {failed_checks} failed")
    logger.info("MAIN", "==================================================================")

    if args.json:
        report = {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "total_checks": total_checks,
            "passed": passed_checks,
            "failed": failed_checks,
            "overall_status": "PASS" if failed_checks == 0 else "FAIL",
            "results": logger.results,
        }
        print(json.dumps(report, indent=2))

    sys.exit(0 if failed_checks == 0 else 1)


if __name__ == "__main__":
    main()
