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
            sunLight.intensity = 1.3f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

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

            // Instantiate standalone fleet models under surface root
            InstantiateFleetModel("mars-2020-perseverance-rover.glb", "MS_Perseverance_Root", surfaceRoot, new Vector3(0, 0, 0));
            InstantiateFleetModel("Ingenuity_Mars_Helicopter.glb", "MS_Ingenuity_Root", surfaceRoot, new Vector3(8, 0, 10));
            InstantiateFleetModel("Curiosity Rover (MSL).glb", "MS_Curiosity_Root", surfaceRoot, new Vector3(-8, 0, -14));
            InstantiateFleetModel("InSight Cruise Lander (panels deployed).glb", "MS_InSight_Lander_Root", surfaceRoot, new Vector3(-12, 0, 16));
            InstantiateFleetModel("mars-exploration-rover-opportunity-mer-b.glb", "MS_Opportunity_Root", surfaceRoot, new Vector3(-20, 0, 4));
            InstantiateFleetModel("Viking Lander.glb", "MS_Viking_Lander_Root", surfaceRoot, new Vector3(18, 0, -8));

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

            // Instantiate orbiters under orbital root
            InstantiateFleetModel("Mars Reconnaissance Orbiter (MRO) (A).glb", "MS_MRO_Root", orbitRoot, new Vector3(-80, 250, 150));
            InstantiateFleetModel("Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb", "MS_MAVEN_Root", orbitRoot, new Vector3(60, 300, -100));
            InstantiateFleetModel("Mars Odyssey.glb", "MS_Odyssey_Root", orbitRoot, new Vector3(0, 280, 200));
            InstantiateFleetModel("Mars Global Surveyor.glb", "MS_GlobalSurveyor_Root", orbitRoot, new Vector3(100, 260, 50));

            // Bind Moons & Keplerian Constellation AFTER orbitRoot fleet anchors exist
            var celCtrl = orchestrator.GetComponent<MarsCelestialOrbitController>();
            celCtrl.EnsureMoonsHierarchy();

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

                mainCam.transform.position = origPos;
                mainCam.transform.rotation = origRot;
                Debug.Log($"[OverhaulVerify] Saved 3 GPU verification captures to {outDir}");
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

        private static void InstantiateFleetModel(string modelFile, string nodeName, Transform parent, Vector3 localPos)
        {
            Transform existing = parent.Find(nodeName);
            string instName = Path.GetFileNameWithoutExtension(modelFile);

            GameObject node = existing != null ? existing.gameObject : new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPos;

            if (node.transform.Find(instName) != null) return;

            string path = $"Assets/Models/Fleet/{modelFile}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
                inst.name = instName;
                inst.transform.localPosition = Vector3.zero;
                EnsureUrpMaterials(inst);
                Debug.Log($"[MarsSceneBuilder] Instantiated 3D model '{instName}' under '{nodeName}'.");
            }
            else
            {
                Debug.LogWarning($"[MarsSceneBuilder] Failed to load GameObject prefab at '{path}'.");
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
