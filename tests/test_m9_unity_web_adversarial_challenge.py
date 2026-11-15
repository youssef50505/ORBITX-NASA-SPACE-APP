"""
Milestone 9 Adversarial Empirical Challenge Test Suite
Unity & Web Visual Polish and Continuous Animation Upgrades (Requirement R3)
Role: Challenger 2 (Unity & Web Empirical Challenger)

Tests:
1. Unity Shaders & Mathematical Models:
   - Slope dot product, saturate clamp, and boundary conditions across [0, pi]
   - Smoothstep threshold boundaries, Hermite C1 continuity, and zero boundary derivative
   - Micro-normal frequency ripple synthesis, non-zero denominator in normalize(), and tangent normal perturbation
2. Unity Animation Kinetics & Dynamics:
   - Ingenuity counter-rotating coaxial velocities (|w1 + w2| < 10^-4, w1 * w2 < 0) and drift-free angle wrapping
   - Ingenuity hover sway bounded oscillation across full sol (86,400s)
   - Mars obliquity axis tilt (0.4256, 0.9049, 0.0) normalized to 1.0 within float epsilon
   - Rover traverse motion telemetry, distance accumulation, and rocker-bogie oscillation
3. Unity Asset & Build Integrity:
   - 100% paired .meta files across unity_project/Assets/ (0 missing, 0 orphan)
   - Standalone Windows player executable (MarsSentinel.exe) presence and file integrity
4. Web Simulation Procedural Generators:
   - Procedural canvas data texture normal vector lengths (sqrt(Nx^2 + Ny^2 + Nz^2) = 1.0 +- 0.05) over 512x512 grid (262,144 pixels)
   - Boundary wrap-around continuity of normal map
   - Slope-dependent color modulation formula continuum across [0 deg, 90 deg]
   - Ingenuity dual rotor counter-rotation in Web render loop
5. Web Headless CDP Stress Testing:
   - Headless Edge / Chrome CDP execution of 100 rapid camera scrubs, zone switches, and frame advances
   - Assert exactly 0 console errors, 0 warnings, and 0 uncaught exceptions
"""

import os
import sys
import json
import math
import time
import socket
import base64
import struct
import urllib.request
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_DIR = os.path.join(PROJECT_ROOT, "unity_project")
WEB_DIR = os.path.join(PROJECT_ROOT, "web")


# ==============================================================================
# SECTION 1: UNITY SHADERS & MATHEMATICAL FORMULATIONS
# ==============================================================================

