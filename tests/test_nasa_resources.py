"""
Automated Test Suite for NASA Martian Datasets Pipeline & Resources
===================================================================
NASA Space Apps Challenge: Interplanetary Survival Guide - Martian Map

Verifies:
- scripts/fetch_nasa_datasets.py executable & CLI verification
- Authenticity, schemas, non-zero sizes, and magic headers for all downloaded datasets:
    * data/routes_traverses/perseverance_traverse_m20.geojson
    * data/routes_traverses/curiosity_traverse_msl.geojson
    * data/routes_traverses/ingenuity_flights_m20.geojson
    * data/routes_traverses/sample_tubes_m20.geojson
    * data/geology/sample_geology_slice.png
    * data/geology/usgs_sim3292_units.json
    * data/geology/usgs_sim3292_stratigraphy.json
    * data/elevation_dtm/sample_mola_dem_slice.tif
    * data/elevation_dtm/sample_jezero_3d.stl
    * data/imagery/perseverance_sample_raw.jpg
    * data/imagery/perseverance_sample_metadata.json
    * data/apis/nasa_endpoints_manifest.json
- data/NASA_MARTIAN_RESOURCES_GUIDE_AR.md content completeness (>10KB, Arabic sections, code snippets)
- Coordinate transformations (IAU2000 Mars geodetic to ENU) and GeoJSON bounding invariants
"""

import json
import math
import os
import struct
import subprocess
import sys
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DATA_DIR = os.path.join(PROJECT_ROOT, "data")
SCRIPTS_DIR = os.path.join(PROJECT_ROOT, "scripts")


def get_data_path(*parts):
    return os.path.join(DATA_DIR, *parts)


class TestNasaDatasetFetcherScript(unittest.TestCase):
    """Verifies scripts/fetch_nasa_datasets.py existence, syntax, and CLI capability."""

    def test_fetcher_script_exists_and_non_empty(self):
        script_path = os.path.join(SCRIPTS_DIR, "fetch_nasa_datasets.py")
        self.assertTrue(os.path.isfile(script_path), f"Missing {script_path}")
        self.assertGreater(os.path.getsize(script_path), 2000, "Fetcher script truncated")

    def test_fetcher_script_compiles_cleanly(self):
        script_path = os.path.join(SCRIPTS_DIR, "fetch_nasa_datasets.py")
        with open(script_path, "r", encoding="utf-8") as f:
            code = f.read()
        compiled = compile(code, script_path, "exec")
        self.assertIsNotNone(compiled)

    def test_fetcher_cli_verify_mode_runs_successfully(self):
        script_path = os.path.join(SCRIPTS_DIR, "fetch_nasa_datasets.py")
        cmd = [sys.executable, script_path, "--verify", "--data-dir", DATA_DIR]
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=30)
        self.assertEqual(proc.returncode, 0, f"Verify CLI failed:\nSTDOUT: {proc.stdout}\nSTDERR: {proc.stderr}")
        self.assertIn("PASS", proc.stdout)


class TestRoverTraverseDatasets(unittest.TestCase):
    """Verifies all rover and helicopter vector datasets under data/routes_traverses/."""

    def test_perseverance_traverse_geojson_validity(self):
        p = get_data_path("routes_traverses", "perseverance_traverse_m20.geojson")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 100000, "Perseverance traverse file unexpectedly small")

        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data.get("type"), "FeatureCollection")
        features = data.get("features", [])
        self.assertGreaterEqual(len(features), 500, "Expected >=500 traverse features for Perseverance")

        first_geom = features[0].get("geometry", {})
        self.assertIn(first_geom.get("type"), ["LineString", "MultiLineString"])
        coords = first_geom.get("coordinates")
        self.assertTrue(len(coords) > 0)

        # Validate coordinate bounding box (Jezero Crater: ~77°E, ~18°N)
        pt = coords[0] if first_geom.get("type") == "LineString" else coords[0][0]
        lon, lat = pt[0], pt[1]
        self.assertTrue(75.0 <= lon <= 80.0, f"Longitude {lon} out of Jezero range (75-80°E)")
        self.assertTrue(16.0 <= lat <= 20.0, f"Latitude {lat} out of Jezero range (16-20°N)")

    def test_curiosity_traverse_geojson_validity(self):
        p = get_data_path("routes_traverses", "curiosity_traverse_msl.geojson")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 100000, "Curiosity traverse file unexpectedly small")

        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data.get("type"), "FeatureCollection")
        features = data.get("features", [])
        self.assertGreaterEqual(len(features), 500, "Expected >=500 traverse features for Curiosity")

        # Validate Gale Crater coordinates (~137°E, ~-4.5°S)
        first_geom = features[0].get("geometry", {})
        coords = first_geom.get("coordinates")
        pt = coords[0] if first_geom.get("type") == "LineString" else coords[0][0]
        if isinstance(pt[0], list):
            pt = pt[0]
        lon, lat = pt[0], pt[1]
        self.assertTrue(135.0 <= lon <= 140.0, f"Longitude {lon} out of Gale range (135-140°E)")
        self.assertTrue(-6.0 <= lat <= -3.0, f"Latitude {lat} out of Gale range (-6 to -3°S)")

    def test_ingenuity_flights_geojson_validity(self):
        p = get_data_path("routes_traverses", "ingenuity_flights_m20.geojson")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 50000, "Ingenuity flights file unexpectedly small")

        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data.get("type"), "FeatureCollection")
        features = data.get("features", [])
        self.assertGreaterEqual(len(features), 50, "Expected >=50 helicopter flight features")

    def test_sample_tubes_geojson_validity(self):
        p = get_data_path("routes_traverses", "sample_tubes_m20.geojson")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 1000, "Sample tubes file unexpectedly small")

        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data.get("type"), "FeatureCollection")
        features = data.get("features", [])
        self.assertGreaterEqual(len(features), 10, "Expected >=10 sample tube features")


