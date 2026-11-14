using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MarsSentinel.Bootstrap;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// MarsSceneManager orchestrates the MarsSentinel Unity 6 capability showcase.
    /// Manages UGS anonymous authentication bootstrap, dataset ingestion,
    /// telemetry visualization, Cinemachine 3.x camera rigs, and the uGUI HUD shell.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarsSceneManager : MonoBehaviour
    {
        public static MarsSceneManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private MarsServicesBootstrap servicesBootstrap;
        [SerializeField] private MarsDatasetLoader datasetLoader;
        [SerializeField] private MarsTelemetryController telemetryController;
        [SerializeField] private MarsCameraController cameraController;

        [Header("Celestial & Constellation Controllers (Milestone 8)")]
        [SerializeField] private MarsCelestialOrbitController celestialController;
        [SerializeField] private MarsSatelliteConstellation constellationController;

        [Header("Visual Polish & Animation Controllers (Milestone 9)")]
        [SerializeField] private MarsSimulationAnimator simulationAnimator;
        [SerializeField] private MarsTerrainManager terrainManager;
        [SerializeField] private IngenuityRotorAnimator ingenuityAnimator;

        [Header("Environment & EVA Survival Physics")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Color sunColor = new Color(1.0f, 0.878f, 0.698f, 1.0f); // #FFE0B2
        [SerializeField] private float sunIntensity = 1.3f;
        [SerializeField] private Color ambientColor = new Color(0.769f, 0.478f, 0.322f, 1.0f); // #C47A52
        [Range(0.3f, 3.5f)] [SerializeField] private float dustOpticalDepthTau = 0.45f;

        private int currentGeologyUnitIndex = 0;
        private bool showHistoricalOverlays = true;

        [Header("UI References (uGUI)")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Text txtVisorTitle;
        [SerializeField] private Text txtCoordinates;
        [SerializeField] private Text txtTelemetry;
        [SerializeField] private Text txtRoutes;
        [SerializeField] private Text txtGeology;
        [SerializeField] private Text txtUgsStatus;
        [SerializeField] private Text txtControlsLegend;
        [SerializeField] private Text txtActiveCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureRequiredComponents();
        }

        private void Start()
        {
            ApplyMartianLighting();
            BuildHUDCanvas();
            SubscribeEvents();
        }

        private void Update()
        {
            HandleInputHotkeys();
        }

        private void HandleInputHotkeys()
        {
            bool pressedL = false;
            bool pressedT = false;
            bool pressedG = false;
            bool pressedD = false;
            bool pressedH = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.lKey.wasPressedThisFrame) pressedL = true;
                if (kb.tKey.wasPressedThisFrame) pressedT = true;
                if (kb.gKey.wasPressedThisFrame) pressedG = true;
                if (kb.dKey.wasPressedThisFrame) pressedD = true;
                if (kb.hKey.wasPressedThisFrame) pressedH = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.L)) pressedL = true;
            if (Input.GetKeyDown(KeyCode.T)) pressedT = true;
            if (Input.GetKeyDown(KeyCode.G)) pressedG = true;
            if (Input.GetKeyDown(KeyCode.D)) pressedD = true;
            if (Input.GetKeyDown(KeyCode.H)) pressedH = true;