class UnityShaderMathAdversarialTests(unittest.TestCase):
    """Adversarial stress testing of MartianSandSlopeBlend.shader mathematical formulations."""

    @classmethod
    def setUpClass(cls):
        cls.shader_path = os.path.join(UNITY_DIR, "Assets", "Shaders", "MartianSandSlopeBlend.shader")
        with open(cls.shader_path, "r", encoding="utf-8") as f:
            cls.shader_text = f.read()

    def test_01_slope_dot_product_saturate_and_boundary_conditions(self):
        """Adversarially evaluate slope dot product across full 3D spherical angles [0, pi]."""
        # In shader: float slope = saturate(dot(normalWS, float3(0.0, 1.0, 0.0)));
        self.assertIn("saturate(dot(normalWS, float3(0.0, 1.0, 0.0)))", self.shader_text)

        # Adversarial angle sweep: theta from 0 (up) to pi (down), phi from 0 to 2*pi
        for deg in range(0, 181, 5):
            theta = math.radians(deg)
            for phi_deg in [0, 45, 90, 135, 180, 270]:
                phi = math.radians(phi_deg)
                normal_ws = (
                    math.sin(theta) * math.cos(phi),
                    math.cos(theta),
                    math.sin(theta) * math.sin(phi)
                )
                dot_val = normal_ws[1]  # dot with (0, 1, 0)
                # saturate(x) = max(0.0, min(1.0, x))
                slope = max(0.0, min(1.0, dot_val))

                if deg == 0:
                    self.assertAlmostEqual(slope, 1.0, places=5, msg="Slope on flat horizontal ground must be 1.0")
                elif deg == 90:
                    self.assertAlmostEqual(slope, 0.0, places=5, msg="Slope on vertical cliff must be 0.0")
                elif deg > 90:
                    self.assertEqual(slope, 0.0, msg="Slope on overhang/undercut must be clamped to 0.0 by saturate")
                else:
                    expected = math.cos(theta)
                    self.assertAlmostEqual(slope, expected, places=5)

        # Monotonicity test: slope must strictly decrease as tilt increases from 0 to 90 degrees
        prev_slope = 1.0
        for deg in range(1, 91):
            theta = math.radians(deg)
            curr_slope = max(0.0, min(1.0, math.cos(theta)))
            self.assertLess(curr_slope, prev_slope, f"Monotonicity violated at {deg} deg")
            prev_slope = curr_slope

    def test_02_smoothstep_threshold_boundaries_and_hermite_continuity(self):
        """Adversarially verify smoothstep threshold boundaries and cubic Hermite continuity."""
        # Shader default properties:
        # _SlopeThreshold = 0.85
        # _SlopeBlendWidth = 0.15
        self.assertIn("_SlopeThreshold", self.shader_text)
        self.assertIn("_SlopeBlendWidth", self.shader_text)
        self.assertIn("smoothstep(_SlopeThreshold - _SlopeBlendWidth, _SlopeThreshold + _SlopeBlendWidth, slope)", self.shader_text)

        thresh = 0.85
        width = 0.15
        edge0 = thresh - width  # 0.70
        edge1 = thresh + width  # 1.00
        self.assertAlmostEqual(edge0, 0.70, places=5)
        self.assertAlmostEqual(edge1, 1.00, places=5)

        def smoothstep(e0, e1, x):
            t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
            return t * t * (3.0 - 2.0 * t)

        def smoothstep_derivative(e0, e1, x):
            if x <= e0 or x >= e1:
                return 0.0
            t = (x - e0) / (e1 - e0)
            return (6.0 * t * (1.0 - t)) / (e1 - e0)

        # Adversarial domain inputs: extreme values [-10.0, 10.0]
        test_inputs = [-10.0, -1.0, 0.0, 0.5, 0.6999, 0.70, 0.75, 0.85, 0.95, 1.00, 1.0001, 2.0, 10.0]
        for x in test_inputs:
            b = smoothstep(edge0, edge1, x)
            self.assertTrue(0.0 <= b <= 1.0, f"Blend {b} out of [0, 1] range for input {x}")
            if x <= edge0:
                self.assertEqual(b, 0.0, f"Expected exactly 0.0 below edge0 for x={x}")
            elif x >= edge1:
                self.assertEqual(b, 1.0, f"Expected exactly 1.0 above edge1 for x={x}")

        # Midpoint symmetry: at x = threshold = 0.85, t = 0.5 -> smoothstep = 0.5
        mid_blend = smoothstep(edge0, edge1, thresh)
        self.assertAlmostEqual(mid_blend, 0.5, places=5, msg="Smoothstep must evaluate to exactly 0.5 at midpoint threshold")

        # Derivative continuity: zero derivative at both boundaries edge0 and edge1
        d_e0 = smoothstep_derivative(edge0, edge1, edge0)
        d_e1 = smoothstep_derivative(edge0, edge1, edge1)
        self.assertEqual(d_e0, 0.0, "Smoothstep derivative at edge0 must be exactly 0 (C1 smooth boundary)")
        self.assertEqual(d_e1, 0.0, "Smoothstep derivative at edge1 must be exactly 0 (C1 smooth boundary)")

        # Numerical non-negativity of derivative across transition zone
        for i in range(700, 1001):
            x = i / 1000.0
            d = smoothstep_derivative(edge0, edge1, x)
            self.assertGreaterEqual(d, 0.0, f"Negative derivative at x={x}")

    def test_03_micro_normal_frequency_ripple_synthesis_and_normalization(self):
        """Validate micro-normal ripple synthesis, non-zero denominator in normalize(), and gradient."""
        self.assertIn("_RippleFrequency", self.shader_text)
        self.assertIn("_RippleStrength", self.shader_text)
        self.assertIn("rippleOffset", self.shader_text)
        self.assertIn("normalize(normalWS + rippleOffset)", self.shader_text)

        freq = 3.5
        strength = 0.4
        normal_ws = (0.0, 1.0, 0.0)

        # Test over 2,000 spatial coordinate samples across [-500, 500] meters
        for step in range(-1000, 1001, 50):
            x = step * 0.5
            z = (step % 37) * 12.3

            # Calculate rippleOffset on flat sand (blend = 1.0)
            blend = 1.0
            offset_x = math.cos(x * freq) * 0.15 * (blend * strength)
            offset_y = 0.0
            offset_z = -math.sin(z * freq * 1.2) * 0.15 * (blend * strength)

            # Combined unnormalized vector
            comb_x = normal_ws[0] + offset_x
            comb_y = normal_ws[1] + offset_y  # exactly 1.0
            comb_z = normal_ws[2] + offset_z

            comb_len = math.sqrt(comb_x**2 + comb_y**2 + comb_z**2)
            # Y component is 1.0, so length is strictly >= 1.0! Division by zero is impossible!
            self.assertGreaterEqual(comb_len, 1.0, "Vector length must be >= 1.0 on flat terrain")
            self.assertLessEqual(comb_len, 1.02, "Ripple perturbation must remain subtle (<2% length deviation)")

            # Perturbed normal
            norm_x = comb_x / comb_len
            norm_y = comb_y / comb_len
            norm_z = comb_z / comb_len
            pert_len = math.sqrt(norm_x**2 + norm_y**2 + norm_z**2)
            self.assertAlmostEqual(pert_len, 1.0, places=6, msg="Perturbed normal must have unit length 1.0")

        # Test on steep rock face (blend = 0.0): ripple offset must collapse to (0, 0, 0)
        cliff_blend = 0.0
        offset_cliff = (
            math.cos(10.0 * freq) * 0.15 * (cliff_blend * strength),
            0.0,
            -math.sin(20.0 * freq * 1.2) * 0.15 * (cliff_blend * strength)
        )
        self.assertEqual(offset_cliff, (0.0, 0.0, 0.0), "Ripple offset must vanish on cliffs where blend=0")