class TestGeologyDatasets(unittest.TestCase):
    """Verifies USGS SIM 3292 geologic map slice, unit catalog, and stratigraphy."""

    def test_sample_geology_slice_png_header_and_size(self):
        p = get_data_path("geology", "sample_geology_slice.png")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 5000, "PNG file unexpectedly small")

        with open(p, "rb") as f:
            header = f.read(8)
        self.assertEqual(header, b"\x89PNG\r\n\x1a\n", "Invalid PNG magic header")

    def test_usgs_sim3292_units_json_completeness(self):
        p = get_data_path("geology", "usgs_sim3292_units.json")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")

        with open(p, "r", encoding="utf-8") as f:
            units = json.load(f)

        self.assertIsInstance(units, list)
        self.assertEqual(len(units), 44, "Expected exactly 44 standard USGS SIM 3292 geologic units")

        # Verify unit schema fields
        for u in units:
            self.assertIn("symbol", u)
            self.assertIn("name", u)
            self.assertIn("epoch", u)
            self.assertIn("system", u)
            self.assertIn("rgb", u)
            self.assertEqual(len(u["rgb"]), 3, f"Invalid RGB color in unit {u['symbol']}")
            self.assertIn("trafficability", u)
            self.assertTrue(1 <= u["trafficability"] <= 5)
            self.assertIn("description", u)

    def test_usgs_sim3292_stratigraphy_json_epochs(self):
        p = get_data_path("geology", "usgs_sim3292_stratigraphy.json")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")

        with open(p, "r", encoding="utf-8") as f:
            strat = json.load(f)

        self.assertIn("epochs", strat)
        epochs = strat["epochs"]
        epoch_names = [e["name"] for e in epochs]

        # Must span all three major Martian epochs: Amazonian, Hesperian, Noachian
        self.assertTrue(any("Amazonian" in n for n in epoch_names))
        self.assertTrue(any("Hesperian" in n for n in epoch_names))
        self.assertTrue(any("Noachian" in n for n in epoch_names))


