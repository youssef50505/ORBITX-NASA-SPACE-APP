using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// High-fidelity visual polish, runtime fleet sanitization, synchronized diurnal lighting,
    /// atmospheric moon airlight, sleek orbital trajectories, HUD framing repair,
    /// and deterministic -captureShowcase diagnostic verification for Mars Sentinel.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class MarsVisualRealismPolish : MonoBehaviour
    {
        public static MarsVisualRealismPolish Instance { get; private set; }
        public bool IsExploreModeActive { get; private set; } = true;

        public static float SampleTerrainElevation(float x, float z)
        {
            return SampleHorizonHeightStatic(x, z);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePolishAttached()
        {
            if (FindFirstObjectByType<MarsVisualRealismPolish>() != null) return;
            var enhancer = FindFirstObjectByType<MarsVisualRealismEnhancer>();
            if (enhancer != null)
            {
                enhancer.gameObject.AddComponent<MarsVisualRealismPolish>();
            }
            else
            {
                var go = new GameObject("MS_VisualRealismPolish");
                go.AddComponent<MarsVisualRealismPolish>();
            }
        }

        private readonly Vector3 marsCenter = new Vector3(0f, 2400f, 0f);

        private MarsVisualRealismEnhancer enhancer;
        private MarsCelestialOrbitController celestialCtrl;
        private MarsCameraController cameraCtrl;
        private MarsSatelliteConstellation constellation;
        private CinemachineBrain cinemachineBrain;
        private Light sunLight;
        private Light spaceFillLight;
        private Material skyboxMat;
        private Material rimMat;

        private GameObject marsGlobe;
        private GameObject phobosObj;
        private GameObject deimosObj;
        private GameObject spaceSunObj;
        private GameObject horizonTerrainObj;
        private GameObject traverseRoutesObj;
        private GameObject waypointsObj;
        private Transform sunHaloQuadA;
        private Transform sunHaloQuadB;
        private Transform sunSpikeQuadA;
        private Transform sunSpikeQuadB;

        private CinemachineCamera orbitalCam;
        private CinemachineCamera surfaceCam;
        private CinemachineCamera persevCam;
        private CinemachineCamera ingenuityCam;
        private CinemachineCamera traverseCam;

        private const float PhobosOrbitRadius = 1340f;
        private const float DeimosOrbitRadius = 2060f;
        private const float PhobosInclDeg = 1.09f;
        private const float PhobosAscDeg = 22f;
        private const float DeimosInclDeg = 1.79f;
        private const float DeimosAscDeg = 38f;

        private readonly List<LineRenderer> allOrbitLines = new List<LineRenderer>();
        private readonly List<LineRenderer> legacyOrbitLines = new List<LineRenderer>();
        private readonly List<GameObject> orbiterRoots = new List<GameObject>();
        private readonly float[] orbiterInclinationsDeg = { 92.8f, 93.1f, 75.0f, 92.8f };
        private readonly float[] orbiterAscendingDeg = { 18f, 118f, 215f, 305f };
        private readonly float[] orbiterRadii = { 820f, 910f, 1080f, 980f };
        private readonly float[] orbiterSpeeds = { 0.42f, 0.36f, 0.25f, 0.39f };
        private readonly float[] orbiterPhaseOffsets = { 0.80f, 2.20f, 3.60f, 4.30f };
        private readonly Dictionary<LineRenderer, int> smoothedRouteCounts = new Dictionary<LineRenderer, int>();

        private Shader urpLitShader;
        private Shader spriteShader;
        private Material phobosMat;
        private Material deimosMat;
        private Light phobosCrescentLight;
        private Light deimosCrescentLight;
        private Texture2D orbitRibbonTex;

        private Text txtControlsLegend;
        private Text txtActiveCamera;
        private RectTransform statusPanelRect;

        private FieldInfo fiEnhancerSpaceBlend;
        private FieldInfo fiEnhancerOrbitOverride;
        private FieldInfo fiEnhancerFocusMode;
        private FieldInfo fiCelestialSolHour;
        private FieldInfo fiCelestialAutoAdvance;
        private FieldInfo fiCelestialPhobosRoot;
        private FieldInfo fiCelestialDeimosRoot;

        private bool initialized;
        private bool beaconsSanitized;
        private bool wasSpaceCamera;
        private bool forceOrbital;
        private int focusMode; // 0 = normal, 1 = Sun, 2 = Phobos, 3 = Deimos
        private int activeSurfaceKey = 1;
        private float spaceBlend;
        private float orbitYawOffset;
        private float orbitPitchOffset;
        private float orbitZoomMultiplier = 1f;
        private Vector3 lastAlignedSunDir = new Vector3(-0.34f, 0.25f, 0.91f).normalized;
        private readonly Vector3 orbitSunDirection = new Vector3(-0.92f, 0.31f, -0.24f).normalized;
        private string captureOutDir;
        private bool captureStarted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            ParseCliArgs();
            IsExploreModeActive = string.IsNullOrEmpty(captureOutDir);
        }

        private void Start()
        {
            InitializePolish();
        }

        private void ParseCliArgs()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                foreach (string arg in args)
                {
                    if (arg.StartsWith("-captureShowcase=", StringComparison.OrdinalIgnoreCase))
                    {
                        captureOutDir = arg.Substring("-captureShowcase=".Length).Trim().Trim((char)34);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore CLI access issues in restricted sandboxes
            }
        }

        private void InitializePolish()
        {
            if (initialized) return;
            initialized = true;

            enhancer = FindFirstObjectByType<MarsVisualRealismEnhancer>();
            celestialCtrl = FindFirstObjectByType<MarsCelestialOrbitController>();
            cameraCtrl = FindFirstObjectByType<MarsCameraController>();
            constellation = FindFirstObjectByType<MarsSatelliteConstellation>();
            cinemachineBrain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : FindFirstObjectByType<CinemachineBrain>();

            ResolveReflectionBindings();

            // Prevent legacy enhancer & constellation from overwriting polished moons, orbits, sky, and camera
            if (enhancer != null) enhancer.enabled = false;
            if (constellation != null) constellation.enabled = false;

            // Redirect MarsCelestialOrbitController moon roots to hidden proxies so [L] Sol Cycle keeps working without pulling moons underground
            if (celestialCtrl != null)
            {
                var proxyHost = new GameObject("MS_LegacyMoonProxies");
                proxyHost.hideFlags = HideFlags.HideInHierarchy;
                var pProxy = new GameObject("PhobosProxy").transform;
                var dProxy = new GameObject("DeimosProxy").transform;
                pProxy.SetParent(proxyHost.transform, false);
                dProxy.SetParent(proxyHost.transform, false);
                if (fiCelestialPhobosRoot != null) fiCelestialPhobosRoot.SetValue(celestialCtrl, pProxy);
                if (fiCelestialDeimosRoot != null) fiCelestialDeimosRoot.SetValue(celestialCtrl, dProxy);

                // Start at golden afternoon (~16.10h LMST) for dramatic terrain relief and visible Sun
                if (fiCelestialSolHour != null) fiCelestialSolHour.SetValue(celestialCtrl, 16.10f);
                if (fiCelestialAutoAdvance != null) fiCelestialAutoAdvance.SetValue(celestialCtrl, false);
            }

            LocateShadersAndMaterials();
            LocateCameras();
            LocateSunLight();

            FixHudLayoutAndLegend();
            UpgradeHorizonAndTerrain();
            SanitizeAndCompleteFleet();
            SanitizeBeaconsAndRoutes();
            UpgradeMoonsSunAndOrbits();
            FrameSurfaceOverviewCamera();

            if (!IsExploreModeActive)
            {
                if (cinemachineBrain != null) cinemachineBrain.enabled = true;
            }
            else
            {
                EnterExploreMode();
            }

            if (!string.IsNullOrEmpty(captureOutDir) && !captureStarted)
            {
                captureStarted = true;
                StartCoroutine(RunShowcaseCaptureRoutine());
            }
        }

        private void ResolveReflectionBindings()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            if (enhancer != null)
            {
                Type t = enhancer.GetType();
                fiEnhancerSpaceBlend = t.GetField("spaceBlend", flags);
                fiEnhancerOrbitOverride = t.GetField("orbitOverrideActive", flags);
                fiEnhancerFocusMode = t.GetField("focusTargetMode", flags);
            }
            if (celestialCtrl != null)
            {
                Type t = celestialCtrl.GetType();
                fiCelestialSolHour = t.GetField("solHour", flags);
                fiCelestialAutoAdvance = t.GetField("autoAdvanceTime", flags);
                fiCelestialPhobosRoot = t.GetField("phobosRoot", flags);
                fiCelestialDeimosRoot = t.GetField("deimosRoot", flags);
            }
        }

        private void LocateShadersAndMaterials()
        {
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null &&
                    r.sharedMaterial.shader.name.Contains("Universal Render Pipeline/Lit"))
                {
                    urpLitShader = r.sharedMaterial.shader;
                    break;
                }
            }
            if (urpLitShader == null) urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null) urpLitShader = Shader.Find("Standard");

            spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null) spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

            skyboxMat = RenderSettings.skybox;
            if (skyboxMat == null || skyboxMat.shader == null || !skyboxMat.shader.name.Contains("MartianAtmosphereSkybox"))
            {
                Shader skyShader = Shader.Find("MarsSentinel/MartianAtmosphereSkybox");
                if (skyShader != null)
                {
                    skyboxMat = new Material(skyShader);
                    RenderSettings.skybox = skyboxMat;
                }
            }

            rimMat = Resources.Load<Material>("Materials/M_MarsAtmosphereRim");
            if (rimMat == null)
            {
                Shader rimShader = Shader.Find("MarsSentinel/MarsAtmosphereRim");
                if (rimShader != null) rimMat = new Material(rimShader);
            }
        }

        private void LocateCameras()
        {
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                if (cam.name.Contains("Orbital")) orbitalCam = cam;
                else if (cam.name.Contains("SurfaceFleet")) surfaceCam = cam;
                else if (cam.name.Contains("Perseverance")) persevCam = cam;
                else if (cam.name.Contains("Ingenuity")) ingenuityCam = cam;
                else if (cam.name.Contains("Traverse")) traverseCam = cam;
            }
            if (Camera.main != null)
            {
                if (Camera.main.farClipPlane < 32000f) Camera.main.farClipPlane = 32000f;
                Camera.main.fieldOfView = 54f;
            }
        }

        private void FrameSurfaceOverviewCamera()
        {
            if (surfaceCam != null)
            {
                Vector3 camPos = new Vector3(10.5f, 3.6f, -24.5f);
                Vector3 lookTarget = new Vector3(-5.0f, 5.8f, 12.0f);
                surfaceCam.transform.position = camPos;
                surfaceCam.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                var lens = surfaceCam.Lens;
                lens.FieldOfView = 54f;
                lens.FarClipPlane = 32000f;
                surfaceCam.Lens = lens;
            }
            if (!forceOrbital && focusMode == 0 && activeSurfaceKey == 1 && Camera.main != null && surfaceCam != null)
            {
                Camera.main.transform.position = surfaceCam.transform.position;
                Camera.main.transform.rotation = surfaceCam.transform.rotation;
            }
        }

        private void LocateSunLight()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    break;
                }
            }
            if (sunLight != null)
            {
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = 0.78f;
            }
        }

        private void FixHudLayoutAndLegend()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Left Telemetry Panel
            GameObject leftGo = GameObject.Find("TelemetryPanel");
            if (leftGo != null)
            {
                RectTransform rt = leftGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(18f, -18f);
                    rt.sizeDelta = new Vector2(455f, 200f);
                }
            }

            // Center Top Title Banner (preserve modern 3-line slim 960x74 strip)
            GameObject topGo = GameObject.Find("VisorHeaderPanel") ?? GameObject.Find("TopBannerPanel");
            if (topGo != null)
            {
                RectTransform rt = topGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -14f);
                    rt.sizeDelta = new Vector2(960f, 74f);
                }
            }

            // Right Status & Controls Panel (fix pivot=(1,1) so zero right-edge clipping!)
            GameObject rightGo = GameObject.Find("StatusAndControlsPanel");
            if (rightGo != null)
            {
                statusPanelRect = rightGo.GetComponent<RectTransform>();
                if (statusPanelRect != null)
                {
                    statusPanelRect.anchorMin = new Vector2(1f, 1f);
                    statusPanelRect.anchorMax = new Vector2(1f, 1f);
                    statusPanelRect.pivot = new Vector2(1f, 1f);
                    statusPanelRect.anchoredPosition = new Vector2(-18f, -18f);
                    statusPanelRect.sizeDelta = new Vector2(435f, 245f);
                }

                bool assignedControls = false;
                foreach (Text t in rightGo.GetComponentsInChildren<Text>(true))
                {
                    bool isActiveCamField = t.name.IndexOf("ActiveCam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            t.text.StartsWith("ACTIVE VIEW", StringComparison.OrdinalIgnoreCase);
                    if (isActiveCamField)
                    {
                        txtActiveCamera = t;
                    }
                    else if (!assignedControls && (t.name.Contains("Legend") || t.text.Contains("CAMERAS:") || t.text.Contains("[1]")))
                    {
                        assignedControls = true;
                        txtControlsLegend = t;
                        t.gameObject.SetActive(true);
                        t.fontSize = 13;
                        t.lineSpacing = 1.05f;
                        t.verticalOverflow = VerticalWrapMode.Overflow;
                        t.horizontalOverflow = HorizontalWrapMode.Overflow;
                        t.text =
                            "EXPLORE & CAMERAS:\n" +
                            "[F] Explore  [WASD] Move  [E] Inspect/Rover  [V] Visor\n" +
                            "[1-4] Cams  [5/O] Orbit  [6-8] Sun/Moons  [B] Lamp  [K] Mute\n" +
                            "[H] Data Layers  [P] 1m Demo  [G] Geo  [D/C] Dust  [L] Sol";
                    }
                    else if (assignedControls && (t.text.Contains("CONTROLS:") || t.text.Contains("CAMERAS:")))
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }

        private float SampleHorizonHeight(float x, float z) => SampleHorizonHeightStatic(x, z);

        public static float SampleHorizonHeightStatic(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            // Level pad around landing fleet showcase (r < 16m) so every wheel rests flush, rising smoothly into Jezero dunes & mesa rim
            float padBlend = SmoothStep01(18f, 82f, r);
            float farRim = SmoothStep01(145f, 460f, r);

            float rollingDunes =
                Mathf.Sin(x * 0.032f + z * 0.019f) * 0.95f +
                Mathf.Cos(z * 0.044f - x * 0.021f) * 0.72f +
                Mathf.Sin((x + z) * 0.11f) * 0.18f;

            float ridgeAzimuth = Mathf.Atan2(z, x);
            float craterRimProfile =
                (Mathf.Sin(ridgeAzimuth * 3.0f + 0.5f) * 0.40f +
                 Mathf.Cos(ridgeAzimuth * 7.0f - 1.1f) * 0.22f +
                 Mathf.Sin(ridgeAzimuth * 13.0f + 2.2f) * 0.10f + 1.05f);

            float rimElevation = farRim * 22.0f * Mathf.Clamp(craterRimProfile, 0.42f, 1.55f);
            float undulationFade = SmoothStep01(12f, 32f, r);
            float baseUndulation = Mathf.Sin(x * 0.06f) * Mathf.Cos(z * 0.06f) * 0.05f * undulationFade;

            return Mathf.Lerp(baseUndulation, rollingDunes + rimElevation, padBlend);
        }

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            if (edge1 < edge0) return 1f - SmoothStep01(edge1, edge0, x);
            float denom = Mathf.Max(0.0001f, edge1 - edge0);
            float t = Mathf.Clamp01((x - edge0) / denom);
            return t * t * (3f - 2f * t);
        }

        private void UpgradeHorizonAndTerrain()
        {
            Texture2D regolithTex = BuildRegolithSandTexture(1024);
            Texture2D regolithBump = BuildRegolithNormalTexture(1024);

            GameObject horizonGo = GameObject.Find("MS_Jezero_Horizon_Terrain");
            if (horizonGo == null)
            {
                horizonGo = new GameObject("MS_Jezero_Horizon_Terrain");
            }
            horizonTerrainObj = horizonGo;
            var mf = horizonGo.GetComponent<MeshFilter>() ?? horizonGo.AddComponent<MeshFilter>();
            var mr = horizonGo.GetComponent<MeshRenderer>() ?? horizonGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildTerrainWithSkirtMesh(192, 1280f);
            var mc = horizonGo.GetComponent<MeshCollider>() ?? horizonGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;

            Material terrainMat = CreateLitMaterial(
                "MS_Polished_Regolith_Mat",
                new Color(0.96f, 0.90f, 0.84f, 1f),
                0.02f,
                0.09f,
                regolithTex,
                regolithBump,
                new Vector2(48f, 48f),
                1.18f
            );
            mr.sharedMaterial = terrainMat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            // Blend MS_Terrain_Jezero (6.36m STL square) cleanly into surrounding sand
            GameObject stlTerrain = GameObject.Find("MS_Terrain_Jezero");
            if (stlTerrain != null)
            {
                Renderer stlRend = stlTerrain.GetComponent<Renderer>();
                if (stlRend != null)
                {
                    Material stlMat = CreateLitMaterial(
                        "MS_STL_Regolith_Mat",
                        new Color(0.98f, 0.94f, 0.90f, 1f),
                        0.01f,
                        0.06f,
                        regolithTex,
                        regolithBump,
                        new Vector2(1.5f, 1.5f),
                        0.65f
                    );
                    stlRend.sharedMaterial = stlMat;
                }
                Vector3 sp = stlTerrain.transform.position;
                stlTerrain.transform.position = new Vector3(sp.x, -0.74f, sp.z);
            }

            // Replace smooth egg rock primitives from earlier pass with realistic faceted basalt boulders
            var oldRocks = new List<GameObject>();
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("MS_Basalt_Rock", StringComparison.OrdinalIgnoreCase))
                {
                    oldRocks.Add(t.gameObject);
                }
            }

            Material rockMat = CreateLitMaterial(
                "MS_Basalt_Boulder_Mat",
                new Color(0.32f, 0.25f, 0.22f, 1f),
                0.05f,
                0.16f,
                null,
                regolithBump,
                new Vector2(2f, 2f),
                0.75f
            );

            if (oldRocks.Count > 0)
            {
                Mesh[] rockVariants = new Mesh[4];
                for (int v = 0; v < 4; v++)
                {
                    rockVariants[v] = BuildFacetedBasaltMesh(101 + v * 37);
                }
                for (int i = 0; i < oldRocks.Count; i++)
                {
                    GameObject rk = oldRocks[i];
                    var rmf = rk.GetComponent<MeshFilter>();
                    var rmr = rk.GetComponent<MeshRenderer>();
                    if (rmf != null) rmf.sharedMesh = rockVariants[i % rockVariants.Length];
                    if (rmr != null) rmr.sharedMaterial = rockMat;
                    Vector3 p = rk.transform.position;
                    float gy = SampleHorizonHeight(p.x, p.z);
                    float sy = rk.transform.localScale.y;
                    rk.transform.position = new Vector3(p.x, gy + sy * 0.16f - 0.05f, p.z);
                }
            }
        }

        private void SanitizeBeaconsAndRoutes()
        {
            if (traverseRoutesObj == null) traverseRoutesObj = GameObject.Find("TraverseRoutesRoot");
            if (waypointsObj == null) waypointsObj = GameObject.Find("WaypointMarkersRoot");

            bool foundOctavia = false;
            foreach (string beaconName in new[] { "Octavia_E_Butler_Landing", "Science_Site_Rochette" })
            {
                GameObject b = GameObject.Find(beaconName);
                if (b != null)
                {
                    if (beaconName.Contains("Octavia")) foundOctavia = true;
                    Vector3 pos = b.transform.position;
                    float gy = SampleHorizonHeight(pos.x, pos.z);
                    // Flush precision survey disc marker on the regolith (no upright green pole!)
                    b.transform.position = new Vector3(pos.x, gy + 0.015f, pos.z);
                    b.transform.localScale = new Vector3(0.28f, 0.012f, 0.28f);
                    Renderer br = b.GetComponent<Renderer>();
                    if (br != null && br.sharedMaterial != null)
                    {
                        Color glow = beaconName.Contains("Octavia")
                            ? new Color(0.26f, 0.88f, 0.95f, 0.85f)
                            : new Color(0.95f, 0.74f, 0.28f, 0.85f);
                        if (br.material.HasProperty("_BaseColor")) br.material.SetColor("_BaseColor", glow);
                        if (br.material.HasProperty("_EmissionColor"))
                        {
                            br.material.EnableKeyword("_EMISSION");
                            br.material.SetColor("_EmissionColor", glow * 1.4f);
                        }
                    }
                }
            }

            // Refine jagged A* grid & historical route strips into sleek smooth surface spline overlays
            foreach (LineRenderer lr in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
            {
                string ln = lr.name.ToLowerInvariant();
                if (ln.Contains("orbit") || ln.Contains("trail")) continue;
                int count = lr.positionCount;
                if (count < 2) continue;

                if (smoothedRouteCounts.TryGetValue(lr, out int prevCount) && prevCount == count)
                {
                    continue;
                }

                bool isIngenuity = ln.Contains("ingenuity");
                float targetW = lr.startWidth;
                if (ln.Contains("safest")) targetW = 0.55f;
                else if (ln.Contains("shortest")) targetW = 0.38f;
                else if (ln.Contains("science")) targetW = 0.42f;
                else if (isIngenuity) targetW = 0.28f;
                else if (ln.Contains("persev")) targetW = 0.32f;
                else targetW = 0.26f;

                lr.startWidth = targetW;
                lr.endWidth = targetW;
                lr.numCornerVertices = 2;
                lr.numCapVertices = 2;

                if (!lr.useWorldSpace)
                {
                    Transform t = lr.transform;
                    for (int k = 0; k < count; k++)
                    {
                        lr.SetPosition(k, t.TransformPoint(lr.GetPosition(k)));
                    }
                    lr.useWorldSpace = true;
                }

                var raw = new Vector3[count];
                for (int k = 0; k < count; k++) raw[k] = lr.GetPosition(k);

                // Soften overly harsh neon unlit beam colors into crisp scientific survey overlays
                if (lr.sharedMaterial != null && lr.sharedMaterial.HasProperty("_BaseColor"))
                {
                    Color sc = lr.startColor;
                    sc.a = isIngenuity ? 0.65f : 0.78f;
                    lr.startColor = sc;
                    lr.endColor = sc;
                }

                Vector3[] smoothPts = SmoothRoutePoints(raw, isIngenuity);
                lr.positionCount = smoothPts.Length;
                lr.SetPositions(smoothPts);
                smoothedRouteCounts[lr] = smoothPts.Length;
            }

            if (foundOctavia) beaconsSanitized = true;
        }

        private Vector3[] SmoothRoutePoints(Vector3[] raw, bool preserveHeight)
        {
            if (raw == null || raw.Length <= 2)
            {
                if (raw == null) return Array.Empty<Vector3>();
                var flat = new Vector3[raw.Length];
                for (int i = 0; i < raw.Length; i++)
                {
                    float y = preserveHeight ? Mathf.Max(raw[i].y, SampleTerrainElevation(raw[i].x, raw[i].z) + 0.35f) : SampleTerrainElevation(raw[i].x, raw[i].z) + 0.35f;
                    flat[i] = new Vector3(raw[i].x, y, raw[i].z);
                }
                return flat;
            }

            var dedup = new List<Vector3>(raw.Length) { raw[0] };
            float minDedupDist = preserveHeight ? 0.25f : 0.55f;
            for (int i = 1; i < raw.Length - 1; i++)
            {
                Vector2 prev = new Vector2(dedup[dedup.Count - 1].x, dedup[dedup.Count - 1].z);
                Vector2 cur = new Vector2(raw[i].x, raw[i].z);
                if (Vector2.Distance(prev, cur) > minDedupDist)
                {
                    dedup.Add(raw[i]);
                }
            }
            dedup.Add(raw[raw.Length - 1]);
            if (dedup.Count <= 2)
            {
                for (int i = 0; i < dedup.Count; i++)
                {
                    Vector3 p = dedup[i];
                    float y = preserveHeight ? Mathf.Max(p.y, SampleTerrainElevation(p.x, p.z) + 0.35f) : SampleTerrainElevation(p.x, p.z) + 0.35f;
                    dedup[i] = new Vector3(p.x, y, p.z);
                }
                return dedup.ToArray();
            }

            var relaxed = new List<Vector3>(dedup);
            for (int pass = 0; pass < 5; pass++)
            {
                var next = new List<Vector3>(relaxed.Count * 2) { relaxed[0] };
                for (int i = 0; i < relaxed.Count - 1; i++)
                {
                    Vector3 a = relaxed[i];
                    Vector3 b = relaxed[i + 1];
                    if (i > 0) next.Add(Vector3.Lerp(a, b, 0.25f));
                    if (i < relaxed.Count - 2) next.Add(Vector3.Lerp(a, b, 0.75f));
                }
                next.Add(relaxed[relaxed.Count - 1]);
                relaxed = next;
            }

            // Uniform arc-length resample so Laplacian diffusion rounds long straight hairpins evenly
            float totalLen = 0f;
            for (int i = 0; i < relaxed.Count - 1; i++)
            {
                totalLen += Vector3.Distance(relaxed[i], relaxed[i + 1]);
            }
            float targetStep = Mathf.Max(preserveHeight ? 0.60f : 0.85f, totalLen / 460f);
            if (totalLen > targetStep * 2f)
            {
                var uniform = new List<Vector3>(Mathf.Min(500, Mathf.CeilToInt(totalLen / targetStep) + 2)) { relaxed[0] };
                for (int i = 0; i < relaxed.Count - 1; i++)
                {
                    Vector3 a = relaxed[i];
                    Vector3 b = relaxed[i + 1];
                    float segLen = Vector3.Distance(a, b);
                    int segSubs = Mathf.Clamp(Mathf.CeilToInt(segLen / targetStep), 1, 24);
                    for (int s = 1; s <= segSubs; s++)
                    {
                        uniform.Add(Vector3.Lerp(a, b, s / (float)segSubs));
                    }
                }
                relaxed = uniform;
            }

            for (int smoothPass = 0; smoothPass < 10 && relaxed.Count >= 4; smoothPass++)
            {
                var copy = new List<Vector3>(relaxed);
                for (int i = 1; i < relaxed.Count - 1; i++)
                {
                    copy[i] = 0.25f * relaxed[i - 1] + 0.50f * relaxed[i] + 0.25f * relaxed[i + 1];
                }
                relaxed = copy;
            }

            int n = relaxed.Count;
            var spline = new List<Vector3>(n * 4);
            const int subDiv = 4;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = relaxed[Mathf.Max(0, i - 1)];
                Vector3 p1 = relaxed[i];
                Vector3 p2 = relaxed[i + 1];
                Vector3 p3 = relaxed[Mathf.Min(n - 1, i + 2)];
                for (int s = 0; s < subDiv; s++)
                {
                    float u = s / (float)subDiv;
                    float u2 = u * u;
                    float u3 = u2 * u;
                    Vector3 pt = 0.5f * (
                        (2f * p1) +
                        (-p0 + p2) * u +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * u3
                    );
                    float gy = SampleTerrainElevation(pt.x, pt.z) + 0.35f;
                    pt.y = preserveHeight ? Mathf.Max(pt.y, gy) : gy;
                    spline.Add(pt);
                }
            }
            Vector3 last = relaxed[n - 1];
            float lastGy = SampleTerrainElevation(last.x, last.z) + 0.35f;
            last.y = preserveHeight ? Mathf.Max(last.y, lastGy) : lastGy;
            spline.Add(last);

            const int maxLinePts = 320;
            if (spline.Count > maxLinePts)
            {
                var capped = new Vector3[maxLinePts];
                for (int k = 0; k < maxLinePts; k++)
                {
                    int srcIdx = Mathf.RoundToInt((k / (float)(maxLinePts - 1)) * (spline.Count - 1));
                    capped[k] = spline[srcIdx];
                }
                return capped;
            }
            return spline.ToArray();
        }

        private Mesh BuildTerrainWithSkirtMesh(int gridN, float sizeMeters)
        {
            int stride = gridN + 1;
            int topVerts = stride * stride;
            int skirtRingVerts = gridN * 4;
            int totalVerts = topVerts + skirtRingVerts;

            var verts = new Vector3[totalVerts];
            var uvs = new Vector2[totalVerts];
            var tris = new List<int>(gridN * gridN * 6 + skirtRingVerts * 6);

            float half = sizeMeters * 0.5f;
            float step = sizeMeters / gridN;

            for (int z = 0; z < stride; z++)
            {
                float wz = -half + z * step;
                for (int x = 0; x < stride; x++)
                {
                    float wx = -half + x * step;
                    int idx = z * stride + x;
                    verts[idx] = new Vector3(wx, SampleHorizonHeight(wx, wz), wz);
                    uvs[idx] = new Vector2(x / (float)gridN, z / (float)gridN);
                }
            }

            for (int z = 0; z < gridN; z++)
            {
                for (int x = 0; x < gridN; x++)
                {
                    int i0 = z * stride + x;
                    int i1 = i0 + 1;
                    int i2 = (z + 1) * stride + x;
                    int i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            // Perimeter downward skirt (-55m) around all 4 edges to eliminate horizon slits forever
            var rimIndices = new List<int>(skirtRingVerts);
            for (int x = 0; x < gridN; x++) rimIndices.Add(x); // bottom edge
            for (int z = 0; z < gridN; z++) rimIndices.Add(z * stride + gridN); // right edge
            for (int x = gridN; x > 0; x--) rimIndices.Add(gridN * stride + x); // top edge
            for (int z = gridN; z > 0; z--) rimIndices.Add(z * stride); // left edge

            for (int i = 0; i < rimIndices.Count; i++)
            {
                int topIdx = rimIndices[i];
                Vector3 tp = verts[topIdx];
                verts[topVerts + i] = new Vector3(tp.x, tp.y - 55f, tp.z);
                uvs[topVerts + i] = uvs[topIdx];
            }

            for (int i = 0; i < rimIndices.Count; i++)
            {
                int next = (i + 1) % rimIndices.Count;
                int t0 = rimIndices[i];
                int t1 = rimIndices[next];
                int b0 = topVerts + i;
                int b1 = topVerts + next;
                tris.Add(t0); tris.Add(t1); tris.Add(b0);
                tris.Add(t1); tris.Add(b1); tris.Add(b0);
            }

            var mesh = new Mesh { name = "MS_Jezero_Horizon_SkirtMesh", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void SanitizeAndCompleteFleet()
        {
            Material goldFoil = CreateLitMaterial("MS_GoldKapton", new Color(0.86f, 0.65f, 0.20f, 1f), 0.78f, 0.72f);
            Material solarBlue = CreateLitMaterial("MS_SolarCell", new Color(0.08f, 0.16f, 0.34f, 1f), 0.35f, 0.78f);
            Material whiteThermal = CreateLitMaterial("MS_WhiteMLI", new Color(0.88f, 0.87f, 0.85f, 1f), 0.12f, 0.45f);
            Material darkCarbon = CreateLitMaterial("MS_CarbonFrame", new Color(0.16f, 0.17f, 0.19f, 1f), 0.45f, 0.55f);

            // 1. Clean Blender helper boxes/shadow planes baked into GLBs (Curiosity shadow_arm, Cube_01_p, etc.)
            string[] fleetNames =
            {
                "MS_Perseverance_Root", "MS_Curiosity_Root", "MS_Opportunity_Root",
                "MS_Viking_Lander_Root", "MS_InSight_Lander_Root", "MS_Ingenuity_Root"
            };

            foreach (string rootName in fleetNames)
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null) continue;

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    int vCount = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 9999;
                    bool keepCuriosityCam = rootName == "MS_Curiosity_Root" &&
                        (n.StartsWith("mastcam") || n.StartsWith("navcam") || n.StartsWith("hazcam"));
                    bool isHelper = !keepCuriosityCam && (
                        n.StartsWith("shadow") || n.StartsWith("cube_0") || n.EndsWith("_p") ||
                        n.Contains("_root_p") || n.Contains("pivot") || n.Contains("empty") ||
                        n.StartsWith("hazcam") || n.StartsWith("mastcam") || n.StartsWith("navcam") ||
                        n.StartsWith("mahli_cam") || n == "mardi" ||
                        (vCount <= 24 && (n.Contains("cam") || n.Contains("box") || n.Contains("dummy"))) ||
                        (r.gameObject == root && n.Contains("curiosity")));
                    if (isHelper)
                    {
                        r.enabled = false;
                    }
                }
            }

            // 1b. Hide legacy low-poly placeholder rover/boulder meshes inside MARS_SENTINEL_HERO_WEB so authentic NASA GLBs shine
            GameObject heroWeb = GameObject.Find("MARS_SENTINEL_HERO_WEB");
            if (heroWeb != null)
            {
                foreach (Renderer r in heroWeb.GetComponentsInChildren<Renderer>(true))
                {
                    string hn = r.gameObject.name.ToLowerInvariant();
                    if (hn.Contains("rover") || hn.Contains("persev") || hn.Contains("mars_globe") || hn.Contains("phobos") || hn.Contains("deimos"))
                    {
                        r.enabled = false;
                    }
                }
            }

            // 2. Seat MS_Perseverance_Root cleanly
            GameObject persev = GameObject.Find("MS_Perseverance_Root");
            if (persev != null)
            {
                PolishImportedFleetMaterials(persev, false);
                SeatRootOnTerrain(persev);
            }

            // 3. Seat MS_Curiosity_Root cleanly at (-7.5, y, -8.5) in level 3/4 side profile
            //    Test both pitch signs to guarantee front/rear axle equilibrium, plus align wheels flush to sand
            GameObject curiosity = GameObject.Find("MS_Curiosity_Root");
            if (curiosity != null)
            {
                curiosity.transform.position = new Vector3(-7.5f, 0f, -8.5f);
                curiosity.transform.rotation = Quaternion.Euler(0f, 34f, 0f);
                NormalizeFleetScale(curiosity, 2.8f);
                PolishImportedFleetMaterials(curiosity, true);
                LevelCuriosityWheels(curiosity);
                SeatRootOnTerrain(curiosity);
            }

            // 4. Fix MS_Opportunity_Root (mars-exploration-rover-opportunity-mer-b.glb was 90deg tipped mast artifact)
            GameObject opp = GameObject.Find("MS_Opportunity_Root");
            if (opp != null)
            {
                foreach (Transform ch in opp.transform)
                {
                    // Disable incomplete/tilted raw polySurface mast artifact so clean NASA MER rover replaces it
                    foreach (Renderer r in ch.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                opp.transform.position = new Vector3(-13.5f, 0f, 1.5f);
                opp.transform.rotation = Quaternion.Euler(0f, 42f, 0f);
                BuildOpportunityRoverMesh(opp.transform, goldFoil, solarBlue, whiteThermal, darkCarbon);
                SeatRootOnTerrain(opp);
            }

            // 5. Fix MS_Viking_Lander_Root (polySurfa1/2 boom artifact shrank Viking to a spindly 1-pixel stick)
            GameObject viking = GameObject.Find("MS_Viking_Lander_Root");
            if (viking != null)
            {
                foreach (Renderer r in viking.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                viking.transform.position = new Vector3(2.8f, 0f, 6.2f);
                viking.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
                BuildVikingLanderMesh(viking.transform, goldFoil, whiteThermal, darkCarbon);
                SeatRootOnTerrain(viking);
            }

            // 6. Populate MS_InSight_Lander_Root (was completely empty!)
            GameObject insight = GameObject.Find("MS_InSight_Lander_Root");
            if (insight != null)
            {
                insight.transform.position = new Vector3(-11.0f, 0f, 9.5f);
                insight.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
                if (insight.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    BuildInSightLanderMesh(insight.transform, goldFoil, solarBlue, whiteThermal, darkCarbon);
                }
                SeatRootOnTerrain(insight);
            }

            // 7. Complete MS_Ingenuity_Root (had only 2 hovering BladeMesh cubes; keep rotors & add fuselage/solar/legs)
            GameObject ing = GameObject.Find("MS_Ingenuity_Root");
            if (ing != null)
            {
                ing.transform.position = new Vector3(4.5f, 0f, 2.5f);
                if (ing.transform.Find("MS_Ingenuity_Body") == null)
                {
                    BuildIngenuityBody(ing.transform, goldFoil, solarBlue, darkCarbon);
                }
                float gy = SampleHorizonHeight(ing.transform.position.x, ing.transform.position.z);
                ing.transform.position = new Vector3(4.5f, gy + 2.0f, 2.5f);
            }
        }

        private void PolishImportedFleetMaterials(GameObject root, bool liftDarkChassis)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                string rn = r.gameObject.name.ToLowerInvariant();
                bool isDarkIntentional = rn.Contains("wheel") || rn.Contains("tire") || rn.Contains("tread") ||
                                         rn.Contains("solar") || rn.Contains("lens");
                Material[] mats = r.materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > 0.28f)
                    {
                        m.SetFloat("_Metallic", 0.24f);
                        changed = true;
                    }
                    if (m.HasProperty("metallicFactor") && m.GetFloat("metallicFactor") > 0.28f)
                    {
                        m.SetFloat("metallicFactor", 0.24f);
                        changed = true;
                    }
                    if (m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > 0.48f)
                    {
                        m.SetFloat("_Smoothness", 0.42f);
                        changed = true;
                    }
                    if (m.HasProperty("roughnessFactor") && m.GetFloat("roughnessFactor") < 0.55f)
                    {
                        m.SetFloat("roughnessFactor", 0.58f);
                        changed = true;
                    }
                    if (liftDarkChassis && !isDarkIntentional)
                    {
                        if (m.HasProperty("_BaseColor"))
                        {
                            Color c = m.GetColor("_BaseColor");
                            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                            if (lum < 0.34f && m.GetTexture("_BaseMap") == null)
                            {
                                float boost = 0.54f / Mathf.Max(0.08f, lum);
                                m.SetColor("_BaseColor", new Color(
                                    Mathf.Clamp01(c.r * boost + 0.12f),
                                    Mathf.Clamp01(c.g * boost + 0.10f),
                                    Mathf.Clamp01(c.b * boost + 0.085f),
                                    1f));
                                changed = true;
                            }
                            else if (m.GetTexture("_BaseMap") != null)
                            {
                                m.SetColor("_BaseColor", new Color(
                                    Mathf.Max(c.r, 1.0f),
                                    Mathf.Max(c.g, 0.96f),
                                    Mathf.Max(c.b, 0.92f),
                                    1f));
                                changed = true;
                            }
                        }
                        if (m.HasProperty("baseColorFactor"))
                        {
                            Color c = m.GetColor("baseColorFactor");
                            Texture tex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
                            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                            if (lum < 0.45f && tex == null)
                            {
                                float boost = 0.65f / Mathf.Max(0.08f, lum);
                                m.SetColor("baseColorFactor", new Color(
                                    Mathf.Clamp01(c.r * boost + 0.16f),
                                    Mathf.Clamp01(c.g * boost + 0.14f),
                                    Mathf.Clamp01(c.b * boost + 0.11f),
                                    1f));
                                changed = true;
                            }
                            else
                            {
                                m.SetColor("baseColorFactor", new Color(
                                    Mathf.Max(c.r, 1.0f),
                                    Mathf.Max(c.g, 0.97f),
                                    Mathf.Max(c.b, 0.93f),
                                    1f));
                                changed = true;
                            }
                        }
                        if (m.HasProperty("_EmissionColor"))
                        {
                            m.EnableKeyword("_EMISSION");
                            Color emitCol = new Color(0.16f, 0.13f, 0.105f, 1f);
                            m.SetColor("_EmissionColor", emitCol);
                            changed = true;
                        }
                        if (m.HasProperty("emissiveFactor"))
                        {
                            m.SetColor("emissiveFactor", new Color(0.24f, 0.21f, 0.17f, 1f));
                            changed = true;
                        }
                    }
                }
                if (changed) r.materials = mats;
            }
        }

        private void LevelCuriosityWheels(GameObject curiosity)
        {
            if (curiosity == null) return;

            // Keep mast upright and preserve level rover wheels without suspension camber tilt
            Transform mastPivot = null;
            var wheels = new List<Transform>();
            var wheelRenderers = new List<Renderer>();

            foreach (Transform t in curiosity.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mast_p")
                {
                    mastPivot = t;
                }
                else if (t.name.StartsWith("wheel_0", StringComparison.OrdinalIgnoreCase))
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null && r.enabled)
                    {
                        wheels.Add(t);
                        wheelRenderers.Add(r);
                    }
                }
            }

            if (mastPivot != null)
            {
                mastPivot.localRotation = Quaternion.identity;
            }

            Vector3 euler = curiosity.transform.rotation.eulerAngles;
            Quaternion uprightRot = Quaternion.Euler(0f, euler.y, 0f);
            curiosity.transform.rotation = uprightRot;

            for (int i = 0; i < wheels.Count; i++)
            {
                Transform w = wheels[i];
                if (w.parent != null && w.parent.name.StartsWith("suspension_steer_", StringComparison.OrdinalIgnoreCase))
                {
                    w.parent.rotation = uprightRot;
                }
                w.rotation = uprightRot;
            }

            float minWheelY = float.MaxValue;
            for (int i = 0; i < wheelRenderers.Count; i++)
            {
                float wy = wheelRenderers[i].bounds.min.y;
                if (wy < minWheelY) minWheelY = wy;
            }
            if (wheels.Count == 0 || minWheelY > 1e6f) return;

            for (int i = 0; i < wheels.Count; i++)
            {
                float dy = minWheelY - wheelRenderers[i].bounds.min.y;
                if (Mathf.Abs(dy) > 0.001f && Mathf.Abs(dy) < 0.80f)
                {
                    Transform target = (wheels[i].parent != null &&
                        wheels[i].parent.name.StartsWith("suspension_steer_", StringComparison.OrdinalIgnoreCase))
                        ? wheels[i].parent
                        : wheels[i];
                    target.position += new Vector3(0f, dy, 0f);
                }
            }
        }

        private void NormalizeFleetScale(GameObject root, float targetMaxDim)
        {
            if (TryGetActiveBounds(root, out Bounds b))
            {
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.05f && (maxDim > targetMaxDim * 1.45f || maxDim < targetMaxDim * 0.45f))
                {
                    float factor = targetMaxDim / maxDim;
                    root.transform.localScale *= factor;
                }
            }
        }

        private void SeatRootOnTerrain(GameObject root)
        {
            Vector3 p = root.transform.position;
            float gy = SampleHorizonHeight(p.x, p.z);
            float minWheelY = float.MaxValue;
            float minAnyY = float.MaxValue;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (r == null || !r.enabled || mf.sharedMesh == null) continue;
                string fn = mf.gameObject.name.ToLowerInvariant();
                bool isWheelOrFoot = fn.Contains("wheel") || fn.Contains("tire") || fn.Contains("foot") ||
                                     fn.Contains("pad") || fn.Contains("skid") || fn.Contains("leg");
                Vector3[] verts = mf.sharedMesh.vertices;
                Matrix4x4 l2w = mf.transform.localToWorldMatrix;
                int step = verts.Length <= 4000 ? 1 : Mathf.Max(1, verts.Length / 1000);
                for (int i = 0; i < verts.Length; i += step)
                {
                    float wy = l2w.MultiplyPoint3x4(verts[i]).y;
                    if (wy < minAnyY) minAnyY = wy;
                    if (isWheelOrFoot && wy < minWheelY) minWheelY = wy;
                }
            }
            float refMinY = minWheelY < float.MaxValue * 0.5f ? minWheelY : minAnyY;
            if (refMinY < float.MaxValue * 0.5f)
            {
                // Sink 8mm into loose Martian sand so wheels & footpads show grounded contact with full tire profile
                root.transform.position = new Vector3(p.x, p.y + (gy - refMinY - 0.008f), p.z);
            }
            else if (TryGetActiveBounds(root, out Bounds b))
            {
                root.transform.position = new Vector3(p.x, p.y + (gy - b.min.y - 0.008f), p.z);
            }
            else
            {
                root.transform.position = new Vector3(p.x, gy, p.z);
            }
        }

        private bool TryGetActiveBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool hasAny = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasAny)
                {
                    bounds = r.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return hasAny;
        }

        private void BuildOpportunityRoverMesh(Transform parent, Material gold, Material solar, Material whiteMat, Material dark)
        {
            var container = new GameObject("MS_Opportunity_Detailed");
            container.transform.SetParent(parent, false);

            CreatePart(PrimitiveType.Cube, container.transform, "WarmElectronicsBox", new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.85f, 0.42f, 0.95f), gold);
            CreatePart(PrimitiveType.Cube, container.transform, "SolarDeck_Center", new Vector3(0f, 0.78f, -0.05f), Vector3.zero, new Vector3(1.15f, 0.05f, 1.35f), solar);
            CreatePart(PrimitiveType.Cube, container.transform, "SolarWing_L", new Vector3(-0.92f, 0.78f, -0.10f), new Vector3(0f, 0f, 6f), new Vector3(0.85f, 0.04f, 1.10f), solar);
            CreatePart(PrimitiveType.Cube, container.transform, "SolarWing_R", new Vector3(0.92f, 0.78f, -0.10f), new Vector3(0f, 0f, -6f), new Vector3(0.85f, 0.04f, 1.10f), solar);
            CreatePart(PrimitiveType.Cylinder, container.transform, "PancamMast", new Vector3(0.18f, 1.18f, 0.34f), Vector3.zero, new Vector3(0.08f, 0.42f, 0.08f), whiteMat);
            CreatePart(PrimitiveType.Cube, container.transform, "PancamHead", new Vector3(0.18f, 1.58f, 0.36f), Vector3.zero, new Vector3(0.34f, 0.14f, 0.18f), whiteMat);
            CreatePart(PrimitiveType.Cylinder, container.transform, "HGA_Dish", new Vector3(-0.22f, 1.02f, -0.35f), new Vector3(25f, 0f, 0f), new Vector3(0.34f, 0.04f, 0.34f), whiteMat);

            float[] wx = { -0.56f, 0.56f };
            float[] wz = { -0.58f, 0f, 0.58f };
            for (int xi = 0; xi < wx.Length; xi++)
            {
                for (int zi = 0; zi < wz.Length; zi++)
                {
                    CreatePart(PrimitiveType.Cylinder, container.transform, $"Wheel_{xi}_{zi}",
                        new Vector3(wx[xi], 0.16f, wz[zi]), new Vector3(0f, 0f, 90f), new Vector3(0.28f, 0.10f, 0.28f), dark);
                }
            }
        }

        private void BuildVikingLanderMesh(Transform parent, Material gold, Material whiteMat, Material dark)
        {
            var container = new GameObject("MS_Viking_Detailed");
            container.transform.SetParent(parent, false);

            CreatePart(PrimitiveType.Cylinder, container.transform, "Viking_HexBody", new Vector3(0f, 0.82f, 0f), Vector3.zero, new Vector3(1.55f, 0.28f, 1.35f), whiteMat);
            CreatePart(PrimitiveType.Cube, container.transform, "Viking_RTG_L", new Vector3(-0.78f, 0.88f, 0f), Vector3.zero, new Vector3(0.45f, 0.26f, 0.58f), gold);
            CreatePart(PrimitiveType.Cube, container.transform, "Viking_RTG_R", new Vector3(0.78f, 0.88f, 0f), Vector3.zero, new Vector3(0.45f, 0.26f, 0.58f), gold);
            CreatePart(PrimitiveType.Cylinder, container.transform, "Viking_HGA_Mast", new Vector3(-0.18f, 1.35f, -0.10f), Vector3.zero, new Vector3(0.07f, 0.35f, 0.07f), dark);
            CreatePart(PrimitiveType.Cylinder, container.transform, "Viking_HGA_Dish", new Vector3(-0.18f, 1.66f, -0.10f), new Vector3(26f, 0f, 0f), new Vector3(0.88f, 0.04f, 0.88f), whiteMat);
            CreatePart(PrimitiveType.Cylinder, container.transform, "Viking_MetMast", new Vector3(0.42f, 1.32f, 0.32f), Vector3.zero, new Vector3(0.05f, 0.34f, 0.05f), whiteMat);

            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                Vector3 footPos = new Vector3(Mathf.Cos(a) * 1.12f, 0.06f, Mathf.Sin(a) * 1.12f);
                CreatePart(PrimitiveType.Cylinder, container.transform, $"Footpad_{k}", footPos, Vector3.zero, new Vector3(0.36f, 0.05f, 0.36f), dark);
                Vector3 hip = new Vector3(Mathf.Cos(a) * 0.62f, 0.72f, Mathf.Sin(a) * 0.62f);
                var leg = CreatePart(PrimitiveType.Cylinder, container.transform, $"Leg_{k}", (hip + footPos) * 0.5f, Vector3.zero, new Vector3(0.09f, 0.44f, 0.09f), dark);
                leg.transform.up = (hip - footPos).normalized;
            }
        }

        private void BuildInSightLanderMesh(Transform parent, Material gold, Material solar, Material whiteMat, Material dark)
        {
            var container = new GameObject("MS_InSight_Detailed");
            container.transform.SetParent(parent, false);

            CreatePart(PrimitiveType.Cylinder, container.transform, "LanderDeck", new Vector3(0f, 0.76f, 0f), Vector3.zero, new Vector3(1.55f, 0.22f, 1.55f), gold);
            CreatePart(PrimitiveType.Cylinder, container.transform, "SolarArray_L", new Vector3(-1.95f, 0.86f, 0f), Vector3.zero, new Vector3(2.15f, 0.04f, 2.15f), solar);
            CreatePart(PrimitiveType.Cylinder, container.transform, "SolarArray_R", new Vector3(1.95f, 0.86f, 0f), Vector3.zero, new Vector3(2.15f, 0.04f, 2.15f), solar);
            CreatePart(PrimitiveType.Cube, container.transform, "SolarSpar", new Vector3(0f, 0.82f, 0f), Vector3.zero, new Vector3(2.6f, 0.06f, 0.12f), dark);
            CreatePart(PrimitiveType.Sphere, container.transform, "SEIS_Dome", new Vector3(0.85f, 0.16f, 1.35f), Vector3.zero, new Vector3(0.46f, 0.34f, 0.46f), whiteMat);
            CreatePart(PrimitiveType.Cylinder, container.transform, "UHF_Antenna", new Vector3(-0.32f, 1.25f, -0.25f), Vector3.zero, new Vector3(0.06f, 0.35f, 0.06f), whiteMat);

            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                Vector3 footPos = new Vector3(Mathf.Cos(a) * 1.05f, 0.05f, Mathf.Sin(a) * 1.05f);
                CreatePart(PrimitiveType.Cylinder, container.transform, $"Footpad_{k}", footPos, Vector3.zero, new Vector3(0.32f, 0.04f, 0.32f), dark);
                Vector3 legMid = Vector3.Lerp(new Vector3(Mathf.Cos(a) * 0.6f, 0.65f, Mathf.Sin(a) * 0.6f), footPos, 0.5f);
                var leg = CreatePart(PrimitiveType.Cylinder, container.transform, $"Leg_{k}", legMid, Vector3.zero, new Vector3(0.08f, 0.42f, 0.08f), dark);
                leg.transform.up = (new Vector3(Mathf.Cos(a) * 0.6f, 0.65f, Mathf.Sin(a) * 0.6f) - footPos).normalized;
            }
        }

        private void BuildIngenuityBody(Transform parent, Material gold, Material solar, Material dark)
        {
            var body = new GameObject("MS_Ingenuity_Body");
            body.transform.SetParent(parent, false);
            CreatePart(PrimitiveType.Cylinder, body.transform, "RotorMast", new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(0.05f, 0.36f, 0.05f), dark);
            CreatePart(PrimitiveType.Cube, body.transform, "SolarPanelTop", new Vector3(0f, 1.05f, 0f), Vector3.zero, new Vector3(0.42f, 0.02f, 0.18f), solar);
            CreatePart(PrimitiveType.Cube, body.transform, "FuselageBox", new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(0.20f, 0.16f, 0.16f), gold);

            Vector3[] legDirs =
            {
                new Vector3(0.24f, 0f, 0.24f), new Vector3(-0.24f, 0f, 0.24f),
                new Vector3(0.24f, 0f, -0.24f), new Vector3(-0.24f, 0f, -0.24f)
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 top = new Vector3(0f, 0.38f, 0f);
                Vector3 bot = legDirs[i];
                var leg = CreatePart(PrimitiveType.Cylinder, body.transform, $"SkidLeg_{i}", (top + bot) * 0.5f, Vector3.zero, new Vector3(0.025f, 0.28f, 0.025f), dark);
                leg.transform.up = (top - bot).normalized;
            }
        }

        private void UpgradeMoonsSunAndOrbits()
        {
            // Disable duplicate legacy enhancer Sun & legacy orbit trails cleanly
            GameObject legacySun = GameObject.Find("MS_Visible_Solar_Photosphere");
            if (legacySun != null) legacySun.SetActive(false);

            legacyOrbitLines.Clear();
            foreach (LineRenderer existingLr in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
            {
                string ln = existingLr.name.ToLowerInvariant();
                if (ln.Contains("orbit") || ln.Contains("trail"))
                {
                    existingLr.enabled = false;
                    legacyOrbitLines.Add(existingLr);
                }
            }

            // 1. Mars Orbital Globe & Atmospheric Shell (1024x512 crisp planetary relief, strictly centered at marsCenter!)
            marsGlobe = GameObject.Find("MS_Mars_Orbital_Globe");
            if (marsGlobe == null)
            {
                marsGlobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marsGlobe.name = "MS_Mars_Orbital_Globe";
                DestroyImmediate(marsGlobe.GetComponent<Collider>());
            }
            marsGlobe.transform.position = marsCenter;
            marsGlobe.transform.rotation = Quaternion.Euler(23.9f, 35f, 0f);
            marsGlobe.transform.localScale = Vector3.one * 1040f; // diameter 1040 (radius 520)

            Texture2D marsTex = BuildMarsPlanetTexture(1024, 512);
            Texture2D marsNormal = BuildRegolithNormalTexture(256);
            Renderer globeRend = marsGlobe.GetComponent<Renderer>();
            if (globeRend != null)
            {
                Material marsMat = CreateLitMaterial(
                    "MS_Mars_Globe_Mat",
                    new Color(1.0f, 0.95f, 0.91f, 1f),
                    0.02f,
                    0.14f,
                    marsTex,
                    marsNormal,
                    Vector2.one,
                    1.05f
                );
                marsMat.EnableKeyword("_EMISSION");
                marsMat.SetColor("_EmissionColor", new Color(0.11f, 0.055f, 0.032f, 1f));
                globeRend.sharedMaterial = marsMat;
            }

            Transform atmoTf = marsGlobe.transform.Find("MS_Mars_Atmosphere_Shell");
            if (atmoTf == null)
            {
                GameObject atmoGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                atmoGo.name = "MS_Mars_Atmosphere_Shell";
                DestroyImmediate(atmoGo.GetComponent<Collider>());
                atmoGo.transform.SetParent(marsGlobe.transform, false);
                atmoTf = atmoGo.transform;
            }
            atmoTf.localScale = Vector3.one * 1.056f;
            if (rimMat != null)
            {
                if (rimMat.HasProperty("_DayRimColor")) rimMat.SetColor("_DayRimColor", new Color(1.0f, 0.68f, 0.40f, 1f));
                if (rimMat.HasProperty("_TwilightRimColor")) rimMat.SetColor("_TwilightRimColor", new Color(0.54f, 0.84f, 1.0f, 1f));
                if (rimMat.HasProperty("_Intensity")) rimMat.SetFloat("_Intensity", 2.65f);
            }
            Renderer atmoRend = atmoTf.GetComponent<Renderer>();
            if (atmoRend != null && rimMat != null)
            {
                atmoRend.sharedMaterial = rimMat;
            }

            // Soft 3/4 planetary fill light active ONLY in Space / Orbital view so Mars globe & moons remain rich and dimensional
            GameObject fillGo = GameObject.Find("MS_Space_MarsFillLight");
            if (fillGo == null)
            {
                fillGo = new GameObject("MS_Space_MarsFillLight");
                fillGo.transform.SetParent(transform, false);
            }
            spaceFillLight = fillGo.GetComponent<Light>() ?? fillGo.AddComponent<Light>();
            spaceFillLight.type = LightType.Directional;
            spaceFillLight.color = new Color(1.0f, 0.85f, 0.72f, 1f);
            spaceFillLight.intensity = 1.15f;
            spaceFillLight.shadows = LightShadows.None;
            spaceFillLight.enabled = false;

            // 2. Blazing 3D Space Sun + Custom Additive URP Corona Shader + 4-point diffraction starflare
            spaceSunObj = GameObject.Find("MS_Space_Sun");
            if (spaceSunObj == null)
            {
                spaceSunObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spaceSunObj.name = "MS_Space_Sun";
                DestroyImmediate(spaceSunObj.GetComponent<Collider>());
            }
            spaceSunObj.transform.localScale = Vector3.one * 220f;
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? urpLitShader;
            Renderer sunRend = spaceSunObj.GetComponent<Renderer>();
            if (sunRend != null)
            {
                Material coreMat = new Material(unlitShader);
                if (coreMat.HasProperty("_BaseColor")) coreMat.SetColor("_BaseColor", new Color(3.6f, 3.0f, 2.0f, 1f));
                else coreMat.color = new Color(1.0f, 0.94f, 0.78f, 1f);
                if (coreMat.HasProperty("_EmissionColor"))
                {
                    coreMat.EnableKeyword("_EMISSION");
                    coreMat.SetColor("_EmissionColor", new Color(4.5f, 3.5f, 2.0f, 1f));
                }
                sunRend.sharedMaterial = coreMat;
                sunRend.shadowCastingMode = ShadowCastingMode.Off;
                sunRend.receiveShadows = false;
            }

            Transform oldCorona = spaceSunObj.transform.Find("Corona");
            if (oldCorona != null) oldCorona.gameObject.SetActive(false);

            Material baseGlowMat = Resources.Load<Material>("Materials/M_MarsSolarGlow");
            if (baseGlowMat == null)
            {
                Shader glowShader = Shader.Find("MarsSentinel/SolarPhotosphereGlow") ?? spriteShader;
                baseGlowMat = new Material(glowShader);
            }
            Material innerGlowMat = new Material(baseGlowMat);
            if (innerGlowMat.HasProperty("_Color")) innerGlowMat.SetColor("_Color", new Color(2.8f, 1.9f, 0.95f, 1f));
            if (innerGlowMat.HasProperty("_CorePower")) innerGlowMat.SetFloat("_CorePower", 3.6f);
            sunHaloQuadA = EnsureHaloQuad(spaceSunObj.transform, "MS_Sun_Halo_Inner", 3.2f, innerGlowMat);

            Material outerGlowMat = new Material(baseGlowMat);
            if (outerGlowMat.HasProperty("_Color")) outerGlowMat.SetColor("_Color", new Color(1.15f, 0.58f, 0.20f, 1f));
            if (outerGlowMat.HasProperty("_CorePower")) outerGlowMat.SetFloat("_CorePower", 2.6f);
            sunHaloQuadB = EnsureHaloQuad(spaceSunObj.transform, "MS_Sun_Halo_Outer", 5.0f, outerGlowMat);

            if (spriteShader != null)
            {
                Texture2D spikeTex = BuildDiffractionSpikeTexture(128);
                Material spikeMat = new Material(spriteShader) { name = "MS_Sun_Diffraction_Mat" };
                spikeMat.mainTexture = spikeTex;
                spikeMat.color = new Color(1.0f, 0.90f, 0.72f, 0.48f);
                sunSpikeQuadA = EnsureHaloQuad(spaceSunObj.transform, "MS_Sun_Spike_A", 1.0f, spikeMat);
                sunSpikeQuadA.localScale = new Vector3(10.0f, 0.72f, 1f);
                sunSpikeQuadB = EnsureHaloQuad(spaceSunObj.transform, "MS_Sun_Spike_B", 1.0f, spikeMat);
                sunSpikeQuadB.localScale = new Vector3(8.2f, 0.68f, 1f);
            }

            // 3. Detailed Cratered Phobos & Deimos (realistic sculpted crater relief without concentric bullseye rings)
            Texture2D phobosTex = BuildCraterMoonTexture(512, 256, 33);
            Texture2D phobosBump = BuildCraterNormalTexture(512, 256, 33);
            Texture2D deimosTex = BuildCraterMoonTexture(512, 256, 91);
            Texture2D deimosBump = BuildCraterNormalTexture(512, 256, 91);
            phobosMat = CreateLitMaterial("MS_Phobos_PolishedMat", new Color(0.96f, 0.91f, 0.85f, 1f), 0.03f, 0.12f, phobosTex, phobosBump, Vector2.one, 1.15f);
            deimosMat = CreateLitMaterial("MS_Deimos_PolishedMat", new Color(0.97f, 0.92f, 0.86f, 1f), 0.03f, 0.10f, deimosTex, deimosBump, Vector2.one, 1.05f);

            phobosObj = GameObject.Find("Phobos");
            if (phobosObj != null)
            {
                EnsureCleanMoonMesh(phobosObj, "MS_Phobos_StickneyMesh", new Vector3(1.22f, 0.84f, 1.00f), true, 404, phobosMat);
                phobosCrescentLight = EnsureMoonFillLight("MS_Phobos_CrescentFill");
            }

            deimosObj = GameObject.Find("Deimos");
            if (deimosObj != null)
            {
                EnsureCleanMoonMesh(deimosObj, "MS_Deimos_Mesh", new Vector3(1.25f, 0.83f, 1.00f), false, 707, deimosMat);
                deimosCrescentLight = EnsureMoonFillLight("MS_Deimos_CrescentFill");
            }

            // 4. Populate 4 Orbital Constellation Spacecraft
            Material goldFoil = CreateLitMaterial("MS_Orbiter_Gold", new Color(0.95f, 0.72f, 0.22f, 1f), 0.82f, 0.78f);
            goldFoil.EnableKeyword("_EMISSION");
            goldFoil.SetColor("_EmissionColor", new Color(0.28f, 0.20f, 0.06f, 1f));
            Material solarBlue = CreateLitMaterial("MS_Orbiter_Solar", new Color(0.10f, 0.28f, 0.75f, 1f), 0.4f, 0.82f);
            solarBlue.EnableKeyword("_EMISSION");
            solarBlue.SetColor("_EmissionColor", new Color(0.08f, 0.22f, 0.55f, 1f));
            Material dishWhite = CreateLitMaterial("MS_Orbiter_Dish", new Color(0.92f, 0.94f, 0.98f, 1f), 0.25f, 0.65f);
            dishWhite.EnableKeyword("_EMISSION");
            dishWhite.SetColor("_EmissionColor", new Color(0.18f, 0.20f, 0.24f, 1f));

            string[] orbNames = { "MS_MRO_Root", "MS_Odyssey_Root", "MS_MAVEN_Root", "MS_GlobalSurveyor_Root" };
            orbiterRoots.Clear();
            foreach (string oname in orbNames)
            {
                GameObject orb = GameObject.Find(oname);
                if (orb != null)
                {
                    orbiterRoots.Add(orb);
                    if (orb.GetComponentsInChildren<Renderer>(true).Length == 0)
                    {
                        BuildOrbiterSpacecraft(orb.transform, goldFoil, solarBlue, dishWhite);
                    }
                    orb.transform.localScale = Vector3.one * 22f;
                }
            }

            // 5. Sleek Precision Multi-Layer Orbital Rings (open moon arcs avoiding intersections + AA ribbon texture)
            if (orbitRibbonTex == null) orbitRibbonTex = BuildOrbitRibbonTexture(4, 32);
            allOrbitLines.Clear();
            RebuildSleekOrbitLine("MS_Orbit_Phobos_Trail", PhobosOrbitRadius, PhobosInclDeg, PhobosAscDeg, new Color(0.45f, 0.82f, 1.0f, 0.85f), 2.8f, false);
            RebuildSleekOrbitLine("MS_Orbit_Deimos_Trail", DeimosOrbitRadius, DeimosInclDeg, DeimosAscDeg, new Color(1.0f, 0.86f, 0.44f, 0.82f), 2.8f, false);

            Color[] satColors =
            {
                new Color(0.32f, 0.88f, 1.0f, 0.75f),
                new Color(0.38f, 0.96f, 0.72f, 0.70f),
                new Color(0.98f, 0.52f, 0.82f, 0.70f),
                new Color(0.68f, 0.74f, 1.0f, 0.70f)
            };
            string[] trailNames = { "MRO_OrbitTrail", "Odyssey_OrbitTrail", "MAVEN_OrbitTrail", "GlobalSurveyor_OrbitTrail" };
            for (int i = 0; i < trailNames.Length; i++)
            {
                RebuildSleekOrbitLine(trailNames[i], orbiterRadii[i], orbiterInclinationsDeg[i], orbiterAscendingDeg[i], satColors[i], 1.8f, true);
            }
        }

        private void EnsureCleanMoonMesh(GameObject moonRoot, string meshName, Vector3 radii, bool stickney, int seed, Material mat)
        {
            foreach (Renderer r in moonRoot.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }
            Transform existing = moonRoot.transform.Find("MS_Moon_PolishedSurface");
            GameObject surfGo = existing != null ? existing.gameObject : new GameObject("MS_Moon_PolishedSurface");
            surfGo.transform.SetParent(moonRoot.transform, false);
            surfGo.transform.localPosition = Vector3.zero;
            surfGo.transform.localRotation = Quaternion.identity;
            surfGo.transform.localScale = Vector3.one;

            MeshFilter mf = surfGo.GetComponent<MeshFilter>() ?? surfGo.AddComponent<MeshFilter>();
            MeshRenderer mr = surfGo.GetComponent<MeshRenderer>() ?? surfGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildCrateredMoonMesh(meshName, radii, stickney, seed);
            mr.sharedMaterial = mat;
            mr.enabled = true;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        private Light EnsureMoonFillLight(string lightName)
        {
            GameObject existing = GameObject.Find(lightName);
            GameObject go = existing != null ? existing : new GameObject(lightName);
            go.transform.SetParent(transform, false);
            Light l = go.GetComponent<Light>() ?? go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.90f, 0.78f, 1f);
            l.range = 240f;
            l.intensity = 9500f;
            l.shadows = LightShadows.None;
            return l;
        }

        private Transform EnsureHaloQuad(Transform parent, string name, float scale, Material mat)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = name;
                DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
            }
            go.transform.localScale = Vector3.one * scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go.transform;
        }

        private void BuildOrbiterSpacecraft(Transform parent, Material gold, Material solar, Material whiteMat)
        {
            CreatePart(PrimitiveType.Cube, parent, "Bus", Vector3.zero, Vector3.zero, new Vector3(1.1f, 1.3f, 1.1f), gold);
            CreatePart(PrimitiveType.Cube, parent, "SolarWing_L", new Vector3(-2.3f, 0f, 0f), Vector3.zero, new Vector3(2.8f, 0.06f, 1.05f), solar);
            CreatePart(PrimitiveType.Cube, parent, "SolarWing_R", new Vector3(2.3f, 0f, 0f), Vector3.zero, new Vector3(2.8f, 0.06f, 1.05f), solar);
            CreatePart(PrimitiveType.Cylinder, parent, "HGA_Dish", new Vector3(0f, 0.82f, 0f), Vector3.zero, new Vector3(1.5f, 0.12f, 1.5f), whiteMat);
            CreatePart(PrimitiveType.Cylinder, parent, "Boom", new Vector3(0f, -1.1f, 0f), Vector3.zero, new Vector3(0.08f, 0.7f, 0.08f), whiteMat);
        }

        private void RebuildSleekOrbitLine(string name, float radius, float inclDeg, float ascDeg, Color col, float width, bool loopClosed)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
            }
            LineRenderer lr = go.GetComponent<LineRenderer>() ?? go.AddComponent<LineRenderer>();
            legacyOrbitLines.Remove(lr);
            lr.useWorldSpace = true;
            lr.loop = loopClosed;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.widthMultiplier = 1f;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (spriteShader != null)
            {
                Material m = new Material(spriteShader);
                m.color = Color.white;
                if (orbitRibbonTex != null) m.mainTexture = orbitRibbonTex;
                lr.sharedMaterial = m;
            }

            var grad = new Gradient();
            if (loopClosed)
            {
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(col, 0f),
                        new GradientColorKey(Color.Lerp(col, Color.white, 0.36f), 0.5f),
                        new GradientColorKey(col, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.42f, 0f),
                        new GradientAlphaKey(0.88f, 0.5f),
                        new GradientAlphaKey(0.42f, 1f)
                    }
                );
            }
            else
            {
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(col, 0f),
                        new GradientColorKey(Color.Lerp(col, Color.white, 0.35f), 0.5f),
                        new GradientColorKey(col, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.0f, 0.00f),
                        new GradientAlphaKey(0.55f, 0.06f),
                        new GradientAlphaKey(0.90f, 0.50f),
                        new GradientAlphaKey(0.55f, 0.94f),
                        new GradientAlphaKey(0.0f, 1.00f)
                    }
                );
            }
            lr.colorGradient = grad;

            const int segs = 360;
            lr.positionCount = segs;
            Quaternion qOrbit = Quaternion.Euler(inclDeg, ascDeg, 0f);
            for (int i = 0; i < segs; i++)
            {
                float th = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(th) * radius, 0f, Mathf.Sin(th) * radius);
                lr.SetPosition(i, marsCenter + qOrbit * local);
            }
            allOrbitLines.Add(lr);
        }

        private void UpdateMoonArcOrbitLine(LineRenderer lr, float radius, float inclDeg, float ascDeg,
            float moonAngleRad, float moonVisualRadius)
        {
            if (lr == null) return;
            lr.loop = false;
            int segs = lr.positionCount;
            if (segs < 32)
            {
                segs = 360;
                lr.positionCount = segs;
            }
            // Clear wide gap around moon silhouette so orbit track never cuts visually across Phobos or Deimos
            float gapRad = Mathf.Clamp((moonVisualRadius * 2.45f) / Mathf.Max(100f, radius), 0.045f, 0.16f);
            float startAng = moonAngleRad + gapRad;
            float span = Mathf.PI * 2f - gapRad * 2f;
            Quaternion qOrbit = Quaternion.Euler(inclDeg, ascDeg, 0f);
            for (int i = 0; i < segs; i++)
            {
                float u = i / (float)(segs - 1);
                float th = startAng + u * span;
                Vector3 local = new Vector3(Mathf.Cos(th) * radius, 0f, Mathf.Sin(th) * radius);
                lr.SetPosition(i, marsCenter + qOrbit * local);
            }
        }

        private static void ApplyDistanceCompensatedWidthCurve(LineRenderer lr, Camera cam, float desiredPx, float minWidth, float maxWidth)
        {
            if (lr == null || lr.positionCount < 2) return;
            float fovRad = (cam != null ? cam.fieldOfView : 52f) * Mathf.Deg2Rad;
            float screenH = Mathf.Max(480f, Screen.height);
            float factor = (2f * Mathf.Tan(fovRad * 0.5f)) / screenH;
            int count = lr.positionCount;
            const int keyCount = 13;
            var keys = new Keyframe[keyCount];
            for (int k = 0; k < keyCount; k++)
            {
                float t = k / (float)(keyCount - 1);
                int idx = Mathf.Clamp(Mathf.RoundToInt(t * (count - 1)), 0, count - 1);
                float dist = cam != null ? Vector3.Distance(cam.transform.position, lr.GetPosition(idx)) : 3600f;
                float worldW = Mathf.Clamp(desiredPx * dist * factor, minWidth, maxWidth);
                keys[k] = new Keyframe(t, worldW);
            }
            lr.widthMultiplier = 1f;
            lr.widthCurve = new AnimationCurve(keys);
        }

        private Texture2D BuildOrbitRibbonTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f; // -1..+1 across line width
                float a = Mathf.Exp(-ny * ny * 2.1f) * SmoothStep01(1.0f, 0.55f, Mathf.Abs(ny));
                Color c = new Color(1f, 1f, 1f, a);
                for (int x = 0; x < w; x++) cols[y * w + x] = c;
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildDiffractionSpikeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float vFalloff = Mathf.Exp(-ny * ny * 18.0f) * SmoothStep01(1.0f, 0.65f, Mathf.Abs(ny));
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float hFalloff = SmoothStep01(1.0f, 0.12f, Mathf.Abs(nx));
                    float alpha = vFalloff * hFalloff * hFalloff;
                    cols[y * size + x] = new Color(1f, 0.94f, 0.80f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void LateUpdate()
        {
            if (!initialized) InitializePolish();
            if (!beaconsSanitized || Time.frameCount <= 30 || (Time.frameCount % 8 == 0 && Time.frameCount <= 240))
            {
                SanitizeBeaconsAndRoutes();
            }

            SyncStateWithEnhancerAndInput();
            UpdateSpaceBlend();
            Vector3 sunDir = ComputeSynchronizedSunDirection();
            ApplyAtmosphereAndLighting(sunDir);
            UpdateMoonsAndOrbiters(sunDir);
            UpdateCameraFramingAndSpaceControls();
        }

        private void SyncStateWithEnhancerAndInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame && string.IsNullOrEmpty(captureOutDir))
                {
                    if (IsExploreModeActive) SetSurfaceMode(1);
                    else EnterExploreMode();
                }
                else if (kb.digit1Key.wasPressedThisFrame) SetSurfaceMode(1);
                else if (kb.digit2Key.wasPressedThisFrame) SetSurfaceMode(2);
                else if (kb.digit3Key.wasPressedThisFrame) SetSurfaceMode(3);
                else if (kb.digit4Key.wasPressedThisFrame) SetSurfaceMode(4);
                else if (kb.digit5Key.wasPressedThisFrame || kb.oKey.wasPressedThisFrame) SetOrbitMode(!forceOrbital || focusMode > 0);
                else if (kb.digit6Key.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) SetFocusMode(focusMode == 1 ? 0 : 1);
                else if (kb.digit7Key.wasPressedThisFrame) SetFocusMode(focusMode == 2 ? 0 : 2);
                else if (kb.digit8Key.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) SetFocusMode(focusMode == 3 ? 0 : 3);
            }
            if (cinemachineBrain != null)
            {
                bool allowBrain = !IsExploreModeActive && !forceOrbital && focusMode == 0;
                if (cinemachineBrain.enabled != allowBrain) cinemachineBrain.enabled = allowBrain;
            }

            // Mouse orbit drag & scroll zoom inside Orbit / Celestial Focus mode
            var mouse = Mouse.current;
            if (mouse != null && (forceOrbital || focusMode > 0))
            {
                if (mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    orbitYawOffset += delta.x * 0.25f;
                    orbitPitchOffset = Mathf.Clamp(orbitPitchOffset - delta.y * 0.18f, -45f, 45f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    orbitZoomMultiplier = Mathf.Clamp(orbitZoomMultiplier - scroll * 0.0008f, 0.45f, 2.2f);
                }
            }

            if (txtActiveCamera != null)
            {
                if (!string.IsNullOrEmpty(MarsSceneManager.DirectorHudLabel)) txtActiveCamera.text = MarsSceneManager.DirectorHudLabel;
                else if (focusMode == 1) txtActiveCamera.text = "ACTIVE VIEW: SOL • SUN FOCUS";
                else if (focusMode == 2) txtActiveCamera.text = "ACTIVE VIEW: PHOBOS • INNER MOON";
                else if (focusMode == 3) txtActiveCamera.text = "ACTIVE VIEW: DEIMOS • OUTER MOON";
                else if (forceOrbital) txtActiveCamera.text = "ACTIVE VIEW: ORBITAL CONSTELLATION (KEY 5 / O)";
                else if (IsExploreModeActive)
                {
                    bool driving = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;
                    string modeStr = driving
                        ? "ROVER DRIVE [E Dismount]"
                        : (MarsAstronautController.Instance != null && MarsAstronautController.Instance.IsFirstPerson
                            ? "EVA VISOR [V 3rd-Person]"
                            : "EVA EXPLORER [WASD / E]");
                    txtActiveCamera.text = $"ACTIVE VIEW: [F] {modeStr}";
                }
                else if (activeSurfaceKey == 2) txtActiveCamera.text = "ACTIVE VIEW: PERSEVERANCE CLOSE-UP";
                else if (activeSurfaceKey == 3) txtActiveCamera.text = "ACTIVE VIEW: INGENUITY HELICOPTER";
                else if (activeSurfaceKey == 4) txtActiveCamera.text = "ACTIVE VIEW: JEZERO TRAVERSE OVERVIEW";
                else txtActiveCamera.text = "ACTIVE VIEW: JEZERO SURFACE SHOWCASE";
            }
        }

        private void UpdateSpaceBlend()
        {
            bool isSpace = forceOrbital || focusMode > 0;
            spaceBlend = isSpace ? 1f : 0f;
            if (fiEnhancerSpaceBlend != null && enhancer != null)
            {
                fiEnhancerSpaceBlend.SetValue(enhancer, spaceBlend);
            }
            if (cinemachineBrain != null)
            {
                if (isSpace)
                {
                    cinemachineBrain.enabled = false;
                }
                else
                {
                    if (wasSpaceCamera)
                    {
                        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                        cinemachineBrain.enabled = false;
                        cinemachineBrain.enabled = true;
                        SnapMainCameraToActiveSurfaceCam();
                    }
                    else if (!IsExploreModeActive)
                    {
                        cinemachineBrain.enabled = true;
                        if (string.IsNullOrEmpty(captureOutDir))
                        {
                            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.65f);
                        }
                    }
                    else
                    {
                        cinemachineBrain.enabled = false;
                    }
                }
            }
            wasSpaceCamera = isSpace;
        }

        private void SnapMainCameraToActiveSurfaceCam()
        {
            CinemachineCamera target = GetActiveSurfaceCam();
            if (target != null && Camera.main != null)
            {
                Camera.main.transform.position = target.transform.position;
                Camera.main.transform.rotation = target.transform.rotation;
                Camera.main.fieldOfView = target.Lens.FieldOfView > 5f ? target.Lens.FieldOfView : 54f;
            }
        }

        private CinemachineCamera GetActiveSurfaceCam()
        {
            if (activeSurfaceKey == 2 && persevCam != null) return persevCam;
            if (activeSurfaceKey == 3 && ingenuityCam != null) return ingenuityCam;
            if (activeSurfaceKey == 4 && traverseCam != null) return traverseCam;
            return surfaceCam;
        }

        private Vector3 ComputeSynchronizedSunDirection()
        {
            float solHour = 16.10f;
            if (celestialCtrl != null && fiCelestialSolHour != null)
            {
                solHour = (float)fiCelestialSolHour.GetValue(celestialCtrl);
            }

            // Jezero diurnal curve aligned cleanly into open upper-left sky relative to surface showcase camera
            float phase = (solHour - 6.0f) / 12.0f; // 0 sunrise, 0.5 noon, 1 sunset
            float elevRad = Mathf.Sin(phase * Mathf.PI) * (38.0f * Mathf.Deg2Rad); // 16.1h => +18.1 deg, 17.55h => +7.5 deg
            float azimDeg = -41.0f + (solHour - 16.10f) * 14.0f; // -18 deg left of camera forward (-23 deg)
            float azimRad = azimDeg * Mathf.Deg2Rad;

            float cosEl = Mathf.Cos(elevRad);
            Vector3 surfaceSunDir = new Vector3(
                Mathf.Sin(azimRad) * cosEl,
                Mathf.Sin(elevRad),
                Mathf.Cos(azimRad) * cosEl
            ).normalized;

            Vector3 dir = Vector3.Slerp(surfaceSunDir, orbitSunDirection, spaceBlend).normalized;
            lastAlignedSunDir = dir;
            return dir;
        }

        private void ApplyAtmosphereAndLighting(Vector3 sunDir)
        {
            float elev = sunDir.y;
            float dayFactor = SmoothStep01(-0.08f, 0.32f, elev);
            float goldenFactor = Mathf.Exp(-Mathf.Pow((elev - 0.14f) / 0.16f, 2f));
            float activeTau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float stormFactor = Mathf.Clamp01((activeTau - 0.45f) / 2.35f);

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
                float surfaceIntensity = Mathf.Lerp(0.24f, 2.45f, dayFactor) * Mathf.Lerp(1.0f, 0.48f, stormFactor);
                Color surfaceColor = Color.Lerp(
                    new Color(1.0f, 0.52f, 0.28f, 1f),
                    new Color(1.0f, 0.92f, 0.80f, 1f),
                    SmoothStep01(0.06f, 0.45f, elev)
                );
                surfaceColor = Color.Lerp(surfaceColor, new Color(0.92f, 0.44f, 0.20f, 1f), stormFactor * 0.65f);
                sunLight.intensity = spaceBlend > 0.5f ? 3.25f : surfaceIntensity;
                sunLight.color = spaceBlend > 0.5f
                    ? new Color(1.0f, 0.96f, 0.90f, 1f)
                    : surfaceColor;
            }

            if (spaceFillLight != null)
            {
                spaceFillLight.enabled = spaceBlend > 0.35f;
                if (spaceFillLight.enabled)
                {
                    Vector3 fillDir = new Vector3(0.45f, 0.35f, -0.82f).normalized;
                    spaceFillLight.transform.rotation = Quaternion.LookRotation(-fillDir, Vector3.up);
                    spaceFillLight.intensity = 1.10f * spaceBlend;
                }
            }

            Color zenithDay = new Color(0.32f, 0.24f, 0.21f, 1f);
            Color zenithNight = new Color(0.012f, 0.015f, 0.026f, 1f);
            Color horizonDay = Color.Lerp(new Color(0.84f, 0.43f, 0.24f, 1f), new Color(0.76f, 0.50f, 0.35f, 1f), dayFactor);
            Color horizonNight = new Color(0.045f, 0.025f, 0.025f, 1f);

            Color zenith = Color.Lerp(zenithNight, zenithDay, dayFactor);
            Color horizon = Color.Lerp(horizonNight, horizonDay, Mathf.Max(dayFactor, goldenFactor * 0.68f));
            horizon = Color.Lerp(horizon, new Color(0.78f, 0.36f, 0.18f, 1f), stormFactor * 0.60f);
            Color haloCol = Color.Lerp(new Color(0.62f, 0.80f, 1.0f, 1f), new Color(0.48f, 0.72f, 0.98f, 1f), goldenFactor);

            if (skyboxMat != null)
            {
                skyboxMat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                skyboxMat.SetColor("_ZenithColor", zenith);
                skyboxMat.SetColor("_HorizonColor", horizon);
                skyboxMat.SetColor("_SunHaloColor", haloCol);
                skyboxMat.SetColor("_SunDiscColor", new Color(3.2f, 2.75f, 2.2f, 1f));
                skyboxMat.SetFloat("_SpaceBlend", spaceBlend);
                skyboxMat.SetFloat("_StarIntensity", Mathf.Lerp(1.6f, 2.5f, spaceBlend));
                skyboxMat.SetFloat("_DustTau", activeTau);
            }

            if (rimMat != null)
            {
                rimMat.SetVector("_SunDir", new Vector4(orbitSunDirection.x, orbitSunDirection.y, orbitSunDirection.z, 0f));
                rimMat.SetFloat("_Intensity", 2.65f);
            }

            RenderSettings.fog = spaceBlend < 0.45f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(0.00050f, 0.0042f, stormFactor), 0.0f, spaceBlend);
            RenderSettings.ambientLight = Color.Lerp(
                Color.Lerp(new Color(0.06f, 0.045f, 0.055f, 1f), new Color(0.54f, 0.36f, 0.28f, 1f), dayFactor),
                new Color(0.22f, 0.17f, 0.16f, 1f),
                spaceBlend
            );

            // Space Sun positioned cleanly along orbitSunDirection at r = 3200m (outside Deimos 2060m orbit, visible in upper-left frame)
            if (spaceSunObj != null)
            {
                spaceSunObj.SetActive(spaceBlend > 0.1f || focusMode == 1);
                Vector3 orbitSunPos = marsCenter + orbitSunDirection * 3200f;
                spaceSunObj.transform.position = orbitSunPos;
                if (Camera.main != null)
                {
                    Vector3 toCam = (Camera.main.transform.position - orbitSunPos).normalized;
                    Vector3 camUp = Camera.main.transform.up;
                    if (sunHaloQuadA != null) sunHaloQuadA.rotation = Quaternion.LookRotation(-toCam, camUp);
                    if (sunHaloQuadB != null) sunHaloQuadB.rotation = Quaternion.LookRotation(-toCam, camUp);
                    if (sunSpikeQuadA != null)
                    {
                        sunSpikeQuadA.localScale = new Vector3(10.0f, 0.68f, 1f);
                        sunSpikeQuadA.rotation = Quaternion.LookRotation(-toCam, camUp);
                    }
                    if (sunSpikeQuadB != null)
                    {
                        sunSpikeQuadB.localScale = new Vector3(8.2f, 0.64f, 1f);
                        sunSpikeQuadB.rotation = Quaternion.LookRotation(-toCam, camUp) * Quaternion.Euler(0f, 0f, 90f);
                    }
                }
            }
        }

        private void UpdateMoonsAndOrbiters(Vector3 sunDir)
        {
            float t = (focusMode == 2 || focusMode == 3) ? 4.0f : Time.time;
            float dayFactor = SmoothStep01(-0.08f, 0.32f, sunDir.y);

            // Orbital positions around Mars Center (Phobos in open right-foreground, Deimos in open left-foreground)
            float phAngle = t * 0.20f - 0.48f;
            Vector3 phOrbPos = marsCenter + Quaternion.Euler(PhobosInclDeg, PhobosAscDeg, 0f) *
                new Vector3(Mathf.Cos(phAngle) * PhobosOrbitRadius, 0f, Mathf.Sin(phAngle) * PhobosOrbitRadius);
            float deAngle = t * 0.08f + 3.48f;
            Vector3 deOrbPos = marsCenter + Quaternion.Euler(DeimosInclDeg, DeimosAscDeg, 0f) *
                new Vector3(Mathf.Cos(deAngle) * DeimosOrbitRadius, 0f, Mathf.Sin(deAngle) * DeimosOrbitRadius);

            // Surface sky positions: clear open upper sky in front of CM_SurfaceFleetOverview
            // Phobos upper-center below Top Banner, Deimos upper-right cleanly LEFT of StatusPanel!
            Vector3 surfCamPos = surfaceCam != null ? surfaceCam.transform.position : new Vector3(10.5f, 3.6f, -24.5f);
            Vector3 phSurfPos = surfCamPos + new Vector3(-0.34f, 0.22f, 0.915f).normalized * 680f;
            Vector3 deSurfPos = surfCamPos + new Vector3(-0.18f, 0.27f, 0.945f).normalized * 900f;

            // Daytime Martian atmospheric airlight scatter + sunlit crescent bounce so moons never render as black cutouts
            Color skyAirlight = Color.Lerp(RenderSettings.fogColor, new Color(0.66f, 0.49f, 0.38f), 0.58f) * Mathf.Lerp(0.16f, 0.85f, dayFactor) * (1f - spaceBlend);
            Color spaceBounce = new Color(0.11f, 0.09f, 0.08f) * spaceBlend;

            float phScale = Mathf.Lerp(26f, 64f, spaceBlend);
            if (phobosObj != null)
            {
                phobosObj.transform.position = Vector3.Lerp(phSurfPos, phOrbPos, spaceBlend);
                phobosObj.transform.localScale = Vector3.one * phScale;
                phobosObj.transform.rotation = Quaternion.Euler(10f, t * 8f + 18f, 6f);
                if (phobosMat != null)
                {
                    phobosMat.EnableKeyword("_EMISSION");
                    phobosMat.SetColor("_EmissionColor", skyAirlight + spaceBounce);
                }
                if (phobosCrescentLight != null)
                {
                    phobosCrescentLight.enabled = !isSpaceView();
                    phobosCrescentLight.transform.position = phobosObj.transform.position + new Vector3(-42f, 24f, -48f);
                }
            }

            float deScale = Mathf.Lerp(16f, 46f, spaceBlend);
            if (deimosObj != null)
            {
                deimosObj.transform.position = Vector3.Lerp(deSurfPos, deOrbPos, spaceBlend);
                deimosObj.transform.localScale = Vector3.one * deScale;
                deimosObj.transform.rotation = Quaternion.Euler(-8f, t * 5f + 20f, -10f);
                if (deimosMat != null)
                {
                    deimosMat.EnableKeyword("_EMISSION");
                    deimosMat.SetColor("_EmissionColor", skyAirlight + spaceBounce);
                }
                if (deimosCrescentLight != null)
                {
                    deimosCrescentLight.enabled = !isSpaceView();
                    deimosCrescentLight.transform.position = deimosObj.transform.position + new Vector3(-36f, 20f, -44f);
                }
            }

            bool isSpaceView() => spaceBlend > 0.45f;

            // Orbiters along exact matching ellipses
            bool showSpace = spaceBlend > 0.5f;
            for (int i = 0; i < orbiterRoots.Count && i < orbiterRadii.Length; i++)
            {
                GameObject orb = orbiterRoots[i];
                if (orb == null) continue;
                orb.SetActive(showSpace);
                if (!showSpace) continue;

                float a = t * orbiterSpeeds[i] + orbiterPhaseOffsets[i];
                Quaternion q = Quaternion.Euler(orbiterInclinationsDeg[i], orbiterAscendingDeg[i], 0f);
                Vector3 pos = marsCenter + q * new Vector3(Mathf.Cos(a) * orbiterRadii[i], 0f, Mathf.Sin(a) * orbiterRadii[i]);
                orb.transform.position = pos;
                Vector3 nadir = (marsCenter - pos).normalized;
                Vector3 vel = (q * new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))).normalized;
                orb.transform.rotation = Quaternion.LookRotation(vel, -nadir);
            }

            // Strictly hide all orbital lines in Surface view & update open moon arcs + uniform pixel-space width
            foreach (LineRenderer lr in legacyOrbitLines)
            {
                if (lr != null) lr.enabled = false;
            }
            if (showSpace)
            {
                if (allOrbitLines.Count > 0)
                {
                    UpdateMoonArcOrbitLine(allOrbitLines[0], PhobosOrbitRadius, PhobosInclDeg, PhobosAscDeg, phAngle, phScale * 0.62f);
                }
                if (allOrbitLines.Count > 1)
                {
                    UpdateMoonArcOrbitLine(allOrbitLines[1], DeimosOrbitRadius, DeimosInclDeg, DeimosAscDeg, deAngle, deScale * 0.62f);
                }
            }
            Camera activeCam = Camera.main;
            for (int i = 0; i < allOrbitLines.Count; i++)
            {
                LineRenderer lr = allOrbitLines[i];
                if (lr == null) continue;
                lr.enabled = showSpace;
                if (showSpace)
                {
                    ApplyDistanceCompensatedWidthCurve(lr, activeCam, i < 2 ? 4.4f : 3.2f, 1.05f, 24f);
                }
            }
            if (marsGlobe != null)
            {
                marsGlobe.SetActive(showSpace || focusMode > 0);
                marsGlobe.transform.position = marsCenter;
                marsGlobe.transform.Rotate(Vector3.up, Time.deltaTime * 3.5f, Space.Self);
            }
            if (horizonTerrainObj != null) horizonTerrainObj.SetActive(!showSpace);
            if (traverseRoutesObj != null) traverseRoutesObj.SetActive(!showSpace);
            if (waypointsObj != null) waypointsObj.SetActive(!showSpace);
        }

        private void UpdateCameraFramingAndSpaceControls()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            if (forceOrbital && focusMode == 0)
            {
                Vector3 baseOffset = new Vector3(1200f, 680f, -3850f);
                Vector3 rotated = Quaternion.Euler(orbitPitchOffset, orbitYawOffset, 0f) * baseOffset * orbitZoomMultiplier;
                Vector3 camPos = marsCenter + rotated;
                Vector3 target = marsCenter + new Vector3(-340f, 140f, 0f);
                mainCam.transform.position = camPos;
                mainCam.transform.LookAt(target, Vector3.up);
                mainCam.fieldOfView = 52f;
                if (orbitalCam != null)
                {
                    orbitalCam.transform.position = camPos;
                    orbitalCam.transform.LookAt(target, Vector3.up);
                }
            }
            else if (focusMode > 0)
            {
                Vector3 targetPos = marsCenter;
                Vector3 camPos = marsCenter;
                if (focusMode == 1 && spaceSunObj != null)
                {
                    // Frame Sun centered at comfortable solar distance showing glowing photosphere, corona, spikes, and open starfield
                    Vector3 sunPos = spaceSunObj.transform.position;
                    Vector3 toMars = (marsCenter - sunPos).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, toMars).normalized;
                    Vector3 viewDir = Quaternion.Euler(orbitPitchOffset, orbitYawOffset, 0f) *
                        (toMars * 0.42f + side * 0.88f + Vector3.up * 0.20f).normalized;
                    targetPos = sunPos;
                    camPos = sunPos + viewDir * (2250f * orbitZoomMultiplier);
                }
                else if (focusMode == 2 && phobosObj != null)
                {
                    targetPos = phobosObj.transform.position;
                    Vector3 sunlitViewDir = Quaternion.Euler(orbitPitchOffset, orbitYawOffset, 0f) *
                        Vector3.Slerp(orbitSunDirection, (marsCenter - targetPos).normalized, 0.25f).normalized;
                    camPos = targetPos + sunlitViewDir * (165f * orbitZoomMultiplier);
                }
                else if (focusMode == 3 && deimosObj != null)
                {
                    targetPos = deimosObj.transform.position;
                    Vector3 sunlitViewDir = Quaternion.Euler(orbitPitchOffset, orbitYawOffset, 0f) *
                        (Vector3.Slerp(orbitSunDirection, (marsCenter - targetPos).normalized, 0.20f) + Vector3.up * 0.16f).normalized;
                    camPos = targetPos + sunlitViewDir * (120f * orbitZoomMultiplier);
                }
                mainCam.transform.position = camPos;
                mainCam.transform.LookAt(targetPos, Vector3.up);
                mainCam.fieldOfView = 48f;
            }
        }

        private IEnumerator RunShowcaseCaptureRoutine()
        {
            Directory.CreateDirectory(captureOutDir);
            for (int i = 0; i < 20; i++) yield return null;

            var entries = new List<string>();

            // 1. Default Surface Opening View
            SetSurfaceMode(1);
            yield return WaitFrames(8);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_01_surface.png"));
            entries.Add(RecordDiag("unity_01_surface"));
            yield return WaitFrames(3);

            // 2. Orbital Constellation View ([O] / [5])
            SetOrbitMode(true);
            yield return WaitFrames(8);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_02_orbit.png"));
            entries.Add(RecordDiag("unity_02_orbit"));
            yield return WaitFrames(3);

            // 3. Focus Phobos ([7])
            SetFocusMode(2);
            yield return WaitFrames(6);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_03_focus_phobos.png"));
            entries.Add(RecordDiag("unity_03_focus_phobos"));
            yield return WaitFrames(3);

            // 4. Focus Deimos ([8])
            SetFocusMode(3);
            yield return WaitFrames(6);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_04_focus_deimos.png"));
            entries.Add(RecordDiag("unity_04_focus_deimos"));
            yield return WaitFrames(3);

            // 5. Focus Sun ([6])
            SetFocusMode(1);
            yield return WaitFrames(6);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_05_focus_sun.png"));
            entries.Add(RecordDiag("unity_05_focus_sun"));
            yield return WaitFrames(3);

            // 6. Camera [2] Perseverance right after Orbit ([O]) to prove clean instant escape!
            SetSurfaceMode(2);
            yield return WaitFrames(8);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_06_cam2_after_orbit.png"));
            entries.Add(RecordDiag("unity_06_cam2_after_orbit"));
            yield return WaitFrames(3);

            // 7. Sunset Sol Phase in Surface Overview (instant snap back to Overview [1])
            SetSurfaceMode(1);
            SnapMainCameraToActiveSurfaceCam();
            if (celestialCtrl != null && fiCelestialSolHour != null)
            {
                fiCelestialSolHour.SetValue(celestialCtrl, 17.45f);
            }
            yield return WaitFrames(8);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captureOutDir, "unity_07_sunset.png"));
            entries.Add(RecordDiag("unity_07_sunset"));
            yield return WaitFrames(4);

            File.WriteAllText(
                Path.Combine(captureOutDir, "capture_report.json"),
                "[\n" + string.Join(",\n", entries) + "\n]\n",
                Encoding.UTF8
            );

            Application.Quit(0);
        }

        private IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        public void EnterExploreMode()
        {
            IsExploreModeActive = true;
            forceOrbital = false;
            focusMode = 0;
            spaceBlend = 0f;
            if (fiEnhancerOrbitOverride != null && enhancer != null) fiEnhancerOrbitOverride.SetValue(enhancer, false);
            if (fiEnhancerFocusMode != null && enhancer != null) fiEnhancerFocusMode.SetValue(enhancer, 0);
            if (fiEnhancerSpaceBlend != null && enhancer != null) fiEnhancerSpaceBlend.SetValue(enhancer, 0f);
            if (enhancer != null) enhancer.SendMessage("ClearCameraOverrides", SendMessageOptions.DontRequireReceiver);
            if (cinemachineBrain != null) cinemachineBrain.enabled = false;
            MarsAudioManager.Instance?.PlayUiClick();
        }

        public void SetOrbitView(bool active)
        {
            SetOrbitMode(active);
        }

        public void TriggerViewMode(int modeKey)
        {
            if (modeKey >= 1 && modeKey <= 4) SetSurfaceMode(modeKey);
            else if (modeKey == 5) SetOrbitMode(!forceOrbital || focusMode > 0);
            else if (modeKey == 6) SetFocusMode(focusMode == 1 ? 0 : 1);
            else if (modeKey == 7) SetFocusMode(focusMode == 2 ? 0 : 2);
            else if (modeKey == 8) SetFocusMode(focusMode == 3 ? 0 : 3);
        }

        private void SetSurfaceMode(int camView)
        {
            IsExploreModeActive = false;
            activeSurfaceKey = Mathf.Clamp(camView, 1, 4);
            forceOrbital = false;
            focusMode = 0;
            spaceBlend = 0f;
            if (fiEnhancerOrbitOverride != null && enhancer != null) fiEnhancerOrbitOverride.SetValue(enhancer, false);
            if (fiEnhancerFocusMode != null && enhancer != null) fiEnhancerFocusMode.SetValue(enhancer, 0);
            if (fiEnhancerSpaceBlend != null && enhancer != null) fiEnhancerSpaceBlend.SetValue(enhancer, 0f);
            if (enhancer != null) enhancer.SendMessage("ClearCameraOverrides", SendMessageOptions.DontRequireReceiver);

            if (orbitalCam != null) orbitalCam.Priority.Value = 5;
            if (surfaceCam != null) surfaceCam.Priority.Value = activeSurfaceKey == 1 ? 30 : 10;
            if (persevCam != null) persevCam.Priority.Value = activeSurfaceKey == 2 ? 30 : 10;
            if (ingenuityCam != null) ingenuityCam.Priority.Value = activeSurfaceKey == 3 ? 30 : 10;
            if (traverseCam != null) traverseCam.Priority.Value = activeSurfaceKey == 4 ? 30 : 10;
            CinemachineCamera chosenCam = GetActiveSurfaceCam();
            if (chosenCam != null) chosenCam.Prioritize();
            if (!string.IsNullOrEmpty(captureOutDir) && cinemachineBrain != null)
            {
                cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                cinemachineBrain.enabled = false;
                cinemachineBrain.enabled = true;
                SnapMainCameraToActiveSurfaceCam();
            }
        }

        private void SetOrbitMode(bool active)
        {
            if (active) IsExploreModeActive = false;
            else if (string.IsNullOrEmpty(captureOutDir)) IsExploreModeActive = true;
            forceOrbital = active;
            focusMode = 0;
            spaceBlend = active ? 1f : 0f;
            if (fiEnhancerOrbitOverride != null && enhancer != null) fiEnhancerOrbitOverride.SetValue(enhancer, active);
            if (fiEnhancerFocusMode != null && enhancer != null) fiEnhancerFocusMode.SetValue(enhancer, 0);
            if (fiEnhancerSpaceBlend != null && enhancer != null) fiEnhancerSpaceBlend.SetValue(enhancer, spaceBlend);
            if (orbitalCam != null) orbitalCam.Priority.Value = active ? 100 : 5;
        }

        private void SetFocusMode(int mode)
        {
            if (mode > 0) IsExploreModeActive = false;
            else if (string.IsNullOrEmpty(captureOutDir)) IsExploreModeActive = true;
            forceOrbital = false;
            focusMode = mode;
            spaceBlend = mode > 0 ? 1f : 0f;
            if (fiEnhancerOrbitOverride != null && enhancer != null) fiEnhancerOrbitOverride.SetValue(enhancer, false);
            if (fiEnhancerFocusMode != null && enhancer != null) fiEnhancerFocusMode.SetValue(enhancer, mode);
            if (fiEnhancerSpaceBlend != null && enhancer != null) fiEnhancerSpaceBlend.SetValue(enhancer, spaceBlend);
        }

        private string RecordDiag(string label)
        {
            Camera cam = Camera.main;
            string sunVp = ProjectVp(cam, cam != null ? (spaceBlend > 0.5f && spaceSunObj != null ? spaceSunObj.transform.position : cam.transform.position + lastAlignedSunDir * 1000f) : Vector3.zero);
            string phVp = phobosObj != null ? ProjectVp(cam, phobosObj.transform.position) : "null";
            string deVp = deimosObj != null ? ProjectVp(cam, deimosObj.transform.position) : "null";
            string marsVp = marsGlobe != null ? ProjectVp(cam, marsGlobe.transform.position) : "null";

            string hudClip = "unknown";
            if (statusPanelRect != null)
            {
                Vector3[] corners = new Vector3[4];
                statusPanelRect.GetWorldCorners(corners);
                float maxX = corners[2].x;
                hudClip = maxX <= Screen.width + 1f ? $"ok(rightX={maxX:F1}/{Screen.width})" : $"CLIP(rightX={maxX:F1}/{Screen.width})";
            }

            string sbStr = spaceBlend.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return $"  {{\"shot\":\"{label}\",\"camPos\":\"{(cam != null ? cam.transform.position.ToString("F1") : "null")}\",\"sunVp\":\"{sunVp}\",\"phobosVp\":\"{phVp}\",\"deimosVp\":\"{deVp}\",\"marsVp\":\"{marsVp}\",\"hud\":\"{hudClip}\",\"spaceBlend\":{sbStr}}}";
        }

        private static string ProjectVp(Camera cam, Vector3 worldPos)
        {
            if (cam == null) return "null";
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return $"({vp.x:F2},{vp.y:F2},z={vp.z:F1})";
        }

        private GameObject CreatePart(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private Material CreateLitMaterial(string name, Color baseCol, float metallic, float smoothness,
            Texture2D albedo = null, Texture2D normalMap = null, Vector2? tiling = null, float bumpScale = 1f)
        {
            Material m = new Material(urpLitShader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            else m.color = baseCol;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (albedo != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
                else m.mainTexture = albedo;
                if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            }
            if (normalMap != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normalMap);
                m.SetFloat("_BumpScale", bumpScale);
                m.EnableKeyword("_NORMALMAP");
            }
            return m;
        }

        private static float Hash2D(int ix, int iy, int seed)
        {
            unchecked
            {
                int h = ix * 374761393 + iy * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= (h >> 16);
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        private static float PeriodicValueNoise2D(float u01, float v01, int period, int seed)
        {
            float fx = u01 * period;
            float fy = v01 * period;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            float sx = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float sy = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);

            int ix0 = ((x0 % period) + period) % period;
            int iy0 = ((y0 % period) + period) % period;
            int ix1 = (ix0 + 1) % period;
            int iy1 = (iy0 + 1) % period;

            float n00 = Hash2D(ix0, iy0, seed);
            float n10 = Hash2D(ix1, iy0, seed);
            float n01 = Hash2D(ix0, iy1, seed);
            float n11 = Hash2D(ix1, iy1, seed);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        private static float PeriodicFbm2D(float u01, float v01, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicValueNoise2D(u01, v01, period, seed + o * 97) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / Mathf.Max(0.001f, norm);
        }

        private static float Hash3D(int ix, int iy, int iz, int seed)
        {
            unchecked
            {
                int h = ix * 374761393 + iy * 668265263 + iz * 2147483647 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= (h >> 16);
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        private static float Noise3D(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x);
            int y0 = Mathf.FloorToInt(p.y);
            int z0 = Mathf.FloorToInt(p.z);
            float tx = p.x - x0;
            float ty = p.y - y0;
            float tz = p.z - z0;
            float sx = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float sy = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);
            float sz = tz * tz * tz * (tz * (tz * 6f - 15f) + 10f);

            float c000 = Hash3D(x0, y0, z0, seed);
            float c100 = Hash3D(x0 + 1, y0, z0, seed);
            float c010 = Hash3D(x0, y0 + 1, z0, seed);
            float c110 = Hash3D(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash3D(x0, y0, z0 + 1, seed);
            float c101 = Hash3D(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash3D(x0, y0 + 1, z0 + 1, seed);
            float c111 = Hash3D(x0 + 1, y0 + 1, z0 + 1, seed);

            float x00 = Mathf.Lerp(c000, c100, sx);
            float x10 = Mathf.Lerp(c010, c110, sx);
            float x01 = Mathf.Lerp(c001, c101, sx);
            float x11 = Mathf.Lerp(c011, c111, sx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, sy), Mathf.Lerp(x01, x11, sy), sz);
        }

        private static float Fbm3D(Vector3 p, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            Vector3 q = p;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise3D(q, seed + o * 131) * amp;
                norm += amp;
                amp *= 0.5f;
                q *= 2.04f;
            }
            return sum / Mathf.Max(0.001f, norm);
        }

        private float EvaluateRegolithHeight(float u01, float v01)
        {
            float macroA = PeriodicFbm2D(u01, v01, 4, 4, 17);
            float macroB = PeriodicFbm2D(u01 + v01, v01 - u01, 3, 4, 41);
            float macro = macroA * 0.55f + macroB * 0.45f;

            float warpU = PeriodicFbm2D(u01 + 0.17f, v01 + 0.31f, 3, 3, 53) * 0.18f;
            float warpV = PeriodicFbm2D(u01 + 0.61f, v01 + 0.09f, 3, 3, 89) * 0.18f;
            float twoPi = Mathf.PI * 2f;
            float windStrength = SmoothStep01(0.32f, 0.72f, PeriodicFbm2D(u01 + 0.29f, v01 + 0.77f, 2, 2, 137));
            float phase = (u01 * 3f + v01 * 1f) * twoPi + warpU * 22f + warpV * 14f;
            float windDunes = (Mathf.Sin(phase) * 0.5f + 0.5f) * windStrength + 0.5f * (1f - windStrength);

            float detailA = PeriodicFbm2D(u01, v01, 12, 3, 211);
            float detailB = PeriodicFbm2D(2f * u01 + v01, -u01 + 2f * v01, 8, 3, 277);
            float detail = detailA * 0.5f + detailB * 0.5f;
            return macro * 0.56f + detail * 0.30f + windDunes * 0.14f;
        }

        private Texture2D BuildRegolithSandTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v01 = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u01 = x / (float)size;
                    float hVal = EvaluateRegolithHeight(u01, v01);
                    float basaltPatch = PeriodicFbm2D(u01 + v01, v01 - u01 + 0.37f, 4, 3, 307);
                    float siltPatch = PeriodicFbm2D(2f * u01 - v01 + 0.19f, u01 + 2f * v01 + 0.53f, 5, 3, 383);
                    float organicTone = PeriodicFbm2D(3f * u01 - 2f * v01, 2f * u01 + 3f * v01, 6, 3, 521);
                    float pebbleGrit = (Hash2D(x, y, 409) - 0.5f) * 0.05f;
                    float val = Mathf.Clamp01(((hVal * 0.62f + basaltPatch * 0.20f + siltPatch * 0.18f) - 0.20f) * 1.44f + pebbleGrit);

                    Color darkBasaltDust = new Color(0.45f, 0.29f, 0.21f, 1f);
                    Color midTerracotta = new Color(0.76f, 0.48f, 0.31f, 1f);
                    Color warmOchre = new Color(0.89f, 0.62f, 0.41f, 1f);
                    Color sunlitBuff = new Color(0.95f, 0.74f, 0.53f, 1f);

                    Color c;
                    if (val < 0.28f) c = Color.Lerp(darkBasaltDust, midTerracotta, val / 0.28f);
                    else if (val < 0.66f) c = Color.Lerp(midTerracotta, warmOchre, (val - 0.28f) / 0.38f);
                    else c = Color.Lerp(warmOchre, sunlitBuff, (val - 0.66f) / 0.34f);

                    // Soft natural mineral dust streaks (zero axis-aligned threshold blocks)
                    float dustMix = SmoothStep01(0.35f, 0.78f, organicTone);
                    c = Color.Lerp(c, midTerracotta * 0.92f, (1f - dustMix) * 0.14f);
                    c = Color.Lerp(c, sunlitBuff, dustMix * 0.12f);
                    cols[y * size + x] = c;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildRegolithNormalTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                float v01 = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u01 = x / (float)size;
                    float microRipple = (PeriodicFbm2D(2f * u01 + v01, -u01 + 2f * v01, 14, 2, 613) - 0.5f) * 0.08f;
                    float fineSand = (Hash2D(x, y, 647) - 0.5f) * 0.015f;
                    h[y * size + x] = EvaluateRegolithHeight(u01, v01) + microRipple + fineSand;
                }
            }
            var cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int yp = (y + 1) % size;
                int ym = (y - 1 + size) % size;
                for (int x = 0; x < size; x++)
                {
                    int xp = (x + 1) % size;
                    int xm = (x - 1 + size) % size;
                    float dx = (h[y * size + xp] - h[y * size + xm]) * 1.6f;
                    float dy = (h[yp * size + x] - h[ym * size + x]) * 1.6f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    cols[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildMarsPlanetTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = (y / (float)h - 0.5f) * Mathf.PI;
                float cosLat = Mathf.Cos(lat);
                float sinLat = Mathf.Sin(lat);
                for (int x = 0; x < w; x++)
                {
                    float lon = (x / (float)w) * Mathf.PI * 2f;
                    Vector3 spherePt = new Vector3(cosLat * Mathf.Cos(lon), sinLat, cosLat * Mathf.Sin(lon));
                    float fbm = Fbm3D(spherePt * 3.1f + new Vector3(1.4f, 0.7f, -2.1f), 5, 77);
                    float fine = Fbm3D(spherePt * 14f, 3, 193);
                    float polar = SmoothStep01(1.16f, 1.38f, Mathf.Abs(lat) + (fbm - 0.5f) * 0.14f);

                    Color basaltLowland = new Color(0.42f, 0.21f, 0.13f, 1f);
                    Color ferricMid = new Color(0.79f, 0.41f, 0.22f, 1f);
                    Color brightDust = new Color(0.95f, 0.65f, 0.41f, 1f);

                    float t = Mathf.Clamp01((fbm * 0.82f + fine * 0.18f - 0.16f) * 1.38f);
                    Color c = t < 0.45f
                        ? Color.Lerp(basaltLowland, ferricMid, t / 0.45f)
                        : Color.Lerp(ferricMid, brightDust, (t - 0.45f) / 0.55f);

                    // Valles Marineris equatorial canyon gash & Olympus / Tharsis highland dust contrast
                    float valles = Mathf.Exp(-Mathf.Pow((lat + 0.20f) * 12f, 2f)) * Mathf.Exp(-Mathf.Pow((lon - 2.4f) * 2.8f, 2f));
                    c = Color.Lerp(c, new Color(0.24f, 0.12f, 0.08f, 1f), valles * 0.82f);

                    float syrtis = Mathf.Exp(-Mathf.Pow((lat - 0.16f) * 4.2f, 2f)) * Mathf.Exp(-Mathf.Pow((lon - 4.7f) * 3.8f, 2f));
                    c = Color.Lerp(c, new Color(0.28f, 0.16f, 0.12f, 1f), syrtis * 0.68f);

                    float hellas = Mathf.Exp(-Mathf.Pow((lat + 0.70f) * 4.0f, 2f)) * Mathf.Exp(-Mathf.Pow((lon - 4.4f) * 3.4f, 2f));
                    c = Color.Lerp(c, new Color(0.95f, 0.72f, 0.52f, 1f), hellas * 0.55f);

                    // Subtle high-altitude CO2 ice cloud wisps & crisp CO2 polar ice caps
                    float wispFbm = Fbm3D(spherePt * 5.2f + new Vector3(-3f, 2f, 1f), 3, 333);
                    float wisp = SmoothStep01(0.60f, 0.78f, wispFbm) * 0.42f;
                    c = Color.Lerp(c, new Color(0.93f, 0.91f, 0.96f, 1f), wisp * (1f - polar));
                    c = Color.Lerp(c, new Color(0.98f, 0.97f, 0.96f, 1f), polar);
                    cols[y * w + x] = c;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildCraterMoonTexture(int w, int h, int seed)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(seed);
            var craterDirs = new Vector3[32];
            var craterRadii = new float[32];
            var craterDepths = new float[32];
            for (int i = 0; i < craterDirs.Length; i++)
            {
                float u = (float)rng.NextDouble() * 2f - 1f;
                float phi = (float)rng.NextDouble() * Mathf.PI * 2f;
                float s = Mathf.Sqrt(1f - u * u);
                craterDirs[i] = new Vector3(s * Mathf.Cos(phi), u, s * Mathf.Sin(phi));
                craterRadii[i] = 0.08f + (float)rng.NextDouble() * 0.16f;
                craterDepths[i] = 0.35f + (float)rng.NextDouble() * 0.45f;
            }
            craterDirs[0] = new Vector3(0.45f, 0.22f, -0.86f).normalized;
            craterRadii[0] = 0.44f;
            craterDepths[0] = 0.85f;

            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float theta = (1f - (y + 0.5f) / h) * Mathf.PI;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);
                for (int x = 0; x < w; x++)
                {
                    float phi = ((x + 0.5f) / w) * Mathf.PI * 2f;
                    Vector3 n = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    // Natural multi-octave chondrite asteroid dust & rocky regolith albedo (NO concentric white bullseye rings!)
                    float macroFbm = Fbm3D(n * 3.4f, 4, seed + 19);
                    float fineFbm = Fbm3D(n * 16.0f, 3, seed + 71);
                    float shade = 0.48f + (macroFbm - 0.5f) * 0.34f + (fineFbm - 0.5f) * 0.16f;
                    for (int i = 0; i < craterDirs.Length; i++)
                    {
                        float dot = Mathf.Clamp(Vector3.Dot(n, craterDirs[i]), -1f, 1f);
                        float d = Mathf.Acos(dot) / craterRadii[i];
                        if (d < 0.92f)
                        {
                            // Subtle weathered floor shadow inside crater bowls only; real rim highlights come from 3D mesh + normal map
                            float bowl = SmoothStep01(0.92f, 0.15f, d);
                            shade -= bowl * 0.12f * craterDepths[i];
                        }
                    }
                    shade = Mathf.Clamp(shade, 0.24f, 0.86f);
                    float warmTint = 0.88f + macroFbm * 0.12f;
                    cols[y * w + x] = new Color(shade * warmTint, shade * 0.84f, shade * 0.76f, 1f);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildCraterNormalTexture(int w, int h, int seed)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(seed);
            var craterDirs = new Vector3[32];
            var craterRadii = new float[32];
            var craterDepths = new float[32];
            for (int i = 0; i < craterDirs.Length; i++)
            {
                float u = (float)rng.NextDouble() * 2f - 1f;
                float phi = (float)rng.NextDouble() * Mathf.PI * 2f;
                float s = Mathf.Sqrt(1f - u * u);
                craterDirs[i] = new Vector3(s * Mathf.Cos(phi), u, s * Mathf.Sin(phi));
                craterRadii[i] = 0.08f + (float)rng.NextDouble() * 0.16f;
                craterDepths[i] = 0.35f + (float)rng.NextDouble() * 0.45f;
            }
            craterDirs[0] = new Vector3(0.45f, 0.22f, -0.86f).normalized;
            craterRadii[0] = 0.44f;
            craterDepths[0] = 0.85f;

            var hf = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float theta = (1f - (y + 0.5f) / h) * Mathf.PI;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);
                for (int x = 0; x < w; x++)
                {
                    float phi = ((x + 0.5f) / w) * Mathf.PI * 2f;
                    Vector3 n = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    float val = (Fbm3D(n * 11f, 3, seed + 151) - 0.5f) * 0.22f;
                    for (int i = 0; i < craterDirs.Length; i++)
                    {
                        float dot = Mathf.Clamp(Vector3.Dot(n, craterDirs[i]), -1f, 1f);
                        float d = Mathf.Acos(dot) / craterRadii[i];
                        if (d < 1.32f)
                        {
                            if (d < 0.90f)
                            {
                                float u = d / 0.90f;
                                val -= (1f - u * u) * 0.42f * craterDepths[i];
                            }
                            float rim = Mathf.Exp(-Mathf.Pow((d - 0.98f) * 4.5f, 2f)) * 0.18f * craterDepths[i];
                            val += rim;
                        }
                    }
                    hf[y * w + x] = val;
                }
            }
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                int yp = Mathf.Min(y + 1, h - 1);
                int ym = Mathf.Max(y - 1, 0);
                for (int x = 0; x < w; x++)
                {
                    int xp = (x + 1) % w;
                    int xm = (x - 1 + w) % w;
                    float dx = (hf[y * w + xp] - hf[y * w + xm]) * 3.2f;
                    float dy = (hf[yp * w + x] - hf[ym * w + x]) * 3.2f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    cols[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildRadialGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 3.6f);
                    // Smooth taper near r=1 so edge alpha is strictly zero
                    glow *= SmoothStep01(1.0f, 0.72f, r);
                    cols[y * size + x] = new Color(1f, 0.92f, 0.78f, glow);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Mesh BuildCrateredMoonMesh(string name, Vector3 radii, bool stickney, int seed)
        {
            const int latSegs = 60;
            const int lonSegs = 96;
            var rng = new System.Random(seed);
            var craterDirs = new Vector3[28];
            var craterRadii = new float[28];
            for (int i = 0; i < craterDirs.Length; i++)
            {
                // Keep large craters out of immediate UV polar cap for silky smooth poles
                float u = ((float)rng.NextDouble() * 1.74f) - 0.87f;
                float phi = (float)rng.NextDouble() * Mathf.PI * 2f;
                float s = Mathf.Sqrt(1f - u * u);
                craterDirs[i] = new Vector3(s * Mathf.Cos(phi), u, s * Mathf.Sin(phi));
                craterRadii[i] = 0.10f + (float)rng.NextDouble() * 0.16f;
            }
            Vector3 stickneyDir = new Vector3(0.45f, 0.22f, -0.86f).normalized;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int y = 0; y <= latSegs; y++)
            {
                float v = y / (float)latSegs;
                float theta = (1f - v) * Mathf.PI;
                for (int x = 0; x <= lonSegs; x++)
                {
                    float u = x / (float)lonSegs;
                    float phi = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)
                    );
                    float rockFbm = Fbm3D(n * 2.4f, 4, seed + 55);
                    float lump = 0.93f + rockFbm * 0.16f +
                                 Mathf.Sin(n.x * 3.1f + n.y * 2.2f) * 0.045f +
                                 Mathf.Cos(n.z * 4.2f - n.x * 2.6f) * 0.035f;
                    if (stickney)
                    {
                        float sd = Vector3.Angle(n, stickneyDir) * Mathf.Deg2Rad;
                        float ds = sd / 0.48f;
                        if (ds < 1.25f)
                        {
                            if (ds < 0.95f)
                            {
                                float ts = ds / 0.95f;
                                lump -= (1f - ts * ts) * 0.20f;
                            }
                            lump += Mathf.Exp(-Mathf.Pow((ds - 0.98f) * 5.2f, 2f)) * 0.04f;
                        }
                    }
                    for (int c = 0; c < craterDirs.Length; c++)
                    {
                        float cd = Vector3.Angle(n, craterDirs[c]) * Mathf.Deg2Rad;
                        float dc = cd / craterRadii[c];
                        if (dc < 1.25f)
                        {
                            if (dc < 0.92f)
                            {
                                float tc = dc / 0.92f;
                                lump -= (1f - tc * tc) * 0.055f;
                            }
                            lump += Mathf.Exp(-Mathf.Pow((dc - 0.98f) * 5.0f, 2f)) * 0.018f;
                        }
                    }
                    verts.Add(new Vector3(n.x * radii.x, n.y * radii.y, n.z * radii.z) * (0.5f * lump));
                    uvs.Add(new Vector2(u, v));
                }
            }

            int stride = lonSegs + 1;
            for (int y = 0; y < latSegs; y++)
            {
                for (int x = 0; x < lonSegs; x++)
                {
                    int i0 = y * stride + x;
                    int i1 = i0 + 1;
                    int i2 = (y + 1) * stride + x;
                    int i3 = i2 + 1;
                    // Winding order matching theta = (1 - v) * pi (south->north)
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private Mesh BuildFacetedBasaltMesh(int seed)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = go.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] srcVerts = src.vertices;
            int[] srcTris = src.triangles;
            DestroyImmediate(go);

            // Duplicate vertices per triangle for crisp flat-shaded fractured volcanic basalt look
            var outVerts = new Vector3[srcTris.Length];
            var outTris = new int[srcTris.Length];
            for (int i = 0; i < srcTris.Length; i++)
            {
                Vector3 v = srcVerts[srcTris[i]].normalized;
                float n = Mathf.Sin(v.x * 5.2f + seed) * 0.16f +
                          Mathf.Cos(v.y * 6.8f - seed * 0.5f) * 0.14f +
                          Mathf.Sin(v.z * 7.4f + v.x * 3.1f) * 0.11f;
                float r = 0.5f * (0.82f + n);
                // Flatten slightly on underside and shear obliquely for natural fracture strata
                outVerts[i] = new Vector3(v.x * r * 1.15f, v.y * r * 0.72f, v.z * r * 1.05f);
                outTris[i] = i;
            }
            var m = new Mesh { name = $"MS_FacetedBasalt_{seed}" };
            m.vertices = outVerts;
            m.triangles = outTris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