#endif

            if (pressedL)
            {
                if (celestialController != null)
                {
                    float hr = celestialController.SolHour;
                    if (hr < 8f) celestialController.ApplyPreset(SolarLightingPreset.SolarNoon);
                    else if (hr < 14f) celestialController.ApplyPreset(SolarLightingPreset.Sunset);
                    else if (hr < 18.2f) celestialController.ApplyPreset(SolarLightingPreset.Twilight);
                    else if (hr < 20f) celestialController.ApplyPreset(SolarLightingPreset.Night);
                    else celestialController.ApplyPreset(SolarLightingPreset.Sunrise);
                }
            }
            else if (pressedT)
            {
                if (celestialController != null)
                {
                    celestialController.SetSolHour(celestialController.SolHour + 1.0f);
                }
            }
            else if (pressedG)
            {
                CycleGeologyUnit();
            }
            else if (pressedD)
            {
                CycleDustStormOpticalDepth();
            }
            else if (pressedH)
            {
                showHistoricalOverlays = !showHistoricalOverlays;
                if (telemetryController != null)
                {
                    telemetryController.SetHistoricalOverlaysVisibility(showHistoricalOverlays, showHistoricalOverlays);
                    RefreshRoutesHudText();
                }
            }
        }

        public void CycleGeologyUnit()
        {
            if (datasetLoader != null && datasetLoader.GeologyUnits != null && datasetLoader.GeologyUnits.Count > 0)
            {
                currentGeologyUnitIndex = (currentGeologyUnitIndex + 1) % datasetLoader.GeologyUnits.Count;
                RefreshGeologyHudText();
            }
        }

        public void CycleDustStormOpticalDepth()
        {
            if (dustOpticalDepthTau < 0.7f) dustOpticalDepthTau = 0.90f;
            else if (dustOpticalDepthTau < 1.3f) dustOpticalDepthTau = 1.65f;
            else if (dustOpticalDepthTau < 2.2f) dustOpticalDepthTau = 2.80f;
            else dustOpticalDepthTau = 0.45f;

            ApplyMartianLighting();
            if (telemetryController != null)
            {
                UpdateTelemetryDisplay(telemetryController.CurrentTelemetry);
            }
        }

        public static float ComputeBeerLambertTransmission(float tau, float solarElevationDeg)
        {
            float clampedElev = Mathf.Clamp(solarElevationDeg, 5.0f, 90.0f);
            float cosZenith = Mathf.Max(0.087f, Mathf.Sin(clampedElev * Mathf.Deg2Rad));
            float airMass = 1.0f / cosZenith;
            return Mathf.Exp(-Mathf.Max(0f, tau) * airMass);
        }

        public static float ComputeEvaSafeRadiusMeters(float tau, float remainingO2Fraction = 0.94f)
        {
            // Baseline Mars EVA safe Point-of-No-Return radius (~1800 m nominal at clear sky tau=0.45, reduced under dust storms)
            float clearVisFactor = Mathf.Clamp(1.0f - (tau - 0.45f) * 0.18f, 0.35f, 1.0f);
            return 1800.0f * clearVisFactor * Mathf.Clamp01(remainingO2Fraction);
        }

        private void EnsureRequiredComponents()
        {
            if (datasetLoader == null)
            {
                datasetLoader = FindAnyObjectByType<MarsDatasetLoader>();
                if (datasetLoader == null) datasetLoader = gameObject.AddComponent<MarsDatasetLoader>();
            }

            if (telemetryController == null)
            {
                telemetryController = FindAnyObjectByType<MarsTelemetryController>();
                if (telemetryController == null) telemetryController = gameObject.AddComponent<MarsTelemetryController>();
            }

            if (servicesBootstrap == null)
            {
                servicesBootstrap = FindAnyObjectByType<MarsServicesBootstrap>();
                if (servicesBootstrap == null)
                {
                    GameObject bsGo = new GameObject("MarsServicesBootstrap");
                    servicesBootstrap = bsGo.AddComponent<MarsServicesBootstrap>();
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraController = mainCam.GetComponent<MarsCameraController>();
                if (cameraController == null)
                {
                    cameraController = mainCam.gameObject.AddComponent<MarsCameraController>();
                }
            }

            if (celestialController == null)
            {
                celestialController = FindAnyObjectByType<MarsCelestialOrbitController>();
                if (celestialController == null) celestialController = gameObject.AddComponent<MarsCelestialOrbitController>();
            }

            if (constellationController == null)
            {
                constellationController = FindAnyObjectByType<MarsSatelliteConstellation>();
                if (constellationController == null) constellationController = gameObject.AddComponent<MarsSatelliteConstellation>();
            }

            if (simulationAnimator == null)
            {
                simulationAnimator = FindAnyObjectByType<MarsSimulationAnimator>();
                if (simulationAnimator == null) simulationAnimator = gameObject.AddComponent<MarsSimulationAnimator>();
            }

            EnsureFleetHierarchy();

            // Wire Terrain Manager on MS_Terrain_Jezero
            Transform terrainAnchor = transform.Find("MS_Mars_Fleet_Surface/MS_Terrain_Jezero");
            if (terrainAnchor != null)
            {
                terrainManager = terrainAnchor.GetComponent<MarsTerrainManager>();
                if (terrainManager == null) terrainManager = terrainAnchor.gameObject.AddComponent<MarsTerrainManager>();
            }

            // Wire Ingenuity Rotor Animator on MS_Ingenuity_Root
            Transform ingAnchor = transform.Find("MS_Mars_Fleet_Surface/MS_Ingenuity_Root");
            if (ingAnchor != null)
            {
                ingenuityAnimator = ingAnchor.GetComponent<IngenuityRotorAnimator>();
                if (ingenuityAnimator == null) ingenuityAnimator = ingAnchor.gameObject.AddComponent<IngenuityRotorAnimator>();
            }
        }

        private void EnsureFleetHierarchy()
        {
            // Root for surface fleet
            Transform surfaceFleet = transform.Find("MS_Mars_Fleet_Surface");
            if (surfaceFleet == null)
            {
                GameObject sGo = new GameObject("MS_Mars_Fleet_Surface");
                sGo.transform.SetParent(transform, false);
                surfaceFleet = sGo.transform;
            }

            CreateFleetAnchor("MS_Perseverance_Root", surfaceFleet, new Vector3(0f, 0f, 0f));
            CreateFleetAnchor("MS_Ingenuity_Root", surfaceFleet, new Vector3(10f, 0f, 12f));
            CreateFleetAnchor("MS_Curiosity_Root", surfaceFleet, new Vector3(-8f, 0f, -14f));
            CreateFleetAnchor("MS_InSight_Lander_Root", surfaceFleet, new Vector3(-15f, 0f, 20f));
            CreateFleetAnchor("MS_Opportunity_Root", surfaceFleet, new Vector3(-25f, 0f, 5f));
            CreateFleetAnchor("MS_Viking_Lander_Root", surfaceFleet, new Vector3(22f, 0f, -10f));
            CreateFleetAnchor("MS_Terrain_Jezero", surfaceFleet, Vector3.zero);

            // Root for orbital fleet
            Transform orbitFleet = transform.Find("MS_Mars_Fleet_Orbit");
            if (orbitFleet == null)
            {
                GameObject oGo = new GameObject("MS_Mars_Fleet_Orbit");
                oGo.transform.SetParent(transform, false);
                orbitFleet = oGo.transform;
            }

            CreateFleetAnchor("MS_MRO_Root", orbitFleet, new Vector3(-120f, 350f, 200f));
            CreateFleetAnchor("MS_MAVEN_Root", orbitFleet, new Vector3(80f, 420f, -150f));
            CreateFleetAnchor("MS_Odyssey_Root", orbitFleet, new Vector3(0f, 380f, 300f));
            CreateFleetAnchor("MS_GlobalSurveyor_Root", orbitFleet, new Vector3(100f, 360f, 50f));
        }

        private void CreateFleetAnchor(string name, Transform parent, Vector3 localPos)
        {
            if (parent.Find(name) == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
            }
        }

        private void ApplyMartianLighting()
        {
            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>();
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            if (sunLight != null)
            {
                float elevDeg = celestialController != null ? celestialController.SolarElevationDeg : 45.0f;
                float transmission = ComputeBeerLambertTransmission(dustOpticalDepthTau, elevDeg);
                sunLight.color = Color.Lerp(sunColor, new Color(0.85f, 0.45f, 0.22f, 1.0f), Mathf.Clamp01((dustOpticalDepthTau - 0.45f) / 2.5f));
                sunLight.intensity = sunIntensity * Mathf.Lerp(0.45f, 1.05f, transmission);
                sunLight.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7686f, 0.4784f, 0.3216f, 1.0f); // #C47A52
            RenderSettings.ambientEquatorColor = new Color(0.549f, 0.3098f, 0.196f, 1.0f); // #8C4F32
            RenderSettings.ambientGroundColor = new Color(0.1804f, 0.0941f, 0.0627f, 1.0f); // #2E1810
        }

        private void SubscribeEvents()
        {
            if (servicesBootstrap != null)
            {
                servicesBootstrap.OnSignedIn += HandleUgsSignedIn;
                servicesBootstrap.OnSignInFailed += HandleUgsFailed;
                if (servicesBootstrap.IsSignedIn && !string.IsNullOrEmpty(servicesBootstrap.PlayerId))
                {
                    HandleUgsSignedIn(servicesBootstrap.PlayerId);
                }
            }

            if (datasetLoader != null)
            {
                datasetLoader.OnDataLoaded += HandleDataLoaded;
                if (datasetLoader.IsLoaded)
                {
                    HandleDataLoaded();
                }
            }

            if (telemetryController != null)
            {
                telemetryController.OnTelemetryUpdated += UpdateTelemetryDisplay;
            }

            if (cameraController != null)
            {
                cameraController.OnViewpointChanged += HandleViewpointChanged;
            }
        }

        private void HandleUgsSignedIn(string playerId)
        {
            if (txtUgsStatus != null)
            {
                txtUgsStatus.text = $"UGS IDENTITY: CONNECTED\nPLAYER ID: {playerId.Substring(0, Mathf.Min(16, playerId.Length))}...";
                txtUgsStatus.color = new Color(0.2f, 1.0f, 0.4f);
            }
        }

        private void HandleUgsFailed(string error)
        {
            if (txtUgsStatus != null)
            {
                txtUgsStatus.text = $"UGS IDENTITY: OFFLINE MODE\n(Local Autonomous Operation)";
                txtUgsStatus.color = new Color(1.0f, 0.75f, 0.2f);
            }
        }

        private void HandleDataLoaded()
        {
            RefreshRoutesHudText();
            RefreshGeologyHudText();
            if (telemetryController != null)
            {
                UpdateTelemetryDisplay(telemetryController.CurrentTelemetry);
            }
        }

        private void RefreshRoutesHudText()
        {
            var r = datasetLoader != null ? datasetLoader.RoutesData : null;
            if (txtRoutes != null && r != null)
            {
                int tubes = datasetLoader.SampleTubes != null ? datasetLoader.SampleTubes.Count : 0;
                int flights = datasetLoader.IngenuityFlights != null ? datasetLoader.IngenuityFlights.Count : 0;
                int dropouts = datasetLoader.PerseveranceDropoutsFiltered;
                float curKm = datasetLoader.CuriosityStats != null ? datasetLoader.CuriosityStats.totalLengthMeters / 1000.0f : 0f;
                txtRoutes.text = $"A* TRAVERSE ROUTES (JEZERO CRATER)\n" +
                                 $"• Safest: {r.safest.distance_m:F1}m ({r.safest.max_slope_deg:F1}°) | Short: {r.shortest.distance_m:F1}m | Sci: {r.science.distance_m:F1}m\n" +
                                 $"• Tubes: {tubes}/22 | Ingenuity: {flights} Flights | Dropouts Fixed: {dropouts}\n" +
                                 $"• Curiosity MSL Gale: {curKm:F1} km | History Overlay [H]: {(showHistoricalOverlays ? "ON" : "OFF")}";
            }
        }

        private void RefreshGeologyHudText()
        {
            if (txtGeology != null)
            {
                if (datasetLoader != null && datasetLoader.GeologyUnits != null && datasetLoader.GeologyUnits.Count > 0)
                {
                    int idx = Mathf.Clamp(currentGeologyUnitIndex, 0, datasetLoader.GeologyUnits.Count - 1);
                    var g = datasetLoader.GeologyUnits[idx];
                    string povInfo = datasetLoader.SampleImageryMeta != null
                        ? $"Sol {datasetLoader.SampleImageryMeta.sol} Mastcam-Z Az {datasetLoader.SampleImageryMeta.mastAzDeg:F1}° El {datasetLoader.SampleImageryMeta.mastElDeg:F1}°"
                        : "Mastcam-Z Nominal";
                    txtGeology.text = $"USGS SIM 3292 GEOLOGY [{idx + 1}/{datasetLoader.GeologyUnits.Count}] ([G] Next)\n" +
                                      $"Unit: {g.symbol} ({g.name})\n" +
                                      $"Trafficability: {g.trafficability}/5 | H2O Ice: {g.water_ice_potential}\n" +
                                      $"POV: {povInfo}";
                }
                else
                {
                    Debug.LogWarning("[MarsSceneManager] No geology units loaded from dataset.");
                    txtGeology.text = "USGS SIM 3292 GEOLOGY\n[Warning] No geology units loaded from dataset.";
                }
            }
        }

        private void UpdateTelemetryDisplay(RoverTelemetry t)
        {
            if (txtVisorTitle != null)
            {
                txtVisorTitle.text = "MARSSENTINEL — JEZERO SURFACE SHOWCASE (10 MISSIONS)";
            }

            float elevDeg = celestialController != null ? celestialController.SolarElevationDeg : 45.0f;
            float trans = ComputeBeerLambertTransmission(dustOpticalDepthTau, elevDeg);
            float safeRadius = ComputeEvaSafeRadiusMeters(dustOpticalDepthTau, t.batteryPercent / 100.0f);

            if (txtCoordinates != null)
            {
                float solHr = celestialController != null ? celestialController.SolHour : 12.0f;
                txtCoordinates.text = $"SOL {t.sol} | LMST {solHr:F1}h | SUN ELEV: {elevDeg:F1}° | DUST τ={dustOpticalDepthTau:F2} (Solar {trans * 100f:F0}%)\n" +
                                      $"COORDS: {t.longitudeEastDeg:F4}°E, {t.latitudeNorthDeg:F4}°N | ELEV: {t.elevationMeters:F1}m";
            }

            if (txtTelemetry != null)
            {
                txtTelemetry.text = $"EVA & ROVER TELEMETRY (PERSEVERANCE + SUIT)\n" +
                                    $"• Speed: {t.currentSpeedMps:F2} m/s | Hdg: {t.headingDeg:F1}° | Slope: {t.currentSlopeDeg:F1}°\n" +
                                    $"• Beer-Lambert Solar Flux: {586.2f * trans:F0} W/m² (τ={dustOpticalDepthTau:F2})\n" +
                                    $"• EVA O2 Reserve: {t.batteryPercent:F0}% | PNR Safe Radius: {safeRadius:F0} m\n" +
                                    $"• Traveled: {t.distanceTraveledMeters:F2} m | {t.trafficabilityStatus}";
            }
        }

        private void HandleViewpointChanged(CameraViewpoint vp)
        {
            if (txtActiveCamera != null)
            {
                txtActiveCamera.text = $"ACTIVE VIEW: [{ (int)vp + 1 }] {vp.ToString().ToUpper()}";
            }
        }

        #region uGUI HUD Generation
        private void BuildHUDCanvas()
        {
            Transform existing = transform.Find("MarsSentinelHUDCanvas");
            if (existing != null)
            {
                hudCanvas = existing.GetComponent<Canvas>();
                return;
            }

            GameObject canvasGo = new GameObject("MarsSentinelHUDCanvas");
            canvasGo.transform.SetParent(transform, false);

            hudCanvas = canvasGo.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Visor Header Panel (Top-Center)
            GameObject headerPanel = CreatePanel("VisorHeaderPanel", canvasGo.transform,
                new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f),
                new Vector2(0, -20), new Vector2(760, 90));
            txtVisorTitle = CreateText("TxtTitle", headerPanel.transform, "MARSSENTINEL — JEZERO SURFACE SHOWCASE",
                18, FontStyle.Bold, new Color(1.0f, 0.85f, 0.5f), TextAnchor.MiddleCenter, defaultFont);
            txtVisorTitle.rectTransform.anchoredPosition = new Vector2(0, 18);
            txtCoordinates = CreateText("TxtCoordinates", headerPanel.transform, "SOL 100 | COORDS: 77.4508°E, 18.4447°N | ELEV: -2569.9m",
                14, FontStyle.Normal, new Color(0.3f, 0.9f, 1.0f), TextAnchor.MiddleCenter, defaultFont);
            txtCoordinates.rectTransform.anchoredPosition = new Vector2(0, -18);

            // Telemetry Panel (Top-Left)
            GameObject telemPanel = CreatePanel("TelemetryPanel", canvasGo.transform,
                new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f),
                new Vector2(25, -25), new Vector2(380, 200));
            txtTelemetry = CreateText("TxtTelemetry", telemPanel.transform,
                "ROVER TELEMETRY (PERSEVERANCE M20)\n• Speed: 0.12 m/s\n• Heading: 284.5° WNW\n• Slope: 4.2° (Safe)\n• Traveled: 1159.95 m\n• Power: RTG Nominal (94%)",
                13, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, defaultFont);

            // Traverse Routes Panel (Bottom-Left)
            GameObject routesPanel = CreatePanel("RoutesPanel", canvasGo.transform,
                new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f),
                new Vector2(25, 25), new Vector2(380, 160));
            txtRoutes = CreateText("TxtRoutes", routesPanel.transform,
                "A* TRAVERSE ROUTES (JEZERO CRATER)\n• Safest Path:   1159.9 m (Slope: 6.5°)\n• Shortest Path: 1113.5 m (Slope: 7.1°)\n• Science Path:  1203.2 m (Slope: 6.8°)",
                13, FontStyle.Normal, new Color(0.2f, 1.0f, 0.8f), TextAnchor.UpperLeft, defaultFont);

            // Geology Panel (Bottom-Right)
            GameObject geoPanel = CreatePanel("GeologyPanel", canvasGo.transform,
                new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(-25, 25), new Vector2(400, 160));
            txtGeology = CreateText("TxtGeology", geoPanel.transform,
                "USGS SIM 3292 GEOLOGY\nLoading geological dataset...",
                13, FontStyle.Normal, new Color(1.0f, 0.8f, 0.4f), TextAnchor.UpperLeft, defaultFont);

            // UGS Identity Status & Camera Legend Panel (Top-Right)
            GameObject statusPanel = CreatePanel("StatusAndControlsPanel", canvasGo.transform,
                new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f),
                new Vector2(-25, -25), new Vector2(420, 225));
            txtUgsStatus = CreateText("TxtUgsStatus", statusPanel.transform,
                "UGS IDENTITY: CONNECTING...",
                13, FontStyle.Bold, new Color(0.4f, 0.8f, 1.0f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtUgsStatus.rectTransform, 10f, 44f);

            txtActiveCamera = CreateText("TxtActiveCamera", statusPanel.transform,
                "ACTIVE VIEW: [1] SURFACEFLEETOVERVIEW",
                13, FontStyle.Bold, new Color(1.0f, 0.95f, 0.2f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtActiveCamera.rectTransform, 58f, 42f);

            txtControlsLegend = CreateText("TxtControlsLegend", statusPanel.transform,
                "CAMERAS: [1]-[5] | [L] Solar | [T] +1h | [D] Dust τ | [G] Geo | [H] History\n[1] Surface Fleet | [2] Rover Close-Up | [3] Ingenuity\n[4] Route Orbit | [5] Orbital Fleet Skyward",
                12, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtControlsLegend.rectTransform, 106f, 110f);
        }

        private GameObject CreatePanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            GameObject panelGo = new GameObject(name);
            panelGo.transform.SetParent(parent, false);

            RectTransform rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = panelGo.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.12f, 0.82f); // Dark translucent visor glass
            return panelGo;
        }

        private Text CreateText(string name, Transform parent, string content,
            int fontSize, FontStyle style, Color color, TextAnchor alignment, Font font)
        {
            GameObject textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);

            RectTransform rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = new Vector2(-20, -20); // 10px margin padding
            rt.anchoredPosition = Vector2.zero;

            Text txt = textGo.AddComponent<Text>();
            txt.text = content;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;

            return txt;
        }

        private void SetTopTextSlot(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-20f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }
        #endregion
    }
}