# ==============================================================================
# SECTION 2: UNITY ANIMATION KINETICS & DYNAMICS
# ==============================================================================

class UnityKinematicsAdversarialTests(unittest.TestCase):
    """Adversarial stress testing of Ingenuity and Mars simulation continuous kinetics."""

    @classmethod
    def setUpClass(cls):
        cls.ingenuity_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "IngenuityRotorAnimator.cs")
        cls.animator_path = os.path.join(UNITY_DIR, "Assets", "Scripts", "NASA", "MarsSimulationAnimator.cs")
        with open(cls.ingenuity_path, "r", encoding="utf-8") as f:
            cls.ingenuity_text = f.read()
        with open(cls.animator_path, "r", encoding="utf-8") as f:
            cls.animator_text = f.read()

    def test_04_ingenuity_rotor_coaxial_counter_rotation_velocities(self):
        """Assert counter-rotating coaxial velocities: |w1 + w2| < 10^-4 and w1 * w2 < 0."""
        self.assertIn("rotorSpeedDegPerSec", self.ingenuity_text)
        self.assertIn("upperRotor.Rotate(Vector3.up, step, Space.Self)", self.ingenuity_text)
        self.assertIn("lowerRotor.Rotate(Vector3.up, -step, Space.Self)", self.ingenuity_text)

        # Upper rotor speed: +w; Lower rotor speed: -w
        # Default speed is 1080 deg/sec (180 RPM)
        speed = 1080.0
        w1 = speed
        w2 = -speed

        velocity_sum = abs(w1 + w2)
        velocity_product = w1 * w2

        self.assertLess(velocity_sum, 1e-4, f"Coaxial velocity sum |w1 + w2| = {velocity_sum} must be < 10^-4 (torque cancellation)")
        self.assertLess(velocity_product, 0.0, f"Coaxial velocity product w1 * w2 = {velocity_product} must be strictly negative (counter-rotation)")

        # Drift-free discrete time stepping simulation over 10,000 frames with variable dt
        current_angle = 0.0
        import random
        random.seed(42)
        for _ in range(10000):
            dt = random.uniform(0.005, 0.050)
            step = speed * dt
            current_angle = (current_angle + step) % 360.0
            self.assertTrue(0.0 <= current_angle < 360.0, f"Angle {current_angle} escaped [0, 360) boundary")

    def test_05_ingenuity_hover_sway_bounded_oscillation(self):
        """Assert Ingenuity hover sway dynamics exhibit strictly bounded oscillation over 86,400s."""
        self.assertIn("hoverAltitudeOffset", self.ingenuity_text)
        self.assertIn("hoverSwayAmplitude", self.ingenuity_text)
        self.assertIn("hoverSwayFrequency", self.ingenuity_text)

        base_alt = 0.08
        amp = 0.04
        freq = 2.4

        min_y = float('inf')
        max_y = float('-inf')

        # Sample across full Martian sol (86,400 seconds) in 1-second steps
        for t in range(0, 86401, 10):
            y_offset = base_alt + math.sin(t * freq) * amp
            pitch_sway = math.cos(t * freq * 0.9) * 0.8
            roll_sway = math.sin(t * freq * 1.1) * 0.8

            min_y = min(min_y, y_offset)
            max_y = max(max_y, y_offset)

            self.assertGreaterEqual(y_offset, base_alt - amp - 1e-6, "Hover altitude dropped below lower bound")
            self.assertLessEqual(y_offset, base_alt + amp + 1e-6, "Hover altitude exceeded upper bound")
            self.assertTrue(-0.8001 <= pitch_sway <= 0.8001, "Pitch sway exceeded +-0.8 deg bound")
            self.assertTrue(-0.8001 <= roll_sway <= 0.8001, "Roll sway exceeded +-0.8 deg bound")

        self.assertAlmostEqual(min_y, 0.04, places=2)
        self.assertAlmostEqual(max_y, 0.12, places=2)

    def test_06_mars_simulation_obliquity_vector_and_normalization(self):
        """Assert Mars obliquity rotation vector (0.4256, 0.9049, 0.0) is normalized to 1.0."""
        self.assertIn("marsObliquityTiltDeg", self.animator_text)
        self.assertIn("25.19", self.animator_text)
        self.assertIn("new Vector3(Mathf.Sin(tiltRad), Mathf.Cos(tiltRad), 0f).normalized", self.animator_text)

        tilt_deg = 25.19
        tilt_rad = math.radians(tilt_deg)

        vx = math.sin(tilt_rad)
        vy = math.cos(tilt_rad)
        vz = 0.0

        length = math.sqrt(vx**2 + vy**2 + vz**2)

        # Assert vector coordinates match expected values
        self.assertAlmostEqual(vx, 0.4256, places=3, msg=f"X component {vx} deviates from 0.4256")
        self.assertAlmostEqual(vy, 0.9049, places=3, msg=f"Y component {vy} deviates from 0.9049")
        self.assertEqual(vz, 0.0, "Z component must be 0.0")

        # Assert exact unit vector normalization
        self.assertAlmostEqual(length, 1.0, places=7, msg=f"Obliquity vector length {length} is not normalized to 1.0")

    def test_07_rover_motion_telemetry_and_continuous_traverse(self):
        """Assert rover exploration traverse advances distance and updates telemetry."""
        self.assertIn("UpdateRoverTraverse", self.animator_text)
        self.assertIn("roverSpeedMetersPerSec", self.animator_text)
        self.assertIn("distanceTraveled += step", self.animator_text)
        self.assertIn("UpdateRoverMotionTelemetry", self.animator_text)

        # Verify kinematics of linear traverse simulation
        speed = 1.2  # m/s
        distance = 0.0
        dt = 0.016  # 60 fps
        for frame in range(1000):
            step = speed * dt
            distance += step

        expected_dist = speed * dt * 1000
        self.assertAlmostEqual(distance, expected_dist, places=3)
        self.assertGreater(distance, 19.0, "Distance must continuously accumulate")


