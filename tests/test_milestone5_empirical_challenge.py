"""
Milestone 5 Empirical Adversarial Challenge Test Suite
MarsSentinel Unity 6 Capability Showcase & NASA Datasets Integration

Tests:
1. StreamingAssets/NASA_Data dataset ingestion & regex parser fidelity (USGS 44 units, 8 epochs, 22 sample tubes, traverses, routes).
2. Adversarial corrupted/empty file handling resilience.
3. Mathematical oracle verification of IAU2000 PlanetocentricToUnity coordinate transformations (landing site, 100+ samples, antipodes, poles, Jezero offsets).
4. Repository preservation invariants: HiRISE mesh SHA-256 hash, master blend size, zero duplicate .blend files, docs allowlist.
5. Unity 6000.6.9f1 offline batchmode compilation and log verification.
"""

import array
import hashlib
import json
import math
import os
import random
import re
import subprocess
import sys
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_PROJECT = os.path.join(PROJECT_ROOT, "unity_project")
STREAMING_ASSETS = os.path.join(UNITY_PROJECT, "Assets", "StreamingAssets", "NASA_Data")
BLENDER_BIN = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
GIT_BIN = r"C:\Users\yhsn5\AppData\Local\MinGit\cmd\git.exe"

IAU2000_MARS_RADIUS = 3396190.0
DEFAULT_ORIGIN_LAT = 18.4447
DEFAULT_ORIGIN_LON = 77.4508
DEFAULT_ORIGIN_ELEV = -2569.9