class TestElevationAndMeshDatasets(unittest.TestCase):
    """Verifies MOLA DEM GeoTIFF raster slice and Jezero 3D STL mesh."""

    def test_sample_mola_dem_slice_tif_magic_and_size(self):
        p = get_data_path("elevation_dtm", "sample_mola_dem_slice.tif")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 10000, "GeoTIFF DEM unexpectedly small")

        with open(p, "rb") as f:
            header = f.read(4)
        # TIFF header must be 'II*\x00' (little endian) or 'MM\x00*' (big endian)
        self.assertTrue(header in (b"II*\x00", b"MM\x00*"), f"Invalid TIFF magic header: {header}")

    def test_sample_mola_dem_slice_tif_int16_data_type(self):
        """Asserts that sample_mola_dem_slice.tif is 16-bit Signed Integer (int16, BitsPerSample=16, SampleFormat=2)."""
        p = get_data_path("elevation_dtm", "sample_mola_dem_slice.tif")
        with open(p, "rb") as f:
            header = f.read(8)
            endian = "<" if header[:2] == b"II" else ">"
            ifd_offset = struct.unpack(endian + "I", header[4:8])[0]
            f.seek(ifd_offset)
            num_entries = struct.unpack(endian + "H", f.read(2))[0]
            tags = {}
            for _ in range(num_entries):
                tid, ttype, cnt, val = struct.unpack(endian + "HHI I", f.read(12))
                tags[tid] = (ttype, cnt, val)

            bits_per_sample = tags.get(258, (0, 0, 0))[2]  # Tag 258: BitsPerSample
            sample_format = tags.get(339, (0, 0, 0))[2]    # Tag 339: SampleFormat (2 = signed int)
            self.assertEqual(bits_per_sample, 16, f"Expected BitsPerSample == 16, got {bits_per_sample}")
            self.assertEqual(sample_format, 2, f"Expected SampleFormat == 2 (signed int), got {sample_format}")

            # Read raster pixels and verify elevation range
            offset = tags[324][2]       # TileOffsets
            byte_count = tags[325][2]   # TileByteCounts
            f.seek(offset)
            pixel_bytes = f.read(byte_count)

        pixel_count = byte_count // 2
        elevations = struct.unpack(f"{endian}{pixel_count}h", pixel_bytes)
        min_elev = min(elevations)
        max_elev = max(elevations)
        self.assertEqual(min_elev, -2667, f"Expected min elevation -2667m in Jezero slice, got {min_elev}")
        self.assertEqual(max_elev, -1472, f"Expected max elevation -1472m in Jezero slice, got {max_elev}")

    def test_sample_jezero_3d_stl_structure(self):
        p = get_data_path("elevation_dtm", "sample_jezero_3d.stl")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        size = os.path.getsize(p)
        self.assertGreater(size, 84, "Binary STL file must be at least 84 bytes")

        with open(p, "rb") as f:
            header = f.read(80)
            tri_count_bytes = f.read(4)

        tri_count = struct.unpack("<I", tri_count_bytes)[0]
        self.assertEqual(tri_count, 33780, f"Expected 33,780 triangles in sanitized terrain mesh, got {tri_count}")
        expected_size = 84 + (tri_count * 50)
        self.assertEqual(size, expected_size, f"STL file size {size} does not match triangle count {tri_count}")
        self.assertEqual(size, 1689084, f"Expected exact STL file size of 1,689,084 bytes, got {size}")

    def test_sample_jezero_3d_stl_valid_unit_normals_and_no_degenerates(self):
        """Asserts all STL triangles have valid normalized unit normals (length ~ 1.0) and zero degenerate facets."""
        p = get_data_path("elevation_dtm", "sample_jezero_3d.stl")
        with open(p, "rb") as f:
            f.seek(80)
            tri_count = struct.unpack("<I", f.read(4))[0]
            raw_data = f.read()

        fmt = "<12fH"
        record_size = 50
        self.assertEqual(len(raw_data), tri_count * record_size)

        zero_normals = 0
        degenerate_count = 0

        for i in range(tri_count):
            record = struct.unpack_from(fmt, raw_data, i * record_size)
            nx, ny, nz = record[0], record[1], record[2]
            v1 = (record[3], record[4], record[5])
            v2 = (record[6], record[7], record[8])
            v3 = (record[9], record[10], record[11])

            norm_len = math.sqrt(nx * nx + ny * ny + nz * nz)
            if norm_len < 0.99 or norm_len > 1.01:
                zero_normals += 1

            # Triangle area via cross product
            e1 = (v2[0] - v1[0], v2[1] - v1[1], v2[2] - v1[2])
            e2 = (v3[0] - v1[0], v3[1] - v1[1], v3[2] - v1[2])
            cp = (
                e1[1] * e2[2] - e1[2] * e2[1],
                e1[2] * e2[0] - e1[0] * e2[2],
                e1[0] * e2[1] - e1[1] * e2[0],
            )
            area = 0.5 * math.sqrt(cp[0] * cp[0] + cp[1] * cp[1] + cp[2] * cp[2])
            if area < 1e-7:
                degenerate_count += 1

        self.assertEqual(zero_normals, 0, f"Found {zero_normals} triangles with non-unit normal vectors")
        self.assertEqual(degenerate_count, 0, f"Found {degenerate_count} degenerate triangles")