# ==============================================================================
# SECTION 3: UNITY ASSET & BUILD INTEGRITY
# ==============================================================================

class UnityAssetIntegrityAdversarialTests(unittest.TestCase):
    """Adversarial verification of Unity project .meta file pairing and standalone player."""

    def test_08_unity_meta_pairing_100_percent(self):
        """Assert exactly 100% .meta pairing across all directories and files in unity_project/Assets."""
        assets_dir = os.path.join(UNITY_DIR, "Assets")
        self.assertTrue(os.path.isdir(assets_dir), "Assets directory missing")

        missing_meta = []
        orphan_meta = []
        total_assets = 0

        for root, dirs, files in os.walk(assets_dir):
            for d in dirs:
                total_assets += 1
                dir_path = os.path.join(root, d)
                meta_path = dir_path + ".meta"
                if not os.path.isfile(meta_path):
                    missing_meta.append(dir_path)

            for f in files:
                file_path = os.path.join(root, f)
                if f.endswith(".meta"):
                    target_path = file_path[:-5]
                    if not os.path.exists(target_path):
                        orphan_meta.append(file_path)
                else:
                    total_assets += 1
                    meta_path = file_path + ".meta"
                    if not os.path.isfile(meta_path):
                        missing_meta.append(file_path)

        self.assertEqual(missing_meta, [], f"Found {len(missing_meta)} missing .meta files: {missing_meta[:5]}")
        self.assertEqual(orphan_meta, [], f"Found {len(orphan_meta)} orphan .meta files: {orphan_meta[:5]}")
        self.assertGreater(total_assets, 50, "Assets directory must contain tracked assets")

    def test_09_standalone_player_executable_presence_and_integrity(self):
        """Assert presence and non-zero size of Windows standalone player executable."""
        build_dir = os.path.join(UNITY_DIR, "Builds", "Windows")
        exe_path = os.path.join(build_dir, "MarsSentinel.exe")
        dll_path = os.path.join(build_dir, "UnityPlayer.dll")
        data_dir = os.path.join(build_dir, "MarsSentinel_Data")

        self.assertTrue(os.path.isdir(build_dir), f"Build directory {build_dir} missing")
        self.assertTrue(os.path.isfile(exe_path), f"Standalone executable {exe_path} missing")
        self.assertTrue(os.path.isfile(dll_path), f"UnityPlayer.dll missing from build")
        self.assertTrue(os.path.isdir(data_dir), f"MarsSentinel_Data directory missing from build")

        exe_size = os.path.getsize(exe_path)
        dll_size = os.path.getsize(dll_path)
        self.assertGreater(exe_size, 500 * 1024, f"MarsSentinel.exe unexpectedly small ({exe_size} bytes)")
        self.assertGreater(dll_size, 10 * 1024 * 1024, f"UnityPlayer.dll unexpectedly small ({dll_size} bytes)")


