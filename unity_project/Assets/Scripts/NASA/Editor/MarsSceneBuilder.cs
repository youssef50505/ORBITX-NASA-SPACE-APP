#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using MarsSentinel.Bootstrap;
using MarsSentinel.NASA;

namespace MarsSentinel.Editor
{
    public static class MarsSceneBuilder
    {
        [MenuItem("MarsSentinel/Build Hero Showcase Scene")]
        public static void BuildHeroScene()
        {
            Debug.Log("[MarsSceneBuilder] Starting MarsSentinel Hero Showcase Scene Construction...");

            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            string heroScenePath = "Assets/Scenes/MarsSentinelHeroScene.unity";

            var scene = EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);

            // 1. Setup Camera Rig on Main Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            mainCam.farClipPlane = 10000f; // Prevents clipping Phobos (850m), Deimos (1750m) and orbital paths
            mainCam.nearClipPlane = 0.3f;

            var additionalCamData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (additionalCamData == null)
            {
                additionalCamData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            additionalCamData.renderPostProcessing = true;
            additionalCamData.volumeLayerMask = ~0; // Everything

            var brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            }
            brain.DefaultBlend.Time = 1.5f;
            brain.DefaultBlend.Style = CinemachineBlendDefinition.Styles.EaseInOut;

            var camController = mainCam.GetComponent<MarsCameraController>();
            if (camController == null)
            {
                camController = mainCam.gameObject.AddComponent<MarsCameraController>();
            }

