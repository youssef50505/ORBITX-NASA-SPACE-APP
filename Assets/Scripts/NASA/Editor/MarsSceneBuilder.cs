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

            var celCtrl = orchestrator.GetComponent<MarsCelestialOrbitController>();
            celCtrl.EnsureMoonsHierarchy();

            var constCtrl = orchestrator.GetComponent<MarsSatelliteConstellation>();
            constCtrl.EnsureDefaultConstellation();
            constCtrl.InitializeOrbitTrails();

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
            if (ingNode != null && ingNode.GetComponent<IngenuityRotorAnimator>() == null)
            {
                var ingAnim = ingNode.gameObject.AddComponent<IngenuityRotorAnimator>();
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

        private static void InstantiateFleetModel(string modelFile, string nodeName, Transform parent, Vector3 localPos)
        {
            Transform existing = parent.Find(nodeName);
            if (existing != null && existing.childCount > 0) return;

            GameObject node = existing != null ? existing.gameObject : new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPos;

            string path = $"Assets/Models/Fleet/{modelFile}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
                inst.name = Path.GetFileNameWithoutExtension(modelFile);
                inst.transform.localPosition = Vector3.zero;
                EnsureUrpMaterials(inst);
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
