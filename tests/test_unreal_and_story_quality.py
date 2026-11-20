import json
import os
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, "unity_project")
UNREAL = os.path.join(ROOT, "unreal_project")
WEB = os.path.join(ROOT, "web")


def read_text(*parts):
    with open(os.path.join(*parts), "r", encoding="utf-8") as f:
        return f.read()


class UnrealAndStoryQualityTests(unittest.TestCase):
    def test_01_unreal_project_manifest_and_configs_valid(self):
        uproj_path = os.path.join(UNREAL, "MarsSentinelUE.uproject")
        self.assertTrue(os.path.isfile(uproj_path), "MarsSentinelUE.uproject missing")
        uproj = json.loads(read_text(uproj_path))
        self.assertEqual(uproj.get("EngineAssociation"), "5.8")
        plugin_names = {p.get("Name") for p in uproj.get("Plugins", []) if p.get("Enabled")}
        for req in ("PythonScriptPlugin", "EditorScriptingUtilities", "EnhancedInput", "Niagara", "Interchange"):
            self.assertIn(req, plugin_names, f"UE5 plugin missing: {req}")

        eng_ini = read_text(UNREAL, "Config", "DefaultEngine.ini")
        self.assertIn("DefaultGravityZ=-372.076", eng_ini)
        self.assertIn("r.DynamicGlobalIlluminationMethod=1", eng_ini)
        self.assertIn("r.Nanite=1", eng_ini)
        self.assertIn("JezeroCrater_Hero", eng_ini)

    def test_02_unreal_cpp_source_and_python_builder_present(self):
        for rel in (
            ("Source", "MarsSentinelUE", "MarsAstronautCharacter.h"),
            ("Source", "MarsSentinelUE", "MarsAstronautCharacter.cpp"),
            ("Source", "MarsSentinelUE", "MarsCtxNavSubsystem.h"),
            ("Source", "MarsSentinelUE", "MarsCtxNavSubsystem.cpp"),
            ("Scripts", "build_jezero_ue5_terrain_glb.py"),
            ("Scripts", "build_mars_sentinel_ue5.py"),
            ("README.md",),
        ):
            p = os.path.join(UNREAL, *rel)
            self.assertTrue(os.path.isfile(p), f"Missing UE5 file: {p}")

        astro_cpp = read_text(UNREAL, "Source", "MarsSentinelUE", "MarsAstronautCharacter.cpp")
        self.assertIn("ComputeBeerLambertTransmission", astro_cpp)
        self.assertIn("Ch5_SampleReturnIgnition", astro_cpp)

        builder_py = read_text(UNREAL, "Scripts", "build_mars_sentinel_ue5.py")
        self.assertIn("JezeroCrater_Hero", builder_py)
        self.assertIn("SkyAtmosphere", builder_py)

    def test_03_unity_baked_terrain_and_story_director(self):
        terrain_asset = os.path.join(UNITY, "Assets", "Models", "Terrain", "JezeroHorizonTerrain.asset")
        self.assertTrue(os.path.isfile(terrain_asset), "Baked JezeroHorizonTerrain.asset missing")

        story_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsStoryDirector.cs")
        for sym in (
            "Ch1_DustfallArrival",
            "Ch2_CheyavaBiosignature",
            "Ch3_SolarFlareTriage",
            "Ch4_BelvaRelayAndMeteorite",
            "Ch5_SampleReturnIgnition",
            "TriggerRocketLaunchSequence",
        ):
            self.assertIn(sym, story_cs)

        shell_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsGameShellUI.cs")
        self.assertIn("MarsStoryDirector.Instance?.BeginCampaign()", shell_cs)

    def test_04_web_modules_organized_and_story_mode_wired(self):
        for mod in ("mars_realism_upgrade.js", "mars_earth_3d_upgrade.js", "mars_story_mode.js"):
            self.assertTrue(os.path.isfile(os.path.join(WEB, "modules", mod)), f"Missing web/modules/{mod}")
        html = read_text(WEB, "index.html")
        self.assertIn("modules/mars_realism_upgrade.js", html)
        self.assertIn("modules/mars_earth_3d_upgrade.js", html)
        self.assertIn("modules/mars_story_mode.js", html)

    def test_05_duplicate_pngs_removed_and_showcase_gallery_valid(self):
        # Verify stale presentation/shots/ and duplicate Fleet thumbnail PNGs are completely removed
        self.assertFalse(os.path.exists(os.path.join(ROOT, "presentation", "shots")), "Stale presentation/shots/ should be removed")
        fleet_dir = os.path.join(UNITY, "Assets", "Models", "Fleet")
        fleet_pngs = [f for f in os.listdir(fleet_dir) if f.endswith(".png")]
        self.assertEqual(fleet_pngs, [], f"Duplicate thumbnail PNGs still in Fleet/: {fleet_pngs}")

        # Verify curated live ground-truth showcase gallery images exist for EVERY module with valid PNG headers
        showcase_dir = os.path.join(ROOT, "showcase")
        self.assertTrue(os.path.isdir(showcase_dir), "showcase/ directory missing")
        required = (
            "hero_mars_planet_and_sun_orbit.png",
            "blender_3d_night_starfield.png",
            "blender_3d_day_closeup.png",
            "blender_viewport_night_moons_simulation.png",
            "blender_viewport_mars_sun_orbit_simulation.png",
            "web_hero_glbs.png",
            "nav_module_ctx_rf.png",
            "web_story_3d_hazard.png",
            "web_arabic_rtl.png",
            "unity_game_live.png",
            "unity_game_xray_story.png",
            "unreal_ue5_jezero_lumen.png",
        )
        for f in required:
            p = os.path.join(showcase_dir, f)
            self.assertTrue(os.path.isfile(p), f"Missing live module screenshot: showcase/{f}")
            with open(p, "rb") as fh:
                self.assertEqual(fh.read(8), b"\x89PNG\r\n\x1a\n", f"Invalid PNG header: {f}")

        readme = read_text(ROOT, "README.md")
        self.assertIn("showcase/", readme)
        self.assertIn("unreal_project/", readme)


if __name__ == "__main__":
    unittest.main()