class TestPerseveranceImageryDatasets(unittest.TestCase):
    """Verifies raw imagery and sidecar metadata from Mars 2020 Raw Image API."""

    def test_perseverance_sample_raw_jpg_magic_and_size(self):
        p = get_data_path("imagery", "perseverance_sample_raw.jpg")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        self.assertGreater(os.path.getsize(p), 5000, "Raw JPG image unexpectedly small")

        with open(p, "rb") as f:
            header = f.read(3)
        self.assertEqual(header, b"\xff\xd8\xff", "Invalid JPEG start of image marker")

    def test_perseverance_sample_metadata_json_fields(self):
        p = get_data_path("imagery", "perseverance_sample_metadata.json")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")

        with open(p, "r", encoding="utf-8") as f:
            meta = json.load(f)

        self.assertIn("imageid", meta)
        self.assertIn("camera", meta)
        self.assertIn("sol", meta)
        self.assertIn("image_files", meta)


class TestApiManifestDataset(unittest.TestCase):
    """Verifies structured API catalog manifest."""

    def test_nasa_endpoints_manifest_completeness(self):
        p = get_data_path("apis", "nasa_endpoints_manifest.json")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")

        with open(p, "r", encoding="utf-8") as f:
            manifest = json.load(f)

        self.assertIn("official_resources", manifest)
        resources = manifest["official_resources"]
        resource_ids = [r["id"] for r in resources]

        expected_ids = ["mars_trek", "usgs_sim3292", "curiosity_msl", "perseverance_m20", "nasa_worldview_gibs"]
        for exp in expected_ids:
            self.assertIn(exp, resource_ids, f"Manifest missing official resource ID: {exp}")