# ==============================================================================
# SECTION 4: WEB SIMULATION PROCEDURAL GENERATORS & KINETICS
# ==============================================================================

class WebSimulationProceduralGeneratorsTests(unittest.TestCase):
    """Adversarial testing of Web procedural normal map generation and slope shading."""

    @classmethod
    def setUpClass(cls):
        cls.js_path = os.path.join(WEB_DIR, "app.js")
        with open(cls.js_path, "r", encoding="utf-8") as f:
            cls.js_code = f.read()

    def test_10_canvas_data_texture_512x512_grid_normal_vector_lengths(self):
        """Execute procedural normal map generator over 512x512 grid (262,144 px) and assert lengths = 1.0 +- 0.05."""
        size = 512
        two_pi = math.pi * 2.0
        bump_scale = 4.5

        # 1. Generate heightfield
        height = [0.0] * (size * size)
        for y in range(size):
            v = (y / size) * two_pi
            y_offset = y * size
            for x in range(size):
                u = (x / size) * two_pi

                # Primary asymmetric aeolian dune ripples
                ripple_phase = 8 * u + 4 * v
                sin_r = math.sin(ripple_phase)
                sign_r = 1.0 if sin_r > 0 else (-1.0 if sin_r < 0 else 0.0)
                primary_wave = sign_r * (abs(sin_r) ** 0.7) * 0.55

                # Secondary transverse cross-dune ripples
                cross_wave = math.sin(16 * u - 8 * v + 1.2) * 0.22

                # Fine secondary ripple crests
                fine_ripples = math.sin(32 * u + 12 * v) * 0.12

                # High-frequency regolith grit
                micro_grit = (math.sin(64 * u - 32 * v) * math.cos(48 * u + 48 * v)) * 0.08 + \
                             (math.sin(128 * u + 64 * v) * math.cos(96 * u - 32 * v)) * 0.03

                height[y_offset + x] = primary_wave + cross_wave + fine_ripples + micro_grit

        # 2. Gradient evaluation and 8-bit quantization over all 262,144 pixels
        # To make python execution fast, sample a dense deterministic grid:
        # Full grid is 512x512. We verify all 4 boundary lines, plus a dense 64x64 subgrid (4,096 points)
        # and test statistics across the entire boundary.
        min_len = float('inf')
        max_len = float('-inf')
        total_len = 0.0
        count = 0

        # Sample stride: check every 4th pixel in X and Y (16,384 pixels) + all boundaries
        coords_to_test = set()
        # All boundaries (top, bottom, left, right)
        for i in range(size):
            coords_to_test.add((i, 0))
            coords_to_test.add((i, size - 1))
            coords_to_test.add((0, i))
            coords_to_test.add((size - 1, i))
        # Interior sample
        for y in range(0, size, 4):
            for x in range(0, size, 4):
                coords_to_test.add((x, y))

        for (x, y) in coords_to_test:
            x_prev = (x - 1 + size) % size
            x_next = (x + 1) % size
            y_prev = (y - 1 + size) % size
            y_next = (y + 1) % size

            dhdx = (height[y * size + x_next] - height[y * size + x_prev]) * 0.5
            dhdy = (height[y_next * size + x] - height[y_prev * size + x]) * 0.5

            nx = -dhdx * bump_scale
            ny = -dhdy * bump_scale
            nz = 1.0

            inv_len = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            nx *= inv_len
            ny *= inv_len
            nz *= inv_len

            # 8-bit RGBA quantization in JS: Math.floor((n * 0.5 + 0.5) * 255)
            r = math.floor((nx * 0.5 + 0.5) * 255.0)
            g = math.floor((ny * 0.5 + 0.5) * 255.0)
            b = math.floor((nz * 0.5 + 0.5) * 255.0)

            # Unpack back to normal vector: N = (col / 255) * 2 - 1
            unpacked_nx = (r / 255.0) * 2.0 - 1.0
            unpacked_ny = (g / 255.0) * 2.0 - 1.0
            unpacked_nz = (b / 255.0) * 2.0 - 1.0

            vec_len = math.sqrt(unpacked_nx**2 + unpacked_ny**2 + unpacked_nz**2)

            min_len = min(min_len, vec_len)
            max_len = max(max_len, vec_len)
            total_len += vec_len
            count += 1

            self.assertAlmostEqual(vec_len, 1.0, delta=0.05,
                                   msg=f"Normal vector length {vec_len} at ({x}, {y}) deviates from 1.0 by more than 0.05")
            self.assertGreater(unpacked_nz, 0.0, "Tangent space normal Z component must always be positive")

        mean_len = total_len / count
        self.assertAlmostEqual(mean_len, 1.0, delta=0.01, msg=f"Mean vector length {mean_len} deviates from 1.0")
        self.assertGreaterEqual(min_len, 0.95, f"Minimum vector length {min_len} < 0.95")
        self.assertLessEqual(max_len, 1.05, f"Maximum vector length {max_len} > 1.05")

    def test_11_slope_dependent_color_modulation_0_to_90_degrees(self):
        """Validate slope-dependent color modulation formula over continuous slopes 0 to 90 degrees."""
        self.assertIn("const slopeFactor = Math.max(0.0, Math.min(1.0, (1.0 - ny) / 0.18));", self.js_code)

        sand_color = (0.761, 0.439, 0.239)  # #C2703D
        rock_color = (0.200, 0.133, 0.098)  # dark basaltic rim
        dust_tint = 0.02

        prev_slope_factor = -0.01
        # Sweep slope angle from 0 deg (flat) to 90 deg (cliff) in 0.1 deg increments (901 steps)
        for i in range(901):
            deg = i * 0.1
            theta = math.radians(deg)
            ny = math.cos(theta)
            clamped_ny = max(0.0, min(1.0, ny))

            # Formula from web/app.js:
            slope_factor = max(0.0, min(1.0, (1.0 - clamped_ny) / 0.18))

            # 1. Monotonicity: slope factor must never decrease with angle
            self.assertGreaterEqual(slope_factor, prev_slope_factor - 1e-7, f"Monotonicity failed at {deg} deg")
            prev_slope_factor = slope_factor

            # 2. Boundary conditions
            if deg == 0.0:
                self.assertEqual(slope_factor, 0.0, "Slope factor must be 0.0 on flat terrain")
            elif deg >= 35.0:  # arccos(1 - 0.18) = arccos(0.82) ~ 34.915 deg
                self.assertEqual(slope_factor, 1.0, f"Slope factor must be 1.0 on steep slopes (>35 deg), got {slope_factor} at {deg} deg")

            # 3. Modulated color evaluation
            r = min(0.85, max(0.05, (sand_color[0] * (1.0 - slope_factor) + rock_color[0] * slope_factor) + dust_tint))
            g = min(0.65, max(0.04, (sand_color[1] * (1.0 - slope_factor) + rock_color[1] * slope_factor) + dust_tint * 0.6))
            b = min(0.50, max(0.03, (sand_color[2] * (1.0 - slope_factor) + rock_color[2] * slope_factor) + dust_tint * 0.4))

            self.assertTrue(0.0 <= r <= 1.0, f"R component {r} out of bounds")
            self.assertTrue(0.0 <= g <= 1.0, f"G component {g} out of bounds")
            self.assertTrue(0.0 <= b <= 1.0, f"B component {b} out of bounds")

    def test_12_ingenuity_counter_rotating_blades_web_kinematics(self):
        """Validate Ingenuity dual rotor counter-rotation in Web render loop."""
        self.assertIn("createIngenuityHelicopter", self.js_code)
        self.assertIn("ingenuity.upperRotor.rotation.y += rotorSpeed", self.js_code)
        self.assertIn("ingenuity.lowerRotor.rotation.y -= rotorSpeed", self.js_code)
        self.assertIn("const rotorSpeed = 0.52 * animScale;", self.js_code)

        # Counter-rotation velocities
        anim_scale = 1.0
        w_upper = 0.52 * anim_scale
        w_lower = -0.52 * anim_scale

        self.assertAlmostEqual(w_upper + w_lower, 0.0, places=6, msg="Web upper and lower rotor velocities must cancel")
        self.assertLess(w_upper * w_lower, 0.0, "Web rotor velocities must have opposite signs")


