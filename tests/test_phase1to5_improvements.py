import json
import os
import struct
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, "unity_project")
WEB = os.path.join(ROOT, "web")


def read_text(*parts):
    with open(os.path.join(*parts), "r", encoding="utf-8") as f:
        return f.read()


def read_glb_json(path):
    with open(path, "rb") as f:
        b = f.read()
    assert b[:4] == b"glTF", f"Invalid glTF magic in {path}"
    jlen, jtype = struct.unpack("<II", b[12:20])
    assert jtype == 0x4E4F534A, f"Missing JSON chunk in {path}"
    return json.loads(b[20:20 + jlen].decode("utf-8")), len(b)


class Phase1To5ImprovementTests(unittest.TestCase):
    def test_01_all_10_unity_fleet_glbs_decompressed_and_valid(self):
        fleet_dir = os.path.join(UNITY, "Assets", "Models", "Fleet")
        glbs = sorted(f for f in os.listdir(fleet_dir) if f.endswith(".glb"))
        self.assertGreaterEqual(len(glbs), 10, f"Expected >=10 fleet GLBs, found {len(glbs)}")
        for name in glbs:
            jdata, size = read_glb_json(os.path.join(fleet_dir, name))
            self.assertGreater(size, 50_000, f"Fleet GLB too small: {name}")
            req = jdata.get("extensionsRequired", [])
            self.assertNotIn("KHR_draco_mesh_compression", req, f"Draco still required in {name}")
            self.assertNotIn("EXT_texture_webp", req, f"WebP still required in {name}")

    def test_02_hero_pbr_facility_glbs_and_builder_hooks(self):
        fac_dir = os.path.join(UNITY, "Assets", "Models", "Facilities")
        expected = ["AgriDome_Hero.glb", "MedBay_Hero.glb", "EDL_Lander_Hero.glb", "LaunchPad_Hero.glb"]
        for name in expected:
            path = os.path.join(fac_dir, name)
            self.assertTrue(os.path.isfile(path), f"Missing facility GLB: {path}")
            jdata, size = read_glb_json(path)
            self.assertGreater(size, 20_000)
            self.assertGreater(len(jdata.get("meshes", [])), 0)

        src = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsBaseFacilitiesBuilder.cs")
        self.assertIn("AttachHeroFacilityGlbUpgrades", src)
        self.assertIn("MS_Hero_AgriDome", src)

    def test_03_rigged_astronaut_glb_has_skeleton_and_animations(self):
        rig_path = os.path.join(UNITY, "Assets", "Models", "Spacecraft", "Astronaut_Rigged.glb")
        self.assertTrue(os.path.isfile(rig_path), "Astronaut_Rigged.glb missing")
        jdata, _ = read_glb_json(rig_path)
        self.assertGreaterEqual(len(jdata.get("skins", [])), 1, "Astronaut_Rigged.glb missing glTF skin")
        anim_names = {a.get("name", "") for a in jdata.get("animations", [])}
        for clip in ("Idle", "Walk", "Sprint", "Jump"):
            self.assertIn(clip, anim_names, f"Missing skeletal animation clip '{clip}'")

        astro_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsAstronautController.cs")
        self.assertIn("BindSkeletalBones", astro_cs)
        self.assertIn("DriveSkeletalBones", astro_cs)

    def test_04_urp_quality_render_graph_xray_cinemachine_and_shell(self):
        rp = read_text(UNITY, "Assets", "Settings", "PC_RPAsset.asset")
        self.assertIn("m_ShadowDistance: 360", rp)
        self.assertIn("m_MainLightShadowmapResolution: 4096", rp)
        self.assertIn("m_MSAA: 4", rp)
        self.assertIn("m_GPUResidentDrawerMode: 1", rp)

        rend = read_text(UNITY, "Assets", "Settings", "PC_Renderer.asset")
        self.assertIn("MarsXRayScannerRendererFeature", rend)
        self.assertIn("Intensity: 0.65", rend)

        xray_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsXRayScannerRendererFeature.cs")
        for sym in ("RecordRenderGraph", "AddBlitPass", "GetTextureDesc", "ConfigureInput"):
            self.assertIn(sym, xray_cs)
        self.assertTrue(os.path.isfile(os.path.join(UNITY, "Assets", "Shaders", "MarsXRayScanner.shader")))

        cam_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsCameraController.cs")
        self.assertIn("CinemachineBasicMultiChannelPerlin", cam_cs)

        shell_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsGameShellUI.cs")
        for state in ("Title = 0", "Playing = 1", "Paused = 2", "Ended = 3", "SetPauseDucking"):
            self.assertIn(state, shell_cs)

    def test_05_web_local_threejs_glbs_3d_heatmaps_bookmarks_and_rtl(self):
        self.assertTrue(os.path.isfile(os.path.join(WEB, "vendor", "three.min.js")))
        self.assertTrue(os.path.isfile(os.path.join(WEB, "vendor", "GLTFLoader.js")))

        html = read_text(WEB, "index.html")
        self.assertIn("vendor/three.min.js", html)
        self.assertIn("vendor/GLTFLoader.js", html)
        self.assertIn("mars_earth_3d_upgrade.js", html)
        self.assertNotIn("cdnjs.cloudflare.com", html)

        models_dir = os.path.join(WEB, "assets", "models")
        web_glbs = ["perseverance_web.glb", "ingenuity_web.glb", "hdu_habitat_web.glb", "agridome_web.glb", "astronaut_web.glb"]
        total_bytes = 0
        for name in web_glbs:
            jdata, size = read_glb_json(os.path.join(models_dir, name))
            total_bytes += size
            self.assertEqual(jdata.get("extensionsRequired", []), [])
        self.assertLess(total_bytes, 4_500_000, f"Web GLBs bundle too heavy: {total_bytes} B")

        js = read_text(WEB, "mars_earth_3d_upgrade.js")
        for sym in ("GLTFLoader", "MS_3D_TerrainHeatmapOverlay", "olympus", "valles", "ms-rtl-ar", "flyToBookmark"):
            self.assertIn(sym, js)

    def test_06_physics_ai_audio_localization_persistence_and_coop(self):
        terrain_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsTerrainManager.cs")
        self.assertIn("MeshCollider", terrain_cs)

        rover_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsRoverVehicleController.cs")
        self.assertIn("WheelSlipPercent", rover_cs)
        self.assertIn("TractionFactor", rover_cs)

        scout_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsAutonomousScoutAI.cs")
        self.assertIn("NavMeshAgent", scout_cs)
        self.assertIn("maxSafeSlopeDeg", scout_cs)

        audio_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsAudioManager.cs")
        for sym in ("AudioLowPassFilter", "AudioReverbFilter", "spatialBlend = 1.0f", "SetPauseDucking"):
            self.assertIn(sym, audio_cs)

        loc_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsLocalizationManager.cs")
        self.assertIn("Arabic", loc_cs)

        pers_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsMissionPersistence.cs")
        self.assertIn("CloudSaveService", pers_cs)

        coop_cs = read_text(UNITY, "Assets", "Scripts", "NASA", "MarsCoopWingmanSync.cs")
        self.assertIn("RfVoiceQualityPercent", coop_cs)


if __name__ == "__main__":
    unittest.main()
