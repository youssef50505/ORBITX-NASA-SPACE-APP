using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MarsSentinel.NASA.Editor
{
    /// <summary>
    /// Integrates official NASA-3D-Resources GLB assets (HDU, Saturn V, Ares I, Mobile Launcher,
    /// Mark III Spacesuit) and playable ORBITX exploration systems into both Unity scenes,
    /// generates clean .meta pairings, and builds the Windows standalone executable.
    /// </summary>
    public static class MarsBaseCampusBuilder
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MarsSentinelHeroScene.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        [MenuItem("Mars Sentinel/Upgrade Scenes with Playable NASA Campus")]
        public static void InjectCampusIntoAllScenes()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[MarsBaseCampusBuilder] Scene not found: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                InjectCampusIntoActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MarsBaseCampusBuilder] Upgraded and saved scene: {scenePath}");
            }
        }

        [MenuItem("Mars Sentinel/Upgrade Campus & Build Windows Player")]
        public static void BuildAllCampusAndWindowsPlayer()
        {
            InjectCampusIntoAllScenes();

            string exePath = "Builds/Windows/MarsSentinel.exe";
            Directory.CreateDirectory("Builds/Windows");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MarsSentinelHeroScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
            };

            BuildPlayerOptions opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MarsSentinelHeroScene.unity", "Assets/Scenes/SampleScene.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;
            Debug.Log($"[MarsBaseCampusBuilder] Build result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}, size={summary.totalSize} bytes");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Windows build failed with {summary.totalErrors} errors.");
            }
        }

        private static void InjectCampusIntoActiveScene()
        {
            // Force synchronous re-import of Spacecraft GLBs in case Blender de-Draco updated them
            string[] glbs =
            {
                "Assets/Models/Spacecraft/Astronaut.glb",
                "Assets/Models/Spacecraft/Habitat Demonstration Unit (part 1).glb",
                "Assets/Models/Spacecraft/Habitat Demonstration Unit (part 2).glb",
                "Assets/Models/Spacecraft/Mobile Launcher (assembled).glb",
                "Assets/Models/Spacecraft/Saturn V.glb",
                "Assets/Models/Spacecraft/Ares 1 (A).glb",
                "Assets/Models/Spacecraft/Mark III Spacesuit.glb"
            };
            foreach (string gp in glbs)
            {
                AssetDatabase.ImportAsset(gp, ImportAssetOptions.ForceSynchronousImport);
            }

            // Destroy ANY prior/stale duplicate NASA hardware objects before instantiating cleanly
            DestroyAllByName("MS_NASA_Hardware_Imports");
            DestroyAllByName("MS_NASA_HDU_Combined");
            DestroyAllByName("MS_NASA_HDU_Part1");
            DestroyAllByName("MS_NASA_HDU_Part2");
            DestroyAllByName("MS_NASA_MobileLauncher");
            DestroyAllByName("MS_NASA_Rocket_SaturnV");
            DestroyAllByName("MS_NASA_Rocket_Ares1");
            DestroyAllByName("MS_NASA_MarkIII_Spacesuit");

            // Ensure official NASA GLB hardware models are instantiated inside the scene hierarchy
            GameObject hardwareRoot = FindOrCreateRoot("MS_NASA_Hardware_Imports");

            // Unified Habitat Demonstration Unit (Part 1 & Part 2 share NASA origin; scale & seat together)
            GameObject hduCombined = new GameObject("MS_NASA_HDU_Combined");
            hduCombined.transform.SetParent(hardwareRoot.transform, false);
            GameObject hdu1 = PlaceGlbRaw("Assets/Models/Spacecraft/Habitat Demonstration Unit (part 1).glb", "MS_NASA_HDU_Part1", hduCombined.transform);
            GameObject hdu2 = PlaceGlbRaw("Assets/Models/Spacecraft/Habitat Demonstration Unit (part 2).glb", "MS_NASA_HDU_Part2", hduCombined.transform);
            if (hdu1 != null || hdu2 != null)
            {
                hduCombined.transform.position = new Vector3(-24.0f, 0f, 18.0f);
                NormalizeEditorObject(hduCombined, new Vector3(-24.0f, 0.16f, 18.0f), 4.8f);
            }

            PlaceGlb(
                "Assets/Models/Spacecraft/Mobile Launcher (assembled).glb",
                "MS_NASA_MobileLauncher", hardwareRoot.transform,
                new Vector3(32.6f, 0.30f, 24.0f), Vector3.zero, 14.8f);

            PlaceGlb(
                "Assets/Models/Spacecraft/Saturn V.glb",
                "MS_NASA_Rocket_SaturnV", hardwareRoot.transform,
                new Vector3(36.0f, 1.08f, 24.0f), Vector3.zero, 16.5f);

            PlaceGlb(
                "Assets/Models/Spacecraft/Ares 1 (A).glb",
                "MS_NASA_Rocket_Ares1", hardwareRoot.transform,
                new Vector3(39.6f, 1.08f, 24.0f), Vector3.zero, 12.2f);

            GameObject mark3 = PlaceGlb(
                "Assets/Models/Spacecraft/Mark III Spacesuit.glb",
                "MS_NASA_MarkIII_Spacesuit", hardwareRoot.transform,
                new Vector3(-20.2f, 0.28f, 11.4f), new Vector3(0f, 128f, 0f), 1.92f);

            if (mark3 != null)
            {
                if (mark3.GetComponentInChildren<Collider>() == null)
                {
                    BoxCollider bc = mark3.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.96f, 0f);
                    bc.size = new Vector3(0.9f, 1.92f, 0.8f);
                }
                MarsInspectableTarget suitTarget = mark3.GetComponent<MarsInspectableTarget>() ?? mark3.AddComponent<MarsInspectableTarget>();
                suitTarget.Configure(
                    "nasa_mark3_spacesuit_exhibit",
                    "NASA Mark III H-1 Planetary EVA Suit",
                    "Official NASA 3D Hardware · Life Support Exhibit",
                    "Rear-Entry Hard Upper Torso · 57 kPa Exploration Suit",
                    "HDU Suitport Pad · 18.4448°N, 77.4505°E",
                    "Engineered for high hip/knee mobility across crater slopes and rocky Jezero regolith.",
                    "Official NASA-3D-Resources Mark III Spacesuit GLB asset integrated at HDU EVA Suitport.",
                    "8.3 psi (57 kPa) O2 loop · Reduces pre-breathe overhead for rapid EVA sorties.",
                    "Official NASA Mark III prototype suit designed for planetary surface walking, kneeling, and rock coring.",
                    "https://github.com/nasa/NASA-3D-Resources",
                    "perseverance_sample_raw.jpg",
                    MarsInspectActionType.InspectOnly,
                    "Inspect NASA Mark III Suit",
                    6.5f
                );
            }

            // Ensure runtime controllers & EventSystem exist right inside the saved scene
            EnsureSingletonObject<MarsAudioManager>("MS_AudioManager");
            EnsureSingletonObject<MarsBaseFacilitiesBuilder>("MS_BaseFacilitiesBuilder");
            EnsureSingletonObject<MarsRoverVehicleController>("MS_Pressurized_Rover");
            EnsureSingletonObject<MarsAstronautController>("MS_Astronaut_Player");
            EnsureSingletonObject<MarsInteractionSystem>("MS_InteractionSystem");

            // Mount official NASA Astronaut GLB directly inside MS_Astronaut_Player so Windows player renders high-detail suit
            GameObject astroRoot = GameObject.Find("MS_Astronaut_Player");
            if (astroRoot != null)
            {
                Transform oldGlb = astroRoot.transform.Find("EVA_NASA_Astronaut_GLB");
                if (oldGlb != null) UnityEngine.Object.DestroyImmediate(oldGlb.gameObject);
                GameObject astroAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Spacecraft/Astronaut.glb");
                if (astroAsset != null)
                {
                    GameObject astroInst = (GameObject)PrefabUtility.InstantiatePrefab(astroAsset, astroRoot.transform);
                    if (astroInst == null) astroInst = UnityEngine.Object.Instantiate(astroAsset, astroRoot.transform);
                    astroInst.name = "EVA_NASA_Astronaut_GLB";
                    astroInst.transform.localPosition = Vector3.zero;
                    astroInst.transform.localRotation = Quaternion.identity;
                    astroInst.transform.localScale = Vector3.one;
                    foreach (Collider c in astroInst.GetComponentsInChildren<Collider>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                    }
                    Renderer[] rends = astroInst.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        float factor = 1.82f / Mathf.Max(0.05f, b.size.y);
                        astroInst.transform.localScale = Vector3.one * factor;
                        Bounds sb = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) sb.Encapsulate(rends[i].bounds);
                        astroInst.transform.position += new Vector3(
                            astroRoot.transform.position.x - sb.center.x,
                            astroRoot.transform.position.y - sb.min.y,
                            astroRoot.transform.position.z - sb.center.z);
                    }
                }
            }

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static void DestroyAllByName(string objectName)
        {
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (t != null && t.gameObject != null && t.gameObject.name == objectName)
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        private static GameObject PlaceGlbRaw(string assetPath, string instanceName, Transform parent)
        {
            GameObject old = GameObject.Find(instanceName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[MarsBaseCampusBuilder] Could not load GLB asset at {assetPath}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            if (instance == null) instance = UnityEngine.Object.Instantiate(asset, parent);
            instance.name = instanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static GameObject PlaceGlb(string assetPath, string instanceName, Transform parent, Vector3 worldPos, Vector3 eulerRot, float targetHeight)
        {
            GameObject old = GameObject.Find(instanceName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[MarsBaseCampusBuilder] Could not load GLB asset at {assetPath}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            if (instance == null) instance = UnityEngine.Object.Instantiate(asset, parent);
            instance.name = instanceName;
            instance.transform.position = worldPos;
            instance.transform.rotation = Quaternion.Euler(eulerRot);

            NormalizeEditorObject(instance, worldPos, targetHeight);
            return instance;
        }

        private static void NormalizeEditorObject(GameObject go, Vector3 targetWorldBasePos, float desiredHeight)
        {
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float currentH = Mathf.Max(0.05f, b.size.y);
            float scaleMult = desiredHeight / currentH;
            go.transform.localScale = go.transform.localScale * scaleMult;

            Bounds sb = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) sb.Encapsulate(rends[i].bounds);

            Vector3 delta = new Vector3(
                targetWorldBasePos.x - sb.center.x,
                targetWorldBasePos.y - sb.min.y,
                targetWorldBasePos.z - sb.center.z);
            go.transform.position += delta;
            Debug.Log($"[MarsBaseCampusBuilder] Placed {go.name} pos={go.transform.position} finalBounds={sb.size}");
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go;
        }

        private static void EnsureSingletonObject<T>(string name) where T : Component
        {
            T existing = UnityEngine.Object.FindAnyObjectByType<T>();
            if (existing == null)
            {
                GameObject go = GameObject.Find(name);
                if (go == null) go = new GameObject(name);
                go.AddComponent<T>();
            }
        }
    }
}
