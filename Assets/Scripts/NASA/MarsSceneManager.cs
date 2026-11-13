using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

        [Header("Environment & Lighting")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Color sunColor = new Color(1.0f, 0.878f, 0.698f, 1.0f); // #FFE0B2
        [SerializeField] private float sunIntensity = 1.3f;
        [SerializeField] private Color ambientColor = new Color(0.769f, 0.478f, 0.322f, 1.0f); // #C47A52

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

            EnsureFleetHierarchy();
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
                sunLight.color = sunColor;
                sunLight.intensity = sunIntensity;
                sunLight.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientSkyColor = ambientColor;
        }

        private void SubscribeEvents()
        {
            if (servicesBootstrap != null)
            {
                servicesBootstrap.OnSignedIn += HandleUgsSignedIn;
                servicesBootstrap.OnSignInFailed += HandleUgsFailed;
            }

            if (datasetLoader != null)
            {
                datasetLoader.OnDataLoaded += HandleDataLoaded;
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
            var r = datasetLoader.RoutesData;
            if (txtRoutes != null && r != null)
            {
                txtRoutes.text = $"A* TRAVERSE ROUTES (JEZERO CRATER)\n" +
                                 $"• Safest Path:   {r.safest.distance_m:F1} m (Slope: {r.safest.max_slope_deg:F1}°)\n" +
                                 $"• Shortest Path: {r.shortest.distance_m:F1} m (Slope: {r.shortest.max_slope_deg:F1}°)\n" +
                                 $"• Science Path:  {r.science.distance_m:F1} m (Slope: {r.science.max_slope_deg:F1}°)";
            }

            if (txtGeology != null)
            {
                if (datasetLoader.GeologyUnits != null && datasetLoader.GeologyUnits.Count > 0)
                {
                    var g = datasetLoader.GeologyUnits[0];
                    txtGeology.text = $"USGS SIM 3292 GEOLOGY\n" +
                                      $"Unit: {g.symbol} ({g.name})\n" +
                                      $"System: {g.system} | Trafficability: {g.trafficability}/5\n" +
                                      $"Note: {g.trafficability_desc}";
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
                txtVisorTitle.text = "MARSSENTINEL — JEZERO SURFACE SHOWCASE";
            }

            if (txtCoordinates != null)
            {
                txtCoordinates.text = $"SOL {t.sol} | SITE: {t.siteName}\n" +
                                      $"COORDS: {t.longitudeEastDeg:F4}°E, {t.latitudeNorthDeg:F4}°N | ELEV: {t.elevationMeters:F1}m";
            }

            if (txtTelemetry != null)
            {
                txtTelemetry.text = $"ROVER TELEMETRY (PERSEVERANCE M20)\n" +
                                    $"• Speed:     {t.currentSpeedMps:F2} m/s\n" +
                                    $"• Heading:   {t.headingDeg:F1}° WNW\n" +
                                    $"• Slope:     {t.currentSlopeDeg:F1}° ({t.trafficabilityStatus})\n" +
                                    $"• Traveled:  {t.distanceTraveledMeters:F2} m\n" +
                                    $"• Power:     {t.batteryStatus}";
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
                new Vector2(-25, -25), new Vector2(400, 220));
            txtUgsStatus = CreateText("TxtUgsStatus", statusPanel.transform,
                "UGS IDENTITY: CONNECTING...",
                13, FontStyle.Bold, new Color(0.4f, 0.8f, 1.0f), TextAnchor.UpperLeft, defaultFont);
            txtUgsStatus.rectTransform.anchoredPosition = new Vector2(0, 0);

            txtActiveCamera = CreateText("TxtActiveCamera", statusPanel.transform,
                "ACTIVE VIEW: [1] SURFACEFLEETOVERVIEW",
                13, FontStyle.Bold, new Color(1.0f, 0.95f, 0.2f), TextAnchor.UpperLeft, defaultFont);
            txtActiveCamera.rectTransform.anchoredPosition = new Vector2(0, -50);

            txtControlsLegend = CreateText("TxtControlsLegend", statusPanel.transform,
                "CAMERAS:\n[1] Surface Fleet | [2] Rover Close-Up\n[3] Ingenuity Scout | [4] Route Orbit\n[5] Orbital Fleet Skyward",
                12, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f), TextAnchor.UpperLeft, defaultFont);
            txtControlsLegend.rectTransform.anchoredPosition = new Vector2(0, -95);
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
        #endregion
    }
}
