"""
Adversarial Stress Test Suite for PNG Validation (Milestone 7 Iteration 2).

Challenger: Challenger M7-3 (teamwork_challenger)
Target: scripts.audit_workspace_integrity.validate_png_file
"""

import os
import sys
import tempfile
import unittest
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from scripts.audit_workspace_integrity import validate_png_file, validate_jpeg_file


class AdversarialPngValidationTests(unittest.TestCase):
    """Stress tests and adversarial attack vectors for validate_png_file."""

    def setUp(self):
        self.tmpdir_obj = tempfile.TemporaryDirectory()
        self.tmpdir = self.tmpdir_obj.name

    def tearDown(self):
        self.tmpdir_obj.cleanup()

    def _write_temp_png(self, name: str, data: bytes) -> str:
        path = os.path.join(self.tmpdir, name)
        with open(path, "wb") as f:
            f.write(data)
        return path

    def test_adv_01_empty_file(self):
        """Empty 0-byte file must raise ValueError."""
        p = self._write_temp_png("empty.png", b"")
        with self.assertRaises(ValueError) as cm:
            validate_png_file(p)
        self.assertIn("too small", str(cm.exception).lower())

    def test_adv_02_sub_header_truncation(self):
        """Files between 1 and 7 bytes must raise ValueError."""
        header = b"\x89PNG\r\n\x1a\n"
        for length in range(1, 8):
            p = self._write_temp_png(f"trunc_{length}.png", header[:length])
            with self.assertRaises(ValueError) as cm:
                validate_png_file(p)
            self.assertIn("too small", str(cm.exception).lower())

    def test_adv_03_corrupted_magic_signatures(self):
        """Files with wrong magic (JPEG, GIF, BMP, or mutated PNG header) must raise ValueError."""
        bad_signatures = [
            b"GIF89a\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00",
            b"\xff\xd8\xff\xe0\x00\x10JFIF\x00\x01\x01\x00\x00\x01\x00\x01\x00\x00",
            b"BM\x00\x00\x00\x00\x00\x00\x00\x00\x36\x00\x00\x00\x28\x00\x00\x00",
            b"\x88PNG\r\n\x1a\n" + b"\x00" * 12,
            b"\x89QNG\r\n\x1a\n" + b"\x00" * 12,
            b"\x89PNG\n\r\x1a\n" + b"\x00" * 12,
            b"\x89PNG\r\n\x1a\x00" + b"\x00" * 12,
        ]
        for idx, sig in enumerate(bad_signatures):
            p = self._write_temp_png(f"bad_sig_{idx}.png", sig)
            with self.assertRaises(ValueError) as cm:
                validate_png_file(p)
            self.assertIn("invalid png signature", str(cm.exception).lower())

    def test_adv_04_header_only_no_chunks(self):
        """File with exactly 8-byte valid PNG signature and 0 chunks must be rejected."""
        p = self._write_temp_png("header_only.png", b"\x89PNG\r\n\x1a\n")
        with self.assertRaises(ValueError) as cm:
            validate_png_file(p)
        self.assertIn("missing iend", str(cm.exception).lower())

    def test_adv_05_payload_under_20_bytes_without_complete_iend(self):
        """Files between 9 and 19 bytes without complete 12-byte IEND chunk must be rejected."""
        for length in range(9, 20):
            data = b"\x89PNG\r\n\x1a\n" + b"\x00" * (length - 8)
            p = self._write_temp_png(f"short_{length}.png", data)
            with self.assertRaises(ValueError):
                validate_png_file(p)

    def test_adv_06_unanchored_iend_substring_attack(self):
        """Files with IEND embedded as substring in data but missing terminal chunk must be rejected."""
        attack_payloads = [
            # The exact string from Challenger M7-2
            b"\x89PNG\r\n\x1a\n" + b"TRUNCATEDDATAWITHOUTIENDCHUNK",
            # IEND padded with letters
            b"\x89PNG\r\n\x1a\n" + b"AAAAIEND_NOT_A_CHUNK_BBBB",
            # IEND at beginning of tail but non-zero length prefix
            b"\x89PNG\r\n\x1a\n" + b"PADDING" + b"\x00\x00\x00\x04IEND\x00\x00\x00\x00",
            # Zero prefix with lower case iend
            b"\x89PNG\r\n\x1a\n" + b"PADDING" + b"\x00\x00\x00\x00iend\xaeB\x60\x82",
            # Zero prefix with misspelled IEN_
            b"\x89PNG\r\n\x1a\n" + b"PADDING" + b"\x00\x00\x00\x00IEN_\xaeB\x60\x82",
        ]
        for idx, payload in enumerate(attack_payloads):
            p = self._write_temp_png(f"unanchored_{idx}.png", payload)
            with self.assertRaises(ValueError) as cm:
                validate_png_file(p)
            self.assertIn("missing iend", str(cm.exception).lower())

    def test_adv_07_trailing_garbage_after_iend(self):
        """Valid PNG chunk sequence followed by trailing garbage bytes must be rejected."""
        valid_minimal_png = (
            b"\x89PNG\r\n\x1a\n"
            b"\x00\x00\x00\x00IEND\xaeB\x60\x82"
        )
        garbage_suffixes = [
            b"\x00",
            b"\x00\x00\x00\x00",
            b"EXTRA_TRAILING_DATA_CORRUPTION",
            b"\xff" * 16,
        ]
        for idx, suffix in enumerate(garbage_suffixes):
            p = self._write_temp_png(f"trailing_{idx}.png", valid_minimal_png + suffix)
            with self.assertRaises(ValueError) as cm:
                validate_png_file(p)
            self.assertIn("missing iend", str(cm.exception).lower())

    def test_adv_08_valid_minimal_and_synthetic_png(self):
        """Valid minimal PNG (header + IEND) and synthetic 1x1 PNG must pass cleanly."""
        minimal_png = (
            b"\x89PNG\r\n\x1a\n"
            b"\x00\x00\x00\x00IEND\xaeB\x60\x82"
        )
        p1 = self._write_temp_png("minimal.png", minimal_png)
        result1 = validate_png_file(p1)
        self.assertTrue(result1.startswith("Valid PNG"))

        # Synthetic 1x1 8-bit RGBA PNG
        # Header (8) + IHDR (25) + IDAT (21) + IEND (12)
        ihdr = b"\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15c4"
        idat = b"\x00\x00\x00\x0bIDAT\x78\x9cc\xf8\x0f\x00\x01\x01\x01\x00\x18\xdd\x8d\xb0"
        iend = b"\x00\x00\x00\x00IEND\xaeB\x60\x82"
        full_png = b"\x89PNG\r\n\x1a\n" + ihdr + idat + iend
        p2 = self._write_temp_png("synthetic_1x1.png", full_png)
        result2 = validate_png_file(p2)
        self.assertTrue(result2.startswith("Valid PNG"))

    def test_adv_09_actual_workspace_pngs_pass(self):
        """All genuine PNG assets under data/ and previews/ must pass validate_png_file."""
        target_dirs = [PROJECT_ROOT / "data", PROJECT_ROOT / "previews"]
        png_files = []
        for target_dir in target_dirs:
            for root, _, files in os.walk(target_dir):
                for f in files:
                    if f.lower().endswith(".png"):
                        png_files.append(os.path.join(root, f))

        self.assertGreaterEqual(len(png_files), 15, "Expected at least 15 dataset/preview PNG files")
        for png_path in png_files:
            rel = os.path.relpath(png_path, PROJECT_ROOT)
            with self.subTest(file=rel):
                result = validate_png_file(png_path)
                self.assertTrue(result.startswith("Valid PNG"), f"Validation failed for {rel}: {result}")

    def test_adv_10_truncating_actual_workspace_pngs(self):
        """Truncating genuine workspace PNG files by 1, 4, 8, 12, or 64 bytes must cause rejection."""
        sample_png = PROJECT_ROOT / "data" / "starfield_globe_camera_crop.png"
        self.assertTrue(sample_png.is_file(), f"Expected {sample_png} to exist")

        with open(sample_png, "rb") as f:
            original_bytes = f.read()

        for cut in [1, 4, 8, 12, 64]:
            truncated_bytes = original_bytes[:-cut]
            p = self._write_temp_png(f"real_trunc_{cut}.png", truncated_bytes)
            with self.assertRaises(ValueError, msg=f"Failed to reject real PNG truncated by {cut} bytes"):
                validate_png_file(p)

    def test_adv_11_misnamed_jpeg_in_unity_template_rejection(self):
        """Verify that URP.png (which is actually JPEG data) is rejected by validate_png_file."""
        urp_icon = PROJECT_ROOT / "unity_project" / "Assets" / "TutorialInfo" / "Icons" / "URP.png"
        if urp_icon.is_file():
            # validate_png_file MUST raise ValueError because it's a JPEG
            with self.assertRaises(ValueError) as cm:
                validate_png_file(str(urp_icon))
            self.assertIn("invalid png signature", str(cm.exception).lower())
            # validate_jpeg_file MUST accept it
            jpeg_result = validate_jpeg_file(str(urp_icon))
            self.assertTrue(jpeg_result.startswith("Valid JPEG"))


if __name__ == "__main__":
    unittest.main()
