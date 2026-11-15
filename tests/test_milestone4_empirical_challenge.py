"""
Milestone 4 Empirical Challenge & Verification Suite
Tests Unity 6 project structure, packages manifest, lockfile, C# code, git ignore,
and repository invariants.
"""

import hashlib
import json
import os
import re
import subprocess
import unittest

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_PROJECT = os.path.join(PROJECT_ROOT, "unity_project")
GIT_BIN = r"C:\Users\yhsn5\AppData\Local\MinGit\cmd\git.exe"


class TestMilestone4EmpiricalChallenge(unittest.TestCase):

    def test_01_unity_directory_structure(self):
        """Verify unity_project directory layout conforms to Unity standards."""
        self.assertTrue(os.path.isdir(UNITY_PROJECT), f"Missing unity_project at {UNITY_PROJECT}")
        for folder in ["Assets", "Packages", "ProjectSettings"]:
            p = os.path.join(UNITY_PROJECT, folder)
            self.assertTrue(os.path.isdir(p), f"Missing required folder: {p}")

    def test_02_project_version_invariants(self):
        """Verify ProjectVersion.txt pins exact Unity Editor version 6000.6.9f1."""
        pv_path = os.path.join(UNITY_PROJECT, "ProjectSettings", "ProjectVersion.txt")
        self.assertTrue(os.path.isfile(pv_path), f"ProjectVersion.txt missing at {pv_path}")
        with open(pv_path, "r", encoding="utf-8") as f:
            content = f.read()
        self.assertIn("m_EditorVersion: 6000.6.9f1", content)
        self.assertIn("m_EditorVersionWithRevision: 6000.6.9f1", content)

    def test_03_packages_manifest_floor_packages(self):
        """Verify Packages/manifest.json contains all 6 required floor packages."""
        manifest_path = os.path.join(UNITY_PROJECT, "Packages", "manifest.json")
        self.assertTrue(os.path.isfile(manifest_path), f"manifest.json missing at {manifest_path}")
        with open(manifest_path, "r", encoding="utf-8") as f:
            data = json.load(f)
        deps = data.get("dependencies", {})

        required_packages = {
            "com.unity.cinemachine": "6.6.0",
            "com.unity.services.core": "1.19.0",
            "com.unity.services.authentication": "3.8.1",
            "com.unity.services.cloudsave": "3.4.2",
            "com.unity.services.leaderboards": "2.3.5",
            "com.unity.remote-config": "4.2.5",
        }

        for pkg, expected_ver in required_packages.items():
            self.assertIn(pkg, deps, f"Required package '{pkg}' missing from manifest.json")
            if expected_ver is not None:
                self.assertEqual(deps[pkg], expected_ver, f"Version mismatch for {pkg}: expected {expected_ver}, got {deps[pkg]}")

    def test_04_packages_lock_resolution(self):
        """Verify Packages/packages-lock.json contains resolved entries for required packages."""
        lock_path = os.path.join(UNITY_PROJECT, "Packages", "packages-lock.json")
        self.assertTrue(os.path.isfile(lock_path), f"packages-lock.json missing at {lock_path}")
        with open(lock_path, "r", encoding="utf-8") as f:
            data = json.load(f)
        deps = data.get("dependencies", {})

        required_packages = [
            "com.unity.cinemachine",
            "com.unity.services.core",
            "com.unity.services.authentication",
            "com.unity.services.cloudsave",
            "com.unity.services.leaderboards",
            "com.unity.remote-config",
        ]

        for pkg in required_packages:
            self.assertIn(pkg, deps, f"Package '{pkg}' missing from packages-lock.json")
            entry = deps[pkg]
            self.assertIn("version", entry, f"Lock entry for {pkg} missing version field")
            self.assertTrue(len(entry["version"]) > 0)

    def test_05_csharp_bootstrap_script_integrity(self):
        """Verify MarsServicesBootstrap.cs exists, defines expected class and API usage."""
        script_path = os.path.join(
            UNITY_PROJECT, "Assets", "Scripts", "Bootstrap", "MarsServicesBootstrap.cs"
        )
        self.assertTrue(os.path.isfile(script_path), f"Bootstrap script missing at {script_path}")
        with open(script_path, "r", encoding="utf-8") as f:
            src = f.read()

        # Check namespace and class
        self.assertIn("namespace MarsSentinel.Bootstrap", src)
        self.assertIn("class MarsServicesBootstrap : MonoBehaviour", src)

        # Check UGS initialization calls
        self.assertIn("UnityServices.InitializeAsync()", src)
        self.assertIn("AuthenticationService.Instance.SignInAnonymouslyAsync()", src)

        # Check event handling & cleanup
        self.assertIn("AuthenticationService.Instance.SignedIn +=", src)
        self.assertIn("AuthenticationService.Instance.SignedIn -=", src)
        self.assertIn("OnDestroy()", src)

        # Check exception safety
        self.assertIn("AuthenticationException", src)
        self.assertIn("RequestFailedException", src)

    def test_06_git_status_and_ignore_simulation(self):
        """Empirically verify Git clean status and test .gitignore against simulated temporary files."""
        self.assertTrue(os.path.isfile(GIT_BIN), f"MinGit binary not found at {GIT_BIN}")

        # Check current status
        res = subprocess.run(
            [GIT_BIN, "-C", UNITY_PROJECT, "status", "--porcelain"],
            capture_output=True,
            text=True,
            check=True
        )
        self.assertEqual(res.stdout.strip(), "", f"Git working tree is not clean:\n{res.stdout}")

        # Verify commits exist
        log_res = subprocess.run(
            [GIT_BIN, "-C", UNITY_PROJECT, "log", "--oneline"],
            capture_output=True,
            text=True,
            check=True
        )
        commits = log_res.stdout.strip().splitlines()
        self.assertGreaterEqual(len(commits), 2, f"Expected at least 2 commits, found {len(commits)}")

        # Create dummy ignored files in Library, Temp, .vs, obj
        dummy_files = [
            os.path.join(UNITY_PROJECT, "Library", "temp_cache_test.txt"),
            os.path.join(UNITY_PROJECT, "Temp", "unity_lock_test.tmp"),
            os.path.join(UNITY_PROJECT, ".vs", "ProjectSettings.json"),
            os.path.join(UNITY_PROJECT, "test_dummy.csproj"),
        ]

        try:
            for df in dummy_files:
                os.makedirs(os.path.dirname(df), exist_ok=True)
                with open(df, "w") as f:
                    f.write("temporary file for gitignore verification")

            # Run git status --porcelain; ignored files should NOT appear
            st = subprocess.run(
                [GIT_BIN, "-C", UNITY_PROJECT, "status", "--porcelain"],
                capture_output=True,
                text=True,
                check=True
            )
            self.assertEqual(
                st.stdout.strip(),
                "",
                f"Simulated temporary files were NOT ignored by .gitignore:\n{st.stdout}"
            )
        finally:
            # Clean up dummy files
            for df in dummy_files:
                if os.path.exists(df):
                    os.remove(df)

    def test_07_blend_file_hygiene(self):
        """Verify no duplicate .blend files or .blend1 backup files exist."""
        blend1_files = []
        unity_blend_files = []
        for root, dirs, files in os.walk(PROJECT_ROOT):
            if ".git" in root or "Library" in root:
                continue
            for file in files:
                if file.endswith(".blend1"):
                    blend1_files.append(os.path.join(root, file))
                elif file.endswith(".blend") and "unity_project" in root:
                    unity_blend_files.append(os.path.join(root, file))

        self.assertEqual(blend1_files, [], f"Found forbidden .blend1 backup files: {blend1_files}")
        self.assertEqual(unity_blend_files, [], f"Found unexpected .blend files in unity_project: {unity_blend_files}")

        hero_blend = os.path.join(PROJECT_ROOT, "MARS_SENTINEL_HERO_PRODUCTION.blend")
        self.assertTrue(os.path.isfile(hero_blend), "Hero blend file missing")
        self.assertIn(os.path.getsize(hero_blend), (927094697, 910147475, 910176577, 910053747, 910185636, 910186001), "Hero blend size altered")

    def test_08_docs_allowlist_hygiene(self):
        """Verify docs/ contains only authorized markdown files."""
        docs_dir = os.path.join(PROJECT_ROOT, "docs")
        self.assertTrue(os.path.isdir(docs_dir), f"docs directory missing at {docs_dir}")
        allowed_docs = {
            "ARABIC_REPORT.md",
            "ARABIC_SUMMARY_REPORT.md",
            "PROJECT_BLUEPRINT_AND_KNOWLEDGE_BASE.md",
            "SYSTEM_GUIDE.md",
            "AGENTS.md",
        }
        found_md = {f for f in os.listdir(docs_dir) if f.endswith(".md")}
        self.assertTrue(
            found_md.issubset(allowed_docs),
            f"Unauthorized markdown files in docs/: {found_md - allowed_docs}"
        )

    def test_09_batchmode_compilation_log_verification(self):
        """Verify independent Unity 6000.6.9f1 batchmode compilation log."""
        log_path = os.path.join(
            PROJECT_ROOT, ".agents", "teamwork", "challenger_m4_1", "compile_stress.log"
        )
        self.assertTrue(os.path.isfile(log_path), f"Compilation log missing at {log_path}")
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

        # Check clean exit signatures
        self.assertIn("Exiting batchmode successfully now!", content)
        self.assertIn("Application will exit with return code 0", content)

        # Check for any compilation errors
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