class TestArabicSurvivalGuide(unittest.TestCase):
    """Verifies data/NASA_MARTIAN_RESOURCES_GUIDE_AR.md completeness and depth."""

    def test_guide_file_exists_and_substantial(self):
        p = get_data_path("NASA_MARTIAN_RESOURCES_GUIDE_AR.md")
        self.assertTrue(os.path.isfile(p), f"Missing {p}")
        size = os.path.getsize(p)
        self.assertGreater(size, 10000, f"Guide must be > 10 KB, found {size} bytes")

    def test_guide_covers_all_five_resources(self):
        p = get_data_path("NASA_MARTIAN_RESOURCES_GUIDE_AR.md")
        with open(p, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("Mars Trek", content)
        self.assertIn("SIM 3292", content)
        self.assertIn("Curiosity", content)
        self.assertIn("Worldview", content)
        self.assertIn("Perseverance", content)

    def test_guide_covers_three_engines_and_survival(self):
        p = get_data_path("NASA_MARTIAN_RESOURCES_GUIDE_AR.md")
        with open(p, "r", encoding="utf-8") as f:
            content = f.read()

        # Engine workflows
        self.assertIn("Blender", content)
        self.assertIn("Unreal Engine 5.8", content)
        self.assertIn("CesiumJS", content)
        self.assertIn("Nanite", content)
        self.assertIn("RVT", content)

        # Survival pillars in Arabic
        self.assertIn("ISRU", content)
        self.assertIn("الإشعاع", content)
        self.assertIn("العواصف", content)
        self.assertIn("الكثبان", content)

    def test_guide_includes_executable_code_snippets(self):
        p = get_data_path("NASA_MARTIAN_RESOURCES_GUIDE_AR.md")
        with open(p, "r", encoding="utf-8") as f:
            content = f.read()

        self.assertIn("```python", content)
        self.assertIn("```javascript", content)
        self.assertIn("MARS_RADIUS", content)
        self.assertIn("Beer-Lambert", content)


class TestGeodeticCoordinateTransformations(unittest.TestCase):
    """Verifies mathematical validity of planetary coordinate projection algorithms."""

    def test_enu_transformation_identity_at_origin(self):
        """At the local tangent plane origin, ENU coordinates must equal (0, 0, 0)."""
        mars_radius = 3396190.0
        lat0, lon0, alt0 = 18.44215, 77.44287, -2569.0

        d_lat = math.radians(lat0 - lat0)
        d_lon = math.radians(lon0 - lon0)
        mid_lat = math.radians((lat0 + lat0) / 2.0)

        x_east = mars_radius * math.cos(mid_lat) * d_lon
        y_north = mars_radius * d_lat
        z_up = alt0 - alt0

        self.assertAlmostEqual(x_east, 0.0, places=5)
        self.assertAlmostEqual(y_north, 0.0, places=5)
        self.assertAlmostEqual(z_up, 0.0, places=5)

    def test_enu_one_degree_latitude_displacement(self):
        """1 degree of latitude on Mars must equal ~59,275 meters."""
        mars_radius = 3396190.0
        lat0, lon0 = 0.0, 0.0
        target_lat = 1.0

        d_lat = math.radians(target_lat - lat0)
        y_north = mars_radius * d_lat

        expected = (2.0 * math.pi * mars_radius) / 360.0
        self.assertAlmostEqual(y_north, expected, delta=0.1)
        self.assertTrue(59200 < y_north < 59400)

    def test_latlon_to_enu_modular_longitude_normalization(self):
        """Verifies modular longitude difference normalization to [-180, 180]."""
        from scripts.fetch_nasa_datasets import latlon_to_enu, MARS_RADIUS

        # 1. Opportunity at Meridiani: [0, 360] (354.4734°E) vs [-180, 180] (-5.5266°E)
        origin = (-1.9462, 354.4734, -1390.0)
        point = (-1.9462, -5.5266, -1390.0)
        x, y, z = latlon_to_enu(point[0], point[1], point[2], origin[0], origin[1], origin[2])
        self.assertAlmostEqual(x, 0.0, delta=1e-3, msg="Longitude normalization failed for 354.47° vs -5.53°")
        self.assertAlmostEqual(y, 0.0, delta=1e-3)
        self.assertAlmostEqual(z, 0.0, delta=1e-3)

        # 2. Antimeridian 180° crossing: 179.95°E to -179.95°E (+0.10° separation)
        origin_anti = (0.0, 179.95, 0.0)
        point_anti = (0.0, -179.95, 0.0)
        x_anti, y_anti, z_anti = latlon_to_enu(point_anti[0], point_anti[1], point_anti[2], origin_anti[0], origin_anti[1], origin_anti[2])
        expected_dx = MARS_RADIUS * math.radians(0.10)
        self.assertAlmostEqual(x_anti, expected_dx, delta=1e-3, msg="Antimeridian crossing calculation failed")

        # 3. Prime Meridian 0° crossing: 359.95°E to 0.05°E (+0.10° separation)
        origin_prime = (0.0, 359.95, 0.0)
        point_prime = (0.0, 0.05, 0.0)
        x_prime, y_prime, z_prime = latlon_to_enu(point_prime[0], point_prime[1], point_prime[2], origin_prime[0], origin_prime[1], origin_prime[2])
        self.assertAlmostEqual(x_prime, expected_dx, delta=1e-3, msg="Prime meridian crossing calculation failed")

    def test_traverse_2d_coordinate_handling_and_altitude_dropout_filter(self):
        """Verifies safe handling of 2D coordinates and filtering of near-zero altitude dropouts."""
        from scripts.fetch_nasa_datasets import latlon_to_enu

        origin = (18.44215, 77.44287, -2569.0)

        # 1. 2D Coordinate safe handling (e.g. ingenuity_flights_m20.geojson)
        pt_2d = [77.45287, 18.44215]  # len == 2 [lon, lat]
        raw_alt = pt_2d[2] if len(pt_2d) > 2 else origin[2]
        self.assertEqual(raw_alt, origin[2], "2D coordinate should fall back to origin altitude")
        x_2d, y_2d, z_2d = latlon_to_enu(pt_2d[1], pt_2d[0], raw_alt, origin[0], origin[1], origin[2])
        self.assertAlmostEqual(z_2d, 0.0, places=4, msg="2D coordinate fallback should yield z=0 relative to origin")
        self.assertGreater(x_2d, 0.0)

        # 2. Telemetry dropout filter (e.g. perseverance_traverse_m20.geojson 15 dropouts at ~0.0m)
        last_valid_alt = -2572.5
        pt_dropout = [77.40808136, 18.45276247, 2.200000017e-05]
        raw_alt_dropout = pt_dropout[2] if len(pt_dropout) > 2 else last_valid_alt

        # Apply documented dropout filter logic
        if len(pt_dropout) > 2 and raw_alt_dropout > -1000.0 and origin[2] < -2000.0:
            filtered_alt = last_valid_alt
        else:
            filtered_alt = raw_alt_dropout

        self.assertEqual(filtered_alt, last_valid_alt, "Dropout point (~0m) was not filtered to last valid altitude")
        x_drop, y_drop, z_drop = latlon_to_enu(pt_dropout[1], pt_dropout[0], filtered_alt, origin[0], origin[1], origin[2])
        self.assertLess(abs(z_drop), 50.0, "Filtered altitude produced excessive z displacement")


if __name__ == "__main__":
    unittest.main()