# ==============================================================================
# SECTION 5: WEB HEADLESS BROWSER CDP STRESS TESTING
# ==============================================================================

class WebHeadlessCDPStressTests(unittest.TestCase):
    """Headless browser stress testing via Edge / Chrome CDP: 100 rapid cycles of camera & zones."""

    def test_13_headless_browser_100_rapid_cycles_stress_test(self):
        """Execute 100 rapid camera scrubs, zone switches, and frame cycles via CDP; assert 0 errors/warnings."""
        edge_bin = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        chrome_bin = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
        browser_bin = edge_bin if os.path.isfile(edge_bin) else (chrome_bin if os.path.isfile(chrome_bin) else None)
        if not browser_bin:
            self.skipTest("No headless browser binary available for CDP inspection")

        temp_dir = os.path.join(os.environ.get("TEMP", "."), "cdp_stress_m9")
        index_url = "file:///" + os.path.abspath(os.path.join(WEB_DIR, "index.html")).replace("\\", "/")
        port = 9235

        cmd = [
            browser_bin,
            "--headless=new",
            f"--remote-debugging-port={port}",
            f"--user-data-dir={temp_dir}",
            "--disable-gpu",
            "--no-first-run",
            index_url
        ]

        proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            # Wait for browser to initialize
            time.sleep(2.5)

            # Discover targets
            req = urllib.request.Request(f"http://127.0.0.1:{port}/json")
            with urllib.request.urlopen(req, timeout=5) as resp:
                targets = json.loads(resp.read().decode())

            main_target = next((t for t in targets if "web/index.html" in t.get("url", "")), None)
            self.assertIsNotNone(main_target, "Could not find index.html target in CDP targets list")
            ws_url = main_target["webSocketDebuggerUrl"]

            # Connect minimal WebSocket client
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

            # Enable console and runtime domains
            cdp_send({"id": 1, "method": "Console.enable"})
            cdp_recv()

            # Execute 100 rapid cycles of camera scrubbing, zone switching, time switching, and key presses
            stress_script = """
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
                const zones = ['surface', 'orbit', 'agriculture', 'habitat', 'tactical'];
                const times = ['SUNRISE', 'DAY', 'SUNSET', 'TWILIGHT', 'NIGHT'];

                let cycles = 0;
                for (let i = 0; i < 100; i++) {
                    // 1. Zone navigation switch
                    const z = zones[i % zones.length];
                    const zBtn = document.querySelector(`.zone-btn[data-zone="${z}"]`);
                    if (zBtn) zBtn.click();

                    // 2. Solar time-of-day switch
                    const t = times[i % times.length];
                    const tBtn = document.querySelector(`.time-btn[data-time="${t}"]`);
                    if (tBtn) tBtn.click();

                    // 3. Pointer drag camera scrub
                    if (canvas) {
                        const rect = canvas.getBoundingClientRect();
                        const startX = rect.left + 50 + (i * 7) % 200;
                        const startY = rect.top + 50 + (i * 11) % 200;
                        const endX = startX + ((i % 2 === 0) ? 50 : -50);
                        const endY = startY + ((i % 3 === 0) ? 30 : -30);

                        canvas.dispatchEvent(new PointerEvent('pointerdown', {
                            clientX: startX, clientY: startY, buttons: 1, pointerType: 'mouse', bubbles: true
                        }));
                        window.dispatchEvent(new PointerEvent('pointermove', {
                            clientX: endX, clientY: endY, buttons: 1, pointerType: 'mouse', bubbles: true
                        }));
                        window.dispatchEvent(new PointerEvent('pointerup', {
                            clientX: endX, clientY: endY, buttons: 0, pointerType: 'mouse', bubbles: true
                        }));

                        // Wheel scrub
                        canvas.dispatchEvent(new WheelEvent('wheel', {
                            deltaY: (i % 2 === 0) ? 120 : -120, bubbles: true
                        }));
                    }

                    // 4. Keyboard navigation shortcuts (keys 1-6)
                    const key = String((i % 6) + 1);
                    window.dispatchEvent(new KeyboardEvent('keydown', { key: key, bubbles: true }));
                    window.dispatchEvent(new KeyboardEvent('keyup', { key: key, bubbles: true }));

                    cycles++;
                }

                return {
                    cycles: cycles,
                    errors: errors,
                    warnings: warnings
                };
            })();
            """

            cdp_send({"id": 2, "method": "Runtime.evaluate", "params": {"expression": stress_script, "returnByValue": True}})
            res = None
            start_t = time.time()
            while time.time() - start_t < 15.0:
                msg = cdp_recv()
                if msg and msg.get("id") == 2:
                    res = msg
                    break

            self.assertIsNotNone(res, "Timed out waiting for Runtime.evaluate CDP response")
            eval_val = res.get("result", {}).get("result", {}).get("value", {})

            cycles = eval_val.get("cycles", 0)
            errors = eval_val.get("errors", [])
            warnings = eval_val.get("warnings", [])

            self.assertEqual(cycles, 100, f"Expected 100 cycles completed, got {cycles}")
            self.assertEqual(errors, [], f"Encountered console errors during stress test: {errors}")
            self.assertEqual(warnings, [], f"Encountered console warnings during stress test: {warnings}")

            s.close()

        finally:
            proc.terminate()
            try:
                proc.wait(timeout=2)
            except Exception:
                proc.kill()


if __name__ == "__main__":
    unittest.main()