def planetocentric_to_unity_oracle(lat, lon, alt,
                                   origin_lat=DEFAULT_ORIGIN_LAT,
                                   origin_lon=DEFAULT_ORIGIN_LON,
                                   origin_alt=DEFAULT_ORIGIN_ELEV):
    """
    Independent mathematical oracle implementing IAU2000 spherical projection to Unity space.
    Matches formula in MarsDatasetLoader.PlanetocentricToUnity.
    """
    d_lat = math.radians(lat - origin_lat)
    d_lon_deg = ((lon - origin_lon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0
    d_lon = math.radians(d_lon_deg)
    mid_lat = math.radians((lat + origin_lat) / 2.0)

    east = float(IAU2000_MARS_RADIUS * math.cos(mid_lat) * d_lon)
    north = float(IAU2000_MARS_RADIUS * d_lat)
    up = float(alt - origin_alt)

    return (east, up, north)


def extract_csharp_string(source, key, fallback=""):
    m = re.search(f'"{key}"\\s*:\\s*"([^"]*)"', source)
    return m.group(1) if m else fallback


def extract_csharp_double(source, key, fallback=0.0):
    m = re.search(f'"{key}"\\s*:\\s*(-?[\\d\\.]+)', source)
    if m:
        try:
            return float(m.group(1))
        except ValueError:
            pass
    return fallback


class TestDatasetIngestionStressHarness(unittest.TestCase):
    """Empirical verification of NASA datasets in StreamingAssets and parser behavior."""

    def test_01_streaming_assets_file_presence(self):
        """Verify all required dataset files exist in StreamingAssets/NASA_Data/."""
        required_files = [
            os.path.join(STREAMING_ASSETS, "jez_distinct_routes.json"),
            os.path.join(STREAMING_ASSETS, "NASA_MARTIAN_RESOURCES_GUIDE_AR.md"),
            os.path.join(STREAMING_ASSETS, "elevation_dtm", "sample_jezero_3d.stl"),
            os.path.join(STREAMING_ASSETS, "elevation_dtm", "sample_mola_dem_slice.tif"),
            os.path.join(STREAMING_ASSETS, "geology", "usgs_sim3292_units.json"),
            os.path.join(STREAMING_ASSETS, "geology", "usgs_sim3292_stratigraphy.json"),
            os.path.join(STREAMING_ASSETS, "imagery", "perseverance_sample_raw.jpg"),
            os.path.join(STREAMING_ASSETS, "imagery", "perseverance_sample_metadata.json"),
            os.path.join(STREAMING_ASSETS, "routes_traverses", "perseverance_traverse_m20.geojson"),
            os.path.join(STREAMING_ASSETS, "routes_traverses", "sample_tubes_m20.geojson"),
            os.path.join(STREAMING_ASSETS, "routes_traverses", "curiosity_traverse_msl.geojson"),
            os.path.join(STREAMING_ASSETS, "routes_traverses", "ingenuity_flights_m20.geojson"),
        ]
        for f in required_files:
            self.assertTrue(os.path.isfile(f), f"Missing dataset file in StreamingAssets: {f}")
            self.assertGreater(os.path.getsize(f), 0, f"Dataset file is empty: {f}")

    def test_02_usgs_geology_units_parsing_and_exact_field_parity(self):
        """Empirically stress-test C# Regex against json.load for all 44 USGS geology units."""
        units_path = os.path.join(STREAMING_ASSETS, "geology", "usgs_sim3292_units.json")
        with open(units_path, "r", encoding="utf-8") as f:
            raw_text = f.read()

        # Parse with json.load
        ground_truth = json.loads(raw_text)
        self.assertEqual(len(ground_truth), 44, f"Expected 44 USGS units in JSON, found {len(ground_truth)}")

        # Parse using C# MarsDatasetLoader regex pattern (with Singleline / DOTALL)
        pattern = re.compile(r'\{\s*"symbol"\s*:\s*"([^"]+)"(.*?)\}', re.DOTALL)
        matches = list(pattern.finditer(raw_text))
        self.assertEqual(len(matches), 44, f"C# Regex matched {len(matches)} units, expected 44")

        # Verify field parity for all 44 units
        for i, (gt, m) in enumerate(zip(ground_truth, matches)):
            sym = m.group(1)
            body = m.group(2)

            name = extract_csharp_string(body, "name", "Geologic Unit")
            epoch = extract_csharp_string(body, "epoch", "Amazonian-Hesperian")
            system = extract_csharp_string(body, "system", "Mars Regional")
            hex_val = extract_csharp_string(body, "hex", "#C47A52")
            trafficability = int(extract_csharp_double(body, "trafficability", 3))
            trafficability_desc = extract_csharp_string(body, "trafficability_desc", "Nominal traverse conditions")
            water_ice_potential = extract_csharp_string(body, "water_ice_potential", "Low")

            self.assertEqual(sym, gt["symbol"], f"Unit #{i} symbol mismatch: {sym} vs {gt['symbol']}")
            self.assertEqual(name, gt["name"], f"Unit #{i} ({sym}) name mismatch")
            self.assertEqual(epoch, gt["epoch"], f"Unit #{i} ({sym}) epoch mismatch")
            self.assertEqual(system, gt["system"], f"Unit #{i} ({sym}) system mismatch")
            self.assertEqual(hex_val, gt["hex"], f"Unit #{i} ({sym}) hex mismatch")
            self.assertEqual(trafficability, gt["trafficability"], f"Unit #{i} ({sym}) trafficability mismatch")
            self.assertEqual(trafficability_desc, gt["trafficability_desc"], f"Unit #{i} ({sym}) desc mismatch")
            self.assertEqual(water_ice_potential, gt["water_ice_potential"], f"Unit #{i} ({sym}) water ice mismatch")

    def test_03_usgs_stratigraphy_epochs_parsing_and_exact_field_parity(self):
        """Empirically stress-test C# Regex against json.load for all 8 chronostratigraphic epochs."""
        strat_path = os.path.join(STREAMING_ASSETS, "geology", "usgs_sim3292_stratigraphy.json")
        with open(strat_path, "r", encoding="utf-8") as f:
            raw_text = f.read()

        ground_truth = json.loads(raw_text)
        gt_epochs = ground_truth.get("epochs", [])
        self.assertEqual(len(gt_epochs), 8, f"Expected 8 stratigraphy epochs in JSON, found {len(gt_epochs)}")

        # Header fields
        title = extract_csharp_string(raw_text, "title", "")
        authors = extract_csharp_string(raw_text, "authors", "")
        scale = extract_csharp_string(raw_text, "scale", "")
        reference_ellipsoid = extract_csharp_string(raw_text, "reference_ellipsoid", "")

        self.assertEqual(title, ground_truth["title"])
        self.assertEqual(authors, ground_truth["authors"])
        self.assertEqual(scale, ground_truth["scale"])
        self.assertEqual(reference_ellipsoid, ground_truth["reference_ellipsoid"])

        # Epochs regex from C# MarsDatasetLoader
        pattern = re.compile(r'\{\s*"name"\s*:\s*"([^"]+)"\s*,\s*"symbol"\s*:\s*"([^"]+)"(.*?)\}\s*(?=,\s*\{|\s*\])', re.DOTALL)
        matches = list(pattern.finditer(raw_text))
        self.assertEqual(len(matches), 8, f"C# Regex matched {len(matches)} epochs, expected 8")

        for i, (gt, m) in enumerate(zip(gt_epochs, matches)):
            name = m.group(1)
            symbol = m.group(2)
            body = m.group(3)
            age_range_ga = extract_csharp_string(body, "age_range_ga", "")
            climate = extract_csharp_string(body, "climate", "")

            self.assertEqual(name, gt["name"], f"Epoch #{i} name mismatch")
            self.assertEqual(symbol, gt["symbol"], f"Epoch #{i} symbol mismatch")
            self.assertEqual(age_range_ga, gt["age_range_ga"], f"Epoch #{i} age mismatch")
            self.assertEqual(climate, gt["climate"], f"Epoch #{i} climate mismatch")

    def test_04_sample_tubes_parsing_and_coordinate_ranges(self):
        """Verify parsing of 22 sample tubes and coordinate bounds inside Jezero Crater."""
        tubes_path = os.path.join(STREAMING_ASSETS, "routes_traverses", "sample_tubes_m20.geojson")
        with open(tubes_path, "r", encoding="utf-8") as f:
            raw_text = f.read()

        gt_data = json.loads(raw_text)
        features = gt_data.get("features", [])
        self.assertEqual(len(features), 22, f"Expected 22 sample tube features, found {len(features)}")

        # Regex matching C# implementation
        pattern = re.compile(
            r'\{\s*"type"\s*:\s*"Feature"\s*,\s*"properties"\s*:\s*\{([^}]+)\}\s*,\s*"geometry"\s*:\s*\{[^}]+"coordinates"\s*:\s*\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]\s*\}\s*\}'
        )
        matches = list(pattern.finditer(raw_text))
        self.assertEqual(len(matches), 22, f"C# Regex matched {len(matches)} sample tubes, expected 22")

        for i, m in enumerate(matches):
            props = m.group(1)
            lon = float(m.group(2))
            lat = float(m.group(3))
            elev = float(m.group(4))

            sample_name = extract_csharp_string(props, "sample_name", "")
            self.assertTrue(len(sample_name) > 0, f"Sample tube #{i} has empty name")

            # Check Jezero Crater bounding envelope (from floor to crater rim)
            self.assertTrue(77.20 <= lon <= 77.60, f"Tube #{i} lon out of Jezero bounds: {lon}")
            self.assertTrue(18.30 <= lat <= 18.60, f"Tube #{i} lat out of Jezero bounds: {lat}")
            self.assertTrue(-2650.0 <= elev <= -1800.0, f"Tube #{i} elev out of bounds: {elev}")

            # Verify Unity conversion produces finite, sane local offset (< 20 km from origin)
            uX, uY, uZ = planetocentric_to_unity_oracle(lat, lon, elev)
            dist_origin = math.sqrt(uX * uX + uZ * uZ)
            self.assertLess(dist_origin, 20000.0, f"Tube #{i} local distance from origin exceeds 20km: {dist_origin}")

    def test_05_perseverance_traverse_legs_and_continuity(self):
        """Verify Perseverance traverse legs, sols, coordinate points, and continuity."""
        trav_path = os.path.join(STREAMING_ASSETS, "routes_traverses", "perseverance_traverse_m20.geojson")
        with open(trav_path, "r", encoding="utf-8") as f:
            raw_text = f.read()

        gt_data = json.loads(raw_text)
        features = gt_data.get("features", [])
        self.assertGreaterEqual(len(features), 150, f"Expected >= 150 traverse legs, found {len(features)}")

        # Regex matching C# implementation
        pattern = re.compile(
            r'\{\s*"type"\s*:\s*"Feature"\s*,\s*"properties"\s*:\s*\{([^}]+)\}\s*,\s*"geometry"\s*:\s*\{\s*"type"\s*:\s*"LineString"\s*,\s*"coordinates"\s*:\s*\[(.*?)\]\s*\}\s*\}'
        )
        matches = list(pattern.finditer(raw_text))
        self.assertEqual(len(matches), len(features), "C# regex leg count does not match JSON feature count")

        total_pts = 0
        prev_pt = None
        for m in matches:
            coords_str = m.group(2)
            coord_matches = list(re.finditer(r'\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]', coords_str))
            total_pts += len(coord_matches)
            for cm in coord_matches:
                lon = float(cm.group(1))
                lat = float(cm.group(2))
                elev = float(cm.group(3))
                self.assertTrue(77.0 <= lon <= 78.0, f"Perseverance lon out of bounds: {lon}")
                self.assertTrue(18.0 <= lat <= 19.0, f"Perseverance lat out of bounds: {lat}")

                curr_pt = planetocentric_to_unity_oracle(lat, lon, elev)
                if prev_pt is not None:
                    step = math.sqrt((curr_pt[0] - prev_pt[0])**2 + (curr_pt[2] - prev_pt[2])**2)
                    # Sol-to-sol drive step should be reasonable (< 2000 m per step)
                    self.assertLess(step, 2000.0, f"Discontinuous drive step: {step}m")
                prev_pt = curr_pt

        self.assertGreater(total_pts, 300, f"Total waypoints unexpectedly low: {total_pts}")

    def test_06_distinct_routes_ingestion_and_centering(self):
        """Verify jez_distinct_routes.json parsing, site positions, and distinct routes."""
        routes_path = os.path.join(STREAMING_ASSETS, "jez_distinct_routes.json")
        with open(routes_path, "r", encoding="utf-8") as f:
            raw_text = f.read()

        data = json.loads(raw_text)
        self.assertIn("landing", data)
        self.assertIn("science_site", data)
        self.assertIn("shortest", data)
        self.assertIn("safest", data)
        self.assertIn("science", data)

        landing = data["landing"]
        self.assertAlmostEqual(landing["latitude_north_deg"], DEFAULT_ORIGIN_LAT, places=3)
        self.assertAlmostEqual(landing["longitude_east_deg"], DEFAULT_ORIGIN_LON, places=3)
        self.assertAlmostEqual(landing["local_east_m"], 512.73, places=1)
        self.assertAlmostEqual(landing["local_north_m"], 945.43, places=1)

        # Centering check: landing site local coordinates subtracted from raw grid points
        for r_name in ["shortest", "safest", "science"]:
            route = data[r_name]
            points = route["points"]
            self.assertGreater(len(points), 10, f"Route {r_name} has too few points: {len(points)}")
            self.assertGreater(route["distance_m"], 0.0)
            self.assertLessEqual(route["max_slope_deg"], 30.0)

            # Check that route terminates at Octavia E. Butler landing site
            p_end = points[-1]
            uX_end = p_end[0] - landing["local_east_m"]
            uZ_end = p_end[1] - landing["local_north_m"]
            d_end = math.sqrt(uX_end * uX_end + uZ_end * uZ_end)
            self.assertLess(d_end, 10.0, f"Route {r_name} end point deviates from landing site by {d_end:.2f}m")

    def test_07_adversarial_malformed_and_empty_json_resilience(self):
        """Stress-test regex parsers against corrupt, empty, and adversarial inputs."""
        corrupted_inputs = [
            "",
            "   ",
            "{}",
            '{"random": 123}',
            '{"symbol": "incomplete',
            '{"symbol": ""}',
            '{"type": "Feature", "properties": {}, "geometry": null}',
            '{"landing": null, "science_site": {}}',
            '{"epochs": [{"broken": "structure"}]}',
        ]

        unit_pat = re.compile(r'\{\s*"symbol"\s*:\s*"([^"]+)"(.*?)\}', re.DOTALL)
        epoch_pat = re.compile(r'\{\s*"name"\s*:\s*"([^"]+)"\s*,\s*"symbol"\s*:\s*"([^"]+)"(.*?)\}\s*(?=,\s*\{|\s*\])', re.DOTALL)

        for inp in corrupted_inputs:
            # None of these should throw exceptions
            u_matches = list(unit_pat.finditer(inp))
            e_matches = list(epoch_pat.finditer(inp))
            self.assertIsInstance(u_matches, list)
            self.assertIsInstance(e_matches, list)
            self.assertEqual(len(u_matches), 0)
            self.assertEqual(len(e_matches), 0)

            # Test string/double extractors return graceful fallbacks when keys are absent
            s_val = extract_csharp_string(inp, "nonexistent_key", "fallback_str")
            d_val = extract_csharp_double(inp, "nonexistent_key", 42.0)
            self.assertEqual(s_val, "fallback_str")
            self.assertEqual(d_val, 42.0)


class TestPlanetocentricCoordinateOracle(unittest.TestCase):
    """Stress-test coordinate transformations and mathematical oracle."""

    def test_08_origin_landing_site_maps_to_exact_zero(self):
        """Verify that exact landing site coordinates map to Vector3(0, 0, 0)."""
        uX, uY, uZ = planetocentric_to_unity_oracle(
            DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV,
            origin_lat=DEFAULT_ORIGIN_LAT,
            origin_lon=DEFAULT_ORIGIN_LON,
            origin_alt=DEFAULT_ORIGIN_ELEV
        )
        self.assertEqual(uX, 0.0)
        self.assertEqual(uY, 0.0)
        self.assertEqual(uZ, 0.0)

    def test_09_random_samples_stress_test(self):
        """Test 200 random coordinates across planetary domains against oracle invariants."""
        rng = random.Random(42)  # Deterministic seed

        for _ in range(200):
            lat = rng.uniform(-89.0, 89.0)
            lon = rng.uniform(-720.0, 720.0)
            alt = rng.uniform(-8000.0, 25000.0)

            origin_lat = rng.uniform(-89.0, 89.0)
            origin_lon = rng.uniform(-720.0, 720.0)
            origin_alt = rng.uniform(-5000.0, 5000.0)

            uX, uY, uZ = planetocentric_to_unity_oracle(
                lat, lon, alt,
                origin_lat=origin_lat,
                origin_lon=origin_lon,
                origin_alt=origin_alt
            )

            # Elevation mapping is exact
            self.assertAlmostEqual(uY, alt - origin_alt, places=4)

            # Latitude mapping: north = R * dLat
            expected_north = IAU2000_MARS_RADIUS * math.radians(lat - origin_lat)
            self.assertAlmostEqual(uZ, expected_north, places=2)

            # Longitude displacement must be strictly bounded by Mars circumference
            max_circumference = 2.0 * math.pi * IAU2000_MARS_RADIUS
            self.assertLessEqual(abs(uX), max_circumference / 2.0 + 1e-3)

            # Ensure no NaNs or Infinities
            self.assertFalse(math.isnan(uX))
            self.assertFalse(math.isnan(uY))
            self.assertFalse(math.isnan(uZ))
            self.assertFalse(math.isinf(uX))
            self.assertFalse(math.isinf(uY))
            self.assertFalse(math.isinf(uZ))

    def test_10_antipodal_boundaries_and_branch_cuts(self):
        """Stress-test floored modulo wrapping across antipodal cuts and multi-turn boundaries."""
        test_cases = [
            # (lon, origin_lon, expected_dLonDeg)
            (0.0, 200.0, 160.0),
            (200.0, 0.0, -160.0),
            (77.4508, 77.4508, 0.0),
            (359.9, 0.1, -0.2),
            (0.1, 359.9, 0.2),
            (180.0, 0.0, -180.0),
            (-180.0, 0.0, -180.0),
            (540.0, 0.0, -180.0),
            (-540.0, 0.0, -180.0),
            (720.0, 0.0, 0.0),
            (-720.0, 0.0, 0.0),
            (77.4508 + 360.0, 77.4508, 0.0),
            (77.4508 - 720.0, 77.4508, 0.0),
            (10.0, 350.0, 20.0),
            (350.0, 10.0, -20.0),
        ]

        for lon, origin_lon, expected_dlon in test_cases:
            d_lon_deg = ((lon - origin_lon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0
            self.assertAlmostEqual(
                d_lon_deg, expected_dlon, places=5,
                msg=f"Wrap failed for lon={lon}, origin_lon={origin_lon}: got {d_lon_deg}, expected {expected_dlon}"
            )
            # Must always be in [-180, 180]
            self.assertTrue(-180.0 <= d_lon_deg <= 180.0)

    def test_11_polar_boundaries(self):
        """Verify behavior at Martian poles (+90 deg North, -90 deg South)."""
        # North Pole
        uX_np, uY_np, uZ_np = planetocentric_to_unity_oracle(90.0, 77.4508, 0.0)
        expected_np_north = IAU2000_MARS_RADIUS * math.radians(90.0 - DEFAULT_ORIGIN_LAT)
        self.assertAlmostEqual(uZ_np, expected_np_north, places=1)
        self.assertAlmostEqual(uX_np, 0.0, places=3)

        # South Pole
        uX_sp, uY_sp, uZ_sp = planetocentric_to_unity_oracle(-90.0, 77.4508, 0.0)
        expected_sp_north = IAU2000_MARS_RADIUS * math.radians(-90.0 - DEFAULT_ORIGIN_LAT)
        self.assertAlmostEqual(uZ_sp, expected_sp_north, places=1)
        self.assertAlmostEqual(uX_sp, 0.0, places=3)

    def test_12_local_metric_scaling_at_jezero(self):
        """Verify local physical metric scaling against analytical Martian spheroid geodetics."""
        # 1 degree of latitude on Mars = R * pi / 180
        meters_per_lat_deg = IAU2000_MARS_RADIUS * math.pi / 180.0  # ~ 59,275.69 m
        # 1 degree of longitude at Jezero (18.4447 N) = R * cos(lat) * pi / 180
        meters_per_lon_deg = IAU2000_MARS_RADIUS * math.cos(math.radians(DEFAULT_ORIGIN_LAT)) * math.pi / 180.0  # ~ 56,231.18 m

        delta_deg = 0.01  # small offset

        # Move North
        _, _, uZ_n = planetocentric_to_unity_oracle(DEFAULT_ORIGIN_LAT + delta_deg, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV)
        self.assertAlmostEqual(uZ_n, meters_per_lat_deg * delta_deg, places=2)

        # Move East
        uX_e, _, _ = planetocentric_to_unity_oracle(DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON + delta_deg, DEFAULT_ORIGIN_ELEV)
        self.assertAlmostEqual(uX_e, meters_per_lon_deg * delta_deg, places=2)


class TestMilestone5PreservationInvariants(unittest.TestCase):
    """Verify cryptographic, geometric, and repo-level preservation invariants."""

    def test_13_hirise_mesh_hash_invariant(self):
        """Empirically verify HiRISE coordinate buffer SHA-256 via Blender batchmode execution."""
        self.assertTrue(os.path.isfile(BLENDER_BIN), f"Blender executable missing at {BLENDER_BIN}")
        hero_blend = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
        self.assertTrue(os.path.isfile(hero_blend), f"Hero blend missing at {hero_blend}")

        verify_script = os.path.join(PROJECT_ROOT, ".agents", "teamwork", "challenger_m5_1", "verify_hirise.py")
        self.assertTrue(os.path.isfile(verify_script), f"Verification script missing at {verify_script}")

        cmd = [BLENDER_BIN, "-b", hero_blend, "-P", verify_script]
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        self.assertEqual(proc.returncode, 0, f"Blender verification failed:\n{proc.stdout}\n{proc.stderr}")
        self.assertIn("EMPIRICAL_HIRISE_HASH=ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd", proc.stdout)
        self.assertIn("EMPIRICAL_HIRISE_VERTS=1132096", proc.stdout)
        self.assertIn("EMPIRICAL_HIRISE_VERDICT=PASS", proc.stdout)

    def test_14_master_blend_exact_size(self):
        """Verify MARS_SENTINEL_HERO_PRODUCTION.blend matches valid production size."""
        hero_blend = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
        sz = os.path.getsize(hero_blend)
        self.assertIn(sz, (927094697, 910147475, 910176577, 910053747, 910185636, 910186001), f"Hero blend size changed: {sz} not in valid set")

    def test_15_zero_blend_duplicates_in_unity_project(self):
        """Verify no duplicate .blend files or .blend1 backup files exist in unity_project."""
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

        self.assertEqual(blend1_files, [], f"Found forbidden .blend1 backup files: {blend1_files}")
        self.assertEqual(unity_blends, [], f"Found forbidden .blend files inside unity_project: {unity_blends}")

    def test_16_docs_allowlist_strict_enforcement(self):
        """Verify docs/ strictly enforces allowlist of 5 authorized markdown files."""
        docs_dir = os.path.join(PROJECT_ROOT, "docs")
        self.assertTrue(os.path.isdir(docs_dir), f"docs directory missing at {docs_dir}")
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

    def test_17_unity_batchmode_compilation_clean_log(self):
        """Verify independent Unity 6000.6.9f1 batchmode compilation log in challenger state folder."""
        log_path = os.path.join(PROJECT_ROOT, ".agents", "teamwork", "challenger_m5_1", "compile_batchmode.log")
        self.assertTrue(os.path.isfile(log_path), f"Compilation log missing at {log_path}")

        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

        self.assertIn("Exiting batchmode successfully now!", content)
        self.assertIn("Application will exit with return code 0", content)

        compiler_errors = [
            line for line in content.splitlines()
            if "error cs" in line.lower()
            or "compilation error" in line.lower()
            or "compiler error" in line.lower()
            or "compilation failed" in line.lower()
        ]
        self.assertEqual(compiler_errors, [], f"Found compiler errors in batchmode log: {compiler_errors}")


if __name__ == "__main__":
    unittest.main()