            // 2. Setup Directional Sun & Lighting
            Light sunLight = null;
            Light[] lights = Object.FindObjectsByType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    break;
                }
            }

            if (sunLight == null)
            {
                var sunGo = new GameObject("Directional Light");
                sunLight = sunGo.AddComponent<Light>();
                sunLight.type = LightType.Directional;
            }

            sunLight.color = new Color(1.0f, 0.8784f, 0.698f, 1.0f); // #FFE0B2
            sunLight.intensity = 1.95f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(34f, -38f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7686f, 0.4784f, 0.3216f, 1.0f); // #C47A52
            RenderSettings.ambientEquatorColor = new Color(0.549f, 0.3098f, 0.196f, 1.0f); // #8C4F32
            RenderSettings.ambientGroundColor = new Color(0.1804f, 0.0941f, 0.0627f, 1.0f); // #2E1810

            // 3. Setup Global Volume with SampleSceneProfile
            Volume globalVolume = Object.FindAnyObjectByType<Volume>();
            if (globalVolume == null)
            {
                var volGo = new GameObject("Global Volume");
                globalVolume = volGo.AddComponent<Volume>();
                globalVolume.isGlobal = true;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
            {
                globalVolume.sharedProfile = profile;
            }

            // 4. Setup Showcase Orchestrator & Services
            GameObject orchestrator = GameObject.Find("MarsSentinelShowcase");
            if (orchestrator == null)
            {
                orchestrator = new GameObject("MarsSentinelShowcase");
            }

            if (orchestrator.GetComponent<MarsServicesBootstrap>() == null)
                orchestrator.AddComponent<MarsServicesBootstrap>();

            if (orchestrator.GetComponent<MarsDatasetLoader>() == null)
                orchestrator.AddComponent<MarsDatasetLoader>();

            if (orchestrator.GetComponent<MarsTelemetryController>() == null)
                orchestrator.AddComponent<MarsTelemetryController>();

            if (orchestrator.GetComponent<MarsSceneManager>() == null)
                orchestrator.AddComponent<MarsSceneManager>();

            if (orchestrator.GetComponent<MarsCelestialOrbitController>() == null)
                orchestrator.AddComponent<MarsCelestialOrbitController>();

            if (orchestrator.GetComponent<MarsSatelliteConstellation>() == null)
                orchestrator.AddComponent<MarsSatelliteConstellation>();

            if (orchestrator.GetComponent<MarsSimulationAnimator>() == null)
                orchestrator.AddComponent<MarsSimulationAnimator>();

            if (orchestrator.GetComponent<MarsCtxNavSolarEngine>() == null)
                orchestrator.AddComponent<MarsCtxNavSolarEngine>();

            if (orchestrator.GetComponent<MarsSurvivalMissionController>() == null)
                orchestrator.AddComponent<MarsSurvivalMissionController>();

            if (orchestrator.GetComponent<MarsCinematicVfxAndPost>() == null)
                orchestrator.AddComponent<MarsCinematicVfxAndPost>();

            if (orchestrator.GetComponent<MarsLocalizationManager>() == null)
                orchestrator.AddComponent<MarsLocalizationManager>();

            if (orchestrator.GetComponent<MarsMissionPersistence>() == null)
                orchestrator.AddComponent<MarsMissionPersistence>();

            if (orchestrator.GetComponent<MarsAutonomousScoutAI>() == null)
                orchestrator.AddComponent<MarsAutonomousScoutAI>();

            if (orchestrator.GetComponent<MarsCoopWingmanSync>() == null)
                orchestrator.AddComponent<MarsCoopWingmanSync>();

            if (orchestrator.GetComponent<MarsGameShellUI>() == null)
                orchestrator.AddComponent<MarsGameShellUI>();

            if (orchestrator.GetComponent<MarsStoryDirector>() == null)
                orchestrator.AddComponent<MarsStoryDirector>();

            EnsureXRayRendererFeatureOnPcRenderer();
            BakeJezeroTerrainAndSkyIntoScene();

            // Remove duplicate stray MarsCameraRigs or *_OrbitTrail GameObjects if re-running on existing scene
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            bool keptCameraRigs = false;
            var seenTrails = new System.Collections.Generic.HashSet<string>();
            foreach (var tr in allTransforms)
            {
                if (tr == null) continue;
                if (tr.name == "MarsCameraRigs")
                {
                    if (!keptCameraRigs && tr.childCount > 0)
                    {
                        keptCameraRigs = true;
                        tr.SetParent(null, false);
                    }
                    else if (tr.childCount == 0)
                    {
                        Object.DestroyImmediate(tr.gameObject);
                    }
                }
                else if (tr.name.EndsWith("_OrbitTrail"))
                {
                    if (!seenTrails.Add(tr.name))
                    {
                        Object.DestroyImmediate(tr.gameObject);
                    }
                }
            }

            // 5. Integrate 3D Fleet Models Hierarchy
            Transform surfaceRoot = orchestrator.transform.Find("MS_Mars_Fleet_Surface");
            if (surfaceRoot == null)
            {
                var go = new GameObject("MS_Mars_Fleet_Surface");
                go.transform.SetParent(orchestrator.transform, false);
                surfaceRoot = go.transform;
            }

            Transform orbitRoot = orchestrator.transform.Find("MS_Mars_Fleet_Orbit");
            if (orbitRoot == null)
            {
                var go = new GameObject("MS_Mars_Fleet_Orbit");
                go.transform.SetParent(orchestrator.transform, false);
                orbitRoot = go.transform;
            }

            // Instantiate Hero Web GLB if available
            GameObject heroGlbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Hero/MARS_SENTINEL_HERO_WEB.glb");
            if (heroGlbPrefab != null && surfaceRoot.Find("MARS_SENTINEL_HERO_WEB") == null)
            {
                GameObject heroInst = (GameObject)PrefabUtility.InstantiatePrefab(heroGlbPrefab, surfaceRoot);
                heroInst.name = "MARS_SENTINEL_HERO_WEB";
                heroInst.transform.localPosition = Vector3.zero;
                EnsureUrpMaterials(heroInst);
            }

            // Instantiate standalone fleet models under surface root (normalized to exact NASA metric size & grounded to terrain!)
            InstantiateFleetModel("mars-2020-perseverance-rover.glb", "MS_Perseverance_Root", surfaceRoot, GroundedPos(0f, 0f), 3.0f);
            InstantiateFleetModel("Ingenuity_Mars_Helicopter.glb", "MS_Ingenuity_Root", surfaceRoot, GroundedPos(8f, 10f), 1.4f);
            InstantiateFleetModel("Curiosity Rover (MSL).glb", "MS_Curiosity_Root", surfaceRoot, GroundedPos(-8f, -14f), 3.0f);
            InstantiateFleetModel("InSight Cruise Lander (panels deployed).glb", "MS_InSight_Lander_Root", surfaceRoot, GroundedPos(-14f, 18f), 5.6f);
            InstantiateFleetModel("mars-exploration-rover-opportunity-mer-b.glb", "MS_Opportunity_Root", surfaceRoot, GroundedPos(-20f, 4f), 2.3f);
            InstantiateFleetModel("Viking Lander.glb", "MS_Viking_Lander_Root", surfaceRoot, GroundedPos(18f, -8f), 3.2f);

            // Wire Ingenuity Rotor Animator
            Transform ingNode = surfaceRoot.Find("MS_Ingenuity_Root");
            if (ingNode != null)
            {
                var ingAnim = ingNode.GetComponent<IngenuityRotorAnimator>() ?? ingNode.gameObject.AddComponent<IngenuityRotorAnimator>();
                ingAnim.LocateOrCreateRotors();
            }

            // Wire Terrain Manager on MS_Terrain_Jezero
            Transform terrainNode = surfaceRoot.Find("MS_Terrain_Jezero");
            if (terrainNode == null)
            {
                var tGo = new GameObject("MS_Terrain_Jezero");
                tGo.transform.SetParent(surfaceRoot, false);
                terrainNode = tGo.transform;
            }
            if (terrainNode.GetComponent<MarsTerrainManager>() == null)
            {
                var terrMgr = terrainNode.gameObject.AddComponent<MarsTerrainManager>();
                terrMgr.InitializeTerrain();
            }

            // Instantiate orbiters under orbital root (metric solar-array span)
            InstantiateFleetModel("Mars Reconnaissance Orbiter (MRO) (A).glb", "MS_MRO_Root", orbitRoot, new Vector3(-80, 250, 150), 13.6f);
            InstantiateFleetModel("Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb", "MS_MAVEN_Root", orbitRoot, new Vector3(60, 300, -100), 11.4f);
            InstantiateFleetModel("Mars Odyssey.glb", "MS_Odyssey_Root", orbitRoot, new Vector3(0, 280, 200), 5.7f);
            InstantiateFleetModel("Mars Global Surveyor.glb", "MS_GlobalSurveyor_Root", orbitRoot, new Vector3(100, 260, 50), 12.0f);

            // Instantiate Phase 1.2 Hero PBR Facility GLBs & Phase 1.3 Rigged Astronaut GLB
            Transform facRoot = orchestrator.transform.Find("MS_Base_Facilities_HeroGLBs");
            if (facRoot == null)
            {
                var fGo = new GameObject("MS_Base_Facilities_HeroGLBs");
                fGo.transform.SetParent(orchestrator.transform, false);
                facRoot = fGo.transform;
            }
            InstantiateAssetAtPath("Assets/Models/Facilities/AgriDome_Hero.glb", "MS_Hero_AgriDome", facRoot, GroundedPos(-37.5f, -6.5f));
            InstantiateAssetAtPath("Assets/Models/Facilities/MedBay_Hero.glb", "MS_Hero_MedBay", facRoot, GroundedPos(-1.5f, 30.0f));
            InstantiateAssetAtPath("Assets/Models/Facilities/EDL_Lander_Hero.glb", "MS_Hero_EDL_Lander", facRoot, GroundedPos(32f, -18f));
            InstantiateAssetAtPath("Assets/Models/Facilities/LaunchPad_Hero.glb", "MS_Hero_LaunchPad", facRoot, GroundedPos(36f, 24f));
            InstantiateAssetAtPath("Assets/Models/Spacecraft/Astronaut_Rigged.glb", "EVA_NASA_Astronaut_Rigged", surfaceRoot, GroundedPos(-3.5f, -16f));

            // Sink tiny legacy 6m STL square below baked 1,280m Jezero terrain so no floating square appears
            if (terrainNode != null)
            {
                Vector3 tp = terrainNode.localPosition;
                terrainNode.localPosition = new Vector3(tp.x, -1.25f, tp.z);
            }

            // Bind Moons & Keplerian Constellation AFTER orbitRoot fleet anchors exist
            var celCtrl = orchestrator.GetComponent<MarsCelestialOrbitController>();
            celCtrl.EnsureMoonsHierarchy();
            celCtrl.SetSolHour(12.0f); // Position Phobos (850m) & Deimos (1750m) cleanly in Mars sky orbit!

            var constCtrl = orchestrator.GetComponent<MarsSatelliteConstellation>();
            constCtrl.EnsureDefaultConstellation();
            constCtrl.InitializeOrbitTrails();

            // 6. Initialize camera rig
            camController.InitializeCameraRig();

            // 7. Save Scenes
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, sampleScenePath);
            EditorSceneManager.SaveScene(scene, heroScenePath, true);

            // 8. Register scenes in Build Settings
            var buildScenes = new[]
            {
                new EditorBuildSettingsScene(heroScenePath, true),
                new EditorBuildSettingsScene(sampleScenePath, true)
            };
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            Debug.Log("[MarsSceneBuilder] Hero Showcase Scene built and saved successfully to both SampleScene.unity and MarsSentinelHeroScene.unity!");
        }

        [MenuItem("MarsSentinel/Build Windows Standalone")]
        public static void BuildWindowsPlayer()
        {
            Debug.Log("[MarsSceneBuilder] Building Windows Standalone 64-bit Player...");
            string buildPath = "Builds/Windows/MarsSentinel.exe";
            string dir = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string[] scenes = new[]
            {
                "Assets/Scenes/MarsSentinelHeroScene.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MarsSceneBuilder] Build succeeded: {summary.totalSize} bytes, duration {summary.totalTime.TotalSeconds:F1}s, warnings: {summary.totalWarnings}, errors: {summary.totalErrors}");
            }
            else
            {
                Debug.LogError($"[MarsSceneBuilder] Build failed with result: {summary.result}, errors: {summary.totalErrors}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        [MenuItem("MarsSentinel/Build WebGL Player")]
        public static void BuildWebGlPlayer()
        {
            Debug.Log("[MarsSceneBuilder] Building WebGL Player...");
            string buildPath = "Builds/WebGL";
            if (!Directory.Exists(buildPath)) Directory.CreateDirectory(buildPath);

            string[] scenes = new[]
            {
                "Assets/Scenes/MarsSentinelHeroScene.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MarsSceneBuilder] WebGL Build succeeded: {report.summary.totalSize} bytes");
            }
            else
            {
                Debug.LogError($"[MarsSceneBuilder] WebGL Build failed with {report.summary.totalErrors} errors");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("MarsSentinel/Verify Overhaul & Capture GPU Frames")]
        public static void VerifyOverhaulAndCapture()
        {
            string outDir = @"C:\Users\yhsn5\.gemini\antigravity\brain\d720beb4-3e60-4444-b362-918df577fc42\scratch\unity_editor_captures";
            Directory.CreateDirectory(outDir);

            EditorSceneManager.OpenScene("Assets/Scenes/MarsSentinelHeroScene.unity", OpenSceneMode.Single);

            // 1. Verify URP & Post-Processing pipeline setup
            var urpAsset = UniversalRenderPipeline.asset;
            bool hdrOn = urpAsset != null && urpAsset.supportsHDR;
            Camera mainCam = Camera.main;
            var camData = mainCam != null ? mainCam.GetComponent<UniversalAdditionalCameraData>() : null;
            bool camPp = camData != null && camData.renderPostProcessing;
            Volume vol = Object.FindAnyObjectByType<Volume>();
            VolumeProfile prof = vol != null ? vol.sharedProfile : null;

            // Ensure SampleSceneProfile carries our tuned Martian ACES + Bloom + ColorAdjustments + Vignette overrides
            if (prof != null)
            {
                if (!prof.TryGet(out Tonemapping tone)) tone = prof.Add<Tonemapping>(true);
                tone.active = true; tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES;
                if (!prof.TryGet(out Bloom bloom)) bloom = prof.Add<Bloom>(true);
                bloom.active = true; bloom.threshold.overrideState = true; bloom.threshold.value = 1.15f;
                bloom.intensity.overrideState = true; bloom.intensity.value = 0.34f;
                if (!prof.TryGet(out ColorAdjustments ca)) ca = prof.Add<ColorAdjustments>(true);
                ca.active = true; ca.postExposure.overrideState = true; ca.postExposure.value = 0.08f;
                ca.contrast.overrideState = true; ca.contrast.value = 7.0f; ca.saturation.overrideState = true; ca.saturation.value = 8.0f;
                if (!prof.TryGet(out WhiteBalance wb)) wb = prof.Add<WhiteBalance>(true);
                wb.active = true; wb.temperature.overrideState = true; wb.temperature.value = 6.0f;
                if (!prof.TryGet(out Vignette vig)) vig = prof.Add<Vignette>(true);
                vig.active = true; vig.intensity.overrideState = true; vig.intensity.value = 0.24f;
                EditorUtility.SetDirty(prof);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[OverhaulVerify] URP HDR={hdrOn} | CamPostProcess={camPp} | VolumeProfile={(prof != null ? prof.name : "null")} | Components={(prof != null ? prof.components.Count : 0)}");

            // 2. Verify MarsCtxNavSolarEngine real 399x378 CTX A* + Solar Optics + RF LOS + SiteCheck
            var ctxGo = new GameObject("_TempCtxVerify");
            var ctx = ctxGo.AddComponent<MarsCtxNavSolarEngine>();
            var loadMethod = typeof(MarsCtxNavSolarEngine).GetMethod("LoadCtxGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            loadMethod?.Invoke(ctx, null);
            if (ctx.IsLoaded)
            {
                ctx.RecomputePreset(0);
                MarsCtxNavSolarEngine.ComputeSolarPos(18.4447, 90.0, 12.0, out double el, out double az);
                Debug.Log($"[OverhaulVerify] CTX A* Loaded=True | Shortest={ctx.ShortestRoute.distanceMeters:F1}m (maxSlope={ctx.ShortestRoute.maxSlopeDeg:F1}°, wpts={ctx.ShortestRoute.waypointCount}) | Safest={ctx.SafestRoute.distanceMeters:F1}m (maxSlope={ctx.SafestRoute.maxSlopeDeg:F1}°, wpts={ctx.SafestRoute.waypointCount}) | NoonEl={el:F1}° Az={az:F1}° | RF_Clear={ctx.ActiveRfLos.clear} | SitePass={ctx.ActiveSiteCheck.pass} (slope={ctx.ActiveSiteCheck.slopeDeg:F1}°, rough={ctx.ActiveSiteCheck.roughMeters:F2}m, sun={ctx.ActiveSiteCheck.daylightPercent}%)");
            }
            else
            {
                Debug.LogError("[OverhaulVerify] CTX A* grid failed to load!");
            }
            Object.DestroyImmediate(ctxGo);

            // 3. Render 3 GPU screenshots of upgraded Hero Scene (Campus, Perseverance Rover GLB, Terrain Shader)
            if (mainCam != null)
            {
                Vector3 origPos = mainCam.transform.position;
                Quaternion origRot = mainCam.transform.rotation;

                CaptureCameraFrame(mainCam, new Vector3(-8f, 9.5f, -24f), new Vector3(0f, 1.5f, 4f), Path.Combine(outDir, "unity_overhaul_01_campus.png"));
                CaptureCameraFrame(mainCam, new Vector3(4.2f, 2.6f, -5.2f), new Vector3(0f, 1.1f, 0f), Path.Combine(outDir, "unity_overhaul_02_rover_glb.png"));
                CaptureCameraFrame(mainCam, new Vector3(24f, 16f, -34f), new Vector3(0f, 2f, 14f), Path.Combine(outDir, "unity_overhaul_03_terrain_overview.png"));

                // Verify X-Ray Render Graph Scanner pulse capture at peak visibility
                MarsXRayScannerRendererFeature.ForcePreviewPulse(new Vector3(0f, 0f, 0f), 0.35f, 0);
                CaptureCameraFrame(mainCam, new Vector3(-12f, 11f, -22f), new Vector3(0f, 1.2f, 0f), Path.Combine(outDir, "unity_overhaul_04_xray_scanner.png"));

                mainCam.transform.position = origPos;
                mainCam.transform.rotation = origRot;
                Debug.Log($"[OverhaulVerify] Saved 4 GPU verification captures to {outDir}");
            }

            // 4. Verify Phase 1-5 Fleet, Facilities, Rigged Astronaut, Render Graph Feature, URP Quality
            string[] fleetNodes = {
                "MS_Perseverance_Root", "MS_Ingenuity_Root", "MS_Curiosity_Root", "MS_InSight_Lander_Root",
                "MS_Opportunity_Root", "MS_Viking_Lander_Root", "MS_MRO_Root", "MS_MAVEN_Root",
                "MS_Odyssey_Root", "MS_GlobalSurveyor_Root"
            };
            int loadedFleet = 0;
            foreach (string fn in fleetNodes)
            {
                GameObject n = GameObject.Find(fn);
                if (n != null && n.GetComponentsInChildren<Renderer>(true).Length > 0) loadedFleet++;
            }

            string[] facNodes = { "MS_Hero_AgriDome", "MS_Hero_MedBay", "MS_Hero_EDL_Lander", "MS_Hero_LaunchPad" };
            int loadedFac = 0;
            foreach (string fn in facNodes)
            {
                GameObject n = GameObject.Find(fn);
                if (n != null && n.GetComponentsInChildren<Renderer>(true).Length > 0) loadedFac++;
            }

            GameObject riggedAstro = GameObject.Find("EVA_NASA_Astronaut_Rigged");
            int riggedBones = riggedAstro != null ? riggedAstro.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length : 0;

            var pcRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            bool hasXRayFeature = false;
            if (pcRenderer != null)
            {
                foreach (var rf in pcRenderer.rendererFeatures)
                {
                    if (rf is MarsXRayScannerRendererFeature) { hasXRayFeature = true; break; }
                }
            }

            Debug.Log($"[Phase1to5Verify] FleetLoaded={loadedFleet}/10 | HeroFacilities={loadedFac}/4 | RiggedAstroSkinnedMeshes={riggedBones} | XRayRenderGraphFeature={hasXRayFeature} | ShadowDist={(urpAsset != null ? urpAsset.shadowDistance : 0f)} | MSAA={(urpAsset != null ? urpAsset.msaaSampleCount : 0)}");
        }

        private static Vector3 GroundedPos(float x, float z)
        {
            return new Vector3(x, MarsVisualRealismPolish.SampleTerrainElevation(x, z), z);
        }

        /// <summary>
        /// Track A.1: Bakes a real 1,280m x 1,280m sculpted Jezero Crater Terrain + MeshCollider +
        /// MartianSandSlopeBlend FBM regolith material + sunlit Martian Atmosphere Skybox + scattered
        /// basalt boulders directly into the saved Unity scenes so Editor & Play Mode look AAA immediately.
        /// </summary>
        private static void BakeJezeroTerrainAndSkyIntoScene()
        {
            const string terrainDir = "Assets/Models/Terrain";
            if (!Directory.Exists(terrainDir)) Directory.CreateDirectory(terrainDir);

            // 1. Build & save 192x192 sculpted Jezero Crater mesh (1,280m x 1,280m)
            string meshAssetPath = $"{terrainDir}/JezeroHorizonTerrain.asset";
            Mesh mesh = new Mesh { name = "JezeroHorizonTerrain" };
            mesh.indexFormat = IndexFormat.UInt32;
            const int res = 160;
            const float worldSize = 1280f;
            float half = worldSize * 0.5f;
            int vertCount = (res + 1) * (res + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[res * res * 6];

            int vi = 0;
            for (int z = 0; z <= res; z++)
            {
                float vz = ((float)z / res) * worldSize - half;
                for (int x = 0; x <= res; x++)
                {
                    float vx = ((float)x / res) * worldSize - half;
                    float vy = MarsVisualRealismPolish.SampleTerrainElevation(vx, vz);
                    verts[vi] = new Vector3(vx, vy, vz);
                    uvs[vi] = new Vector2((float)x / res, (float)z / res);
                    vi++;
                }
            }
            int ti = 0;
            int stride = res + 1;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int a = z * stride + x;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (existingMesh != null) AssetDatabase.DeleteAsset(meshAssetPath);
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            // 2. Save MartianSandSlopeBlend FBM regolith material asset
            string matPath = $"{terrainDir}/M_JezeroRegolithBaked.mat";
            Shader slopeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MartianSandSlopeBlend.shader")
                                 ?? Shader.Find("MarsSentinel/MartianSandSlopeBlend")
                                 ?? Shader.Find("Universal Render Pipeline/Lit");
            Material bakedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (bakedMat == null)
            {
                bakedMat = new Material(slopeShader) { name = "M_JezeroRegolithBaked" };
                AssetDatabase.CreateAsset(bakedMat, matPath);
            }
            else
            {
                bakedMat.shader = slopeShader;
            }
            if (bakedMat.HasProperty("_SandColor")) bakedMat.SetColor("_SandColor", new Color(0.76f, 0.44f, 0.24f, 1f));
            if (bakedMat.HasProperty("_RockColor")) bakedMat.SetColor("_RockColor", new Color(0.36f, 0.21f, 0.14f, 1f));
            if (bakedMat.HasProperty("_RippleStrength")) bakedMat.SetFloat("_RippleStrength", 0.55f);
            if (bakedMat.HasProperty("_RippleFrequency")) bakedMat.SetFloat("_RippleFrequency", 3.2f);
            EditorUtility.SetDirty(bakedMat);

            // 3. Attach permanent MS_Jezero_Horizon_Terrain GameObject with MeshCollider
            GameObject terrainGo = GameObject.Find("MS_Jezero_Horizon_Terrain");
            if (terrainGo == null) terrainGo = new GameObject("MS_Jezero_Horizon_Terrain");
            terrainGo.transform.position = Vector3.zero;
            terrainGo.transform.rotation = Quaternion.identity;
            terrainGo.transform.localScale = Vector3.one;
            var mf = terrainGo.GetComponent<MeshFilter>();
            if (mf == null) mf = terrainGo.AddComponent<MeshFilter>();
            var mr = terrainGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = terrainGo.AddComponent<MeshRenderer>();
            var mc = terrainGo.GetComponent<MeshCollider>();
            if (mc == null) mc = terrainGo.AddComponent<MeshCollider>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = bakedMat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            mc.convex = false;
            mc.sharedMesh = mesh;

            // 4. Bake Martian Atmosphere Skybox & golden atmospheric fog so Edit Mode looks breathtaking too
            string skyPath = $"{terrainDir}/M_MartianSkyBaked.mat";
            Shader skyShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MartianAtmosphereSkybox.shader")
                               ?? Shader.Find("MarsSentinel/MartianAtmosphereSkybox");
            if (skyShader != null)
            {
                Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (skyMat == null)
                {
                    skyMat = new Material(skyShader) { name = "M_MartianSkyBaked" };
                    AssetDatabase.CreateAsset(skyMat, skyPath);
                }
                RenderSettings.skybox = skyMat;
                EditorUtility.SetDirty(skyMat);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.73f, 0.48f, 0.32f, 1f);
            RenderSettings.fogDensity = 0.0022f;

            // 5. Scatter 36 grounded basalt perimeter boulders cleanly outside base pads
            GameObject rocksRoot = GameObject.Find("MS_Baked_Basalt_Boulders");
            if (rocksRoot != null) Object.DestroyImmediate(rocksRoot);
            rocksRoot = new GameObject("MS_Baked_Basalt_Boulders");
            string rockMatPath = $"{terrainDir}/M_BasaltBoulderBaked.mat";
            Material rockMat = AssetDatabase.LoadAssetAtPath<Material>(rockMatPath);
            if (rockMat == null)
            {
                rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_BasaltBoulderBaked" };
                rockMat.SetColor("_BaseColor", new Color(0.30f, 0.20f, 0.16f, 1f));
                rockMat.SetFloat("_Smoothness", 0.16f);
                AssetDatabase.CreateAsset(rockMat, rockMatPath);
            }
            var rng = new System.Random(1042);
            for (int i = 0; i < 36; i++)
            {
                float ang = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                float rad = 22f + (float)(rng.NextDouble() * 88.0);
                float rx = Mathf.Cos(ang) * rad;
                float rz = Mathf.Sin(ang) * rad;
                // Keep clear of Launch Pad, Agri-Dome, MedBay, and EDL pads
                if (Vector2.Distance(new Vector2(rx, rz), new Vector2(36f, 24f)) < 14f) continue;
                if (Vector2.Distance(new Vector2(rx, rz), new Vector2(-25f, -4f)) < 14f) continue;
                if (Vector2.Distance(new Vector2(rx, rz), new Vector2(-10f, 24f)) < 12f) continue;
                if (Vector2.Distance(new Vector2(rx, rz), new Vector2(32f, -18f)) < 11f) continue;
                float ry = MarsVisualRealismPolish.SampleTerrainElevation(rx, rz);
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = $"MS_Basalt_Rock_{i:D2}";
                b.transform.SetParent(rocksRoot.transform, false);
                float sx = 0.6f + (float)rng.NextDouble() * 1.9f;
                float sy = sx * (0.35f + (float)rng.NextDouble() * 0.35f);
                float sz = sx * (0.7f + (float)rng.NextDouble() * 0.5f);
                b.transform.position = new Vector3(rx, ry + sy * 0.25f, rz);
                b.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 25f);
                b.transform.localScale = new Vector3(sx, sy, sz);
                var r = b.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = rockMat;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MarsSceneBuilder] Baked 1,280m Jezero Crater terrain, MartianSandSlopeBlend shader, skybox & 36 basalt boulders into scene.");
        }

        private static void EnsureXRayRendererFeatureOnPcRenderer()
        {
            string rendererPath = "Assets/Settings/PC_Renderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"[MarsSceneBuilder] Could not load {rendererPath} to attach MarsXRayScannerRendererFeature.");
                return;
            }

            MarsXRayScannerRendererFeature existing = null;
            foreach (var f in rendererData.rendererFeatures)
            {
                if (f is MarsXRayScannerRendererFeature xf)
                {
                    existing = xf;
                    break;
                }
            }

            Shader xrayShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MarsXRayScanner.shader");
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<MarsXRayScannerRendererFeature>();
                existing.name = "MarsXRayScannerRendererFeature";
                existing.settings.scannerShader = xrayShader;
                AssetDatabase.AddObjectToAsset(existing, rendererData);

                var so = new SerializedObject(rendererData);
                var featuresProp = so.FindProperty("m_RendererFeatures");
                int idx = featuresProp.arraySize;
                featuresProp.InsertArrayElementAtIndex(idx);
                featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = existing;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                Debug.Log("[MarsSceneBuilder] Attached MarsXRayScannerRendererFeature (URP Render Graph) to PC_Renderer.asset.");
            }
            else if (existing.settings.scannerShader == null && xrayShader != null)
            {
                existing.settings.scannerShader = xrayShader;
                EditorUtility.SetDirty(existing);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
            }
        }

        private static void InstantiateAssetAtPath(string assetPath, string nodeName, Transform parent, Vector3 localPos)
        {
            Transform existing = parent.Find(nodeName);
            if (existing != null && existing.GetComponentsInChildren<Renderer>(true).Length > 0) return;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.name = nodeName;
                inst.transform.localPosition = localPos;
                EnsureUrpMaterials(inst);
                Debug.Log($"[MarsSceneBuilder] Instantiated hero asset '{nodeName}' from '{assetPath}'.");
            }
            else
            {
                Debug.LogWarning($"[MarsSceneBuilder] Failed to load asset at '{assetPath}'.");
            }
        }

        private static void CaptureCameraFrame(Camera cam, Vector3 pos, Vector3 lookAt, string filePath)
        {
            int w = 1280, h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        private static void InstantiateFleetModel(string modelFile, string nodeName, Transform parent, Vector3 localPos, float targetSizeMeters = 3.0f)
        {
            Transform existing = parent.Find(nodeName);
            string instName = Path.GetFileNameWithoutExtension(modelFile);

            GameObject node = existing != null ? existing.gameObject : new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPos;

            Transform instTf = node.transform.Find(instName);
            GameObject inst = instTf != null ? instTf.gameObject : null;
            if (inst == null)
            {
                string path = $"Assets/Models/Fleet/{modelFile}";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
                    inst.name = instName;
                    inst.transform.localPosition = Vector3.zero;
                    EnsureUrpMaterials(inst);
                    Debug.Log($"[MarsSceneBuilder] Instantiated 3D model '{instName}' under '{nodeName}'.");
                }
                else
                {
                    Debug.LogWarning($"[MarsSceneBuilder] Failed to load GameObject prefab at '{path}'.");
                    return;
                }
            }

            // Normalize every NASA fleet GLB cleanly to real metric scale & ground bottom flush to local Y=0
            if (inst != null && targetSizeMeters > 0.1f)
            {
                inst.transform.localScale = Vector3.one;
                inst.transform.localPosition = Vector3.zero;
                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (maxDim > 0.001f)
                    {
                        float s = targetSizeMeters / maxDim;
                        inst.transform.localScale = Vector3.one * s;
                        Bounds b2 = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b2.Encapsulate(rends[i].bounds);
                        Vector3 worldDelta = new Vector3(
                            node.transform.position.x - b2.center.x,
                            node.transform.position.y - b2.min.y,
                            node.transform.position.z - b2.center.z);
                        inst.transform.position += worldDelta;
                    }
                }
            }
        }

        private static void EnsureUrpMaterials(GameObject root)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null)
                    {
                        string sName = mats[i].shader.name;
                        if (sName.Contains("Error") || sName.Contains("InternalError") || sName.Contains("Standard"))
                        {
                            mats[i].shader = urpLit;
                        }
                    }
                }
            }
        }
    }
}
#endif
