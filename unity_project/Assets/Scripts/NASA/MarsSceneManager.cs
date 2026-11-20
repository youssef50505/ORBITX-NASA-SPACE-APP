using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        public float DustOpticalDepthTau => dustOpticalDepthTau;

        public static string DirectorHudLabel { get; private set; } = string.Empty;

        private int currentGeologyUnitIndex = 0;
        private bool showHistoricalOverlays = false;
        private int currentLayerCycleIdx = 0;
        private static readonly NasaDataLayerMode[] HCycleOrder =
        {
            NasaDataLayerMode.SafestRouteFocus,
            NasaDataLayerMode.AllThreeAStarRoutes,
            NasaDataLayerMode.HistoricalM20AndIngenuity,
            NasaDataLayerMode.CleanCinemaView
        };

        // Live spatial NASA lookup & manual [G] override tracking
        private bool manualGeologyOverride = false;
        private Vector3 manualGeologyAnchorPos = Vector3.zero;
        private string liveNearestOutcrop = "Octavia E. Butler";
        private float liveNearestOutcropDistM = 0f;
        private string liveGeoSymbol = "Nh_lf";
        private string liveGeoName = "Noachian Jezero Layered Delta";
        private int liveGeoTrafficability = 4;
        private string liveMountSharpNote = "MSL Gale Analog · Mount Sharp";
        private float liveDistToRouteM = 0f;
        private float liveDistToGoalM = 0f;

        // 5-Step Marswalk Mission Checklist state
        private bool step1EvaMeda = false;
        private bool step2OutcropScan = false;
        private bool step3RoverDrive = false;
        private bool step4HydroponicsO2 = false;
        private bool step5StormShelter = false;

        // [P] 60-Second & [N] 28-Second Presentation Auto-Director state
        private bool isPresentationDirectorActive = false;
        private bool isNasa30SecMode = false;
        private float directorElapsed = 0f;
        private int lastDirectorAct = -1;
        private int directorSubStep = -1;
        public bool IsPresentationDirectorActive => isPresentationDirectorActive;
        public bool IsNasa30SecMode => isPresentationDirectorActive && isNasa30SecMode;

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

        private float hudRefreshTimer = 0f;
        private Text txtMuteBtnLabel;
        private Text txtRoverBtnLabel;
        private Text txtAutoBtnLabel;
        private Text txtHudToggleBtnLabel;
        private Text txtDemoBtnLabel;
        private Text txtNasaDemoBtnLabel;

        // Modern 3D-First HUD elements (Clean by default; [M] toggles Full Telemetry drawers)
        private bool isFullHudVisible = false;
        private GameObject panelTelemetry;
        private GameObject panelRoutes;
        private GameObject panelGeology;
        private GameObject panelStatus;
        private GameObject panelAlertToast;
        private Text txtAlertToast;
        private RectTransform objectiveMarkerRect;
        private Text txtObjectiveMarker;
        private RectTransform barFillO2;
        private RectTransform barFillPwr;
        private RectTransform barFillRad;
        private RectTransform barFillRf;
        private Image imgFillO2;
        private Image imgFillRad;
        private Image imgFillRf;
        private Text txtVitalO2;
        private Text txtVitalPwr;
        private Text txtVitalRad;
        private Text txtVitalRf;
        private Text txtMissionObjective;

        private void Update()
        {
            if (Instance == null) Instance = this;

            HandleInputHotkeys();

            if (isPresentationDirectorActive)
            {
                UpdatePresentationDirector();
            }

            UpdateSurvivalBarsAndObjectiveMarker();

            hudRefreshTimer += Time.deltaTime;
            if (hudRefreshTimer >= 0.25f)
            {
                hudRefreshTimer = 0f;
                RefreshLiveExplorationHud();
            }
        }

        private void HandleInputHotkeys()
        {
            bool pressedL = false;
            bool pressedT = false;
            bool pressedG = false;
            bool pressedD = false;
            bool pressedH = false;
            bool pressedP = false;
            bool pressedN = false;
            bool pressedY = false;
            bool pressedQ = false;
            bool pressedJ = false;
            bool pressedR = false;

            bool exploreActive = MarsVisualRealismPolish.Instance != null && MarsVisualRealismPolish.Instance.IsExploreModeActive;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.lKey.wasPressedThisFrame) pressedL = true;
                if (kb.tKey.wasPressedThisFrame) pressedT = true;
                if (kb.gKey.wasPressedThisFrame) pressedG = true;
                if (kb.cKey.wasPressedThisFrame || (!exploreActive && kb.dKey.wasPressedThisFrame)) pressedD = true;
                if (kb.hKey.wasPressedThisFrame) pressedH = true;
                if (kb.pKey.wasPressedThisFrame) pressedP = true;
                if (kb.nKey.wasPressedThisFrame) pressedN = true;
                if (kb.yKey.wasPressedThisFrame) pressedY = true;
                if (kb.qKey.wasPressedThisFrame) pressedQ = true;
                if (kb.jKey.wasPressedThisFrame) pressedJ = true;
                if (kb.rKey.wasPressedThisFrame && (MarsRoverVehicleController.Instance == null || !MarsRoverVehicleController.Instance.IsDriverSeated)) pressedR = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.P)) pressedP = true;
            if (Input.GetKeyDown(KeyCode.N)) pressedN = true;
            if (Input.GetKeyDown(KeyCode.Y)) pressedY = true;
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
                CycleDataLayerMode();
            }
            else if (pressedP)
            {
                TogglePresentationDirector();
            }
            else if (pressedN)
            {
                ToggleNasa30SecDemo();
            }
            else if (pressedY)
            {
                ToggleFullHud();
            }
            else if (pressedQ)
            {
                MarsCtxNavSolarEngine.Instance?.CyclePreset();
            }
            else if (pressedJ)
            {
                MarsCtxNavSolarEngine.Instance?.AdvanceSolarSeason(30f);
            }
            else if (pressedR)
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsRoverVehicleController.Instance?.ToggleAutopilot();
            }
        }

        public void ToggleFullHud()
        {
            SetFullHudVisible(!isFullHudVisible);
            MarsAudioManager.Instance?.PlayUiClick();
        }

        public void SetFullHudVisible(bool visible)
        {
            isFullHudVisible = visible;
            if (panelTelemetry != null) panelTelemetry.SetActive(visible);
            if (panelRoutes != null) panelRoutes.SetActive(visible);
            if (panelGeology != null) panelGeology.SetActive(visible);
            if (panelStatus != null) panelStatus.SetActive(visible);
            if (txtHudToggleBtnLabel != null)
            {
                txtHudToggleBtnLabel.text = visible ? "HUD: FULL [Y]" : "HUD: CLEAN [Y]";
            }
        }

        public void CycleDataLayerMode()
        {
            currentLayerCycleIdx = (currentLayerCycleIdx + 1) % HCycleOrder.Length;
            ApplyLayerModeSelection(HCycleOrder[currentLayerCycleIdx]);
            MarsAudioManager.Instance?.PlayUiClick();
        }

        private void ApplyLayerModeSelection(NasaDataLayerMode mode)
        {
            showHistoricalOverlays = mode == NasaDataLayerMode.HistoricalM20AndIngenuity
                || mode == NasaDataLayerMode.FullLayeredMap;

            if (telemetryController != null)
            {
                telemetryController.ApplyDataLayerMode(mode);
                telemetryController.SetHistoricalOverlaysVisibility(showHistoricalOverlays, showHistoricalOverlays);
            }
            RefreshRoutesHudText();
            RefreshLiveExplorationHud();
        }

        public void TogglePresentationDirector()
        {
            if (isPresentationDirectorActive && !isNasa30SecMode)
            {
                StopPresentationDirector();
            }
            else
            {
                StartPresentationDirector(nasa30SecMode: false);
            }
            MarsAudioManager.Instance?.PlayUiClick();
        }

        public void ToggleNasa30SecDemo()
        {
            if (isPresentationDirectorActive && isNasa30SecMode)
            {
                StopPresentationDirector();
            }
            else
            {
                StartPresentationDirector(nasa30SecMode: true);
            }
            MarsAudioManager.Instance?.PlayUiClick();
        }

        public void StartPresentationDirector(bool nasa30SecMode = false)
        {
            isPresentationDirectorActive = true;
            isNasa30SecMode = nasa30SecMode;
            directorElapsed = 0f;
            lastDirectorAct = -1;
            directorSubStep = -1;
            UpdateDirectorButtonLabel();
            UpdatePresentationDirector();
        }

        public void StopPresentationDirector()
        {
            isPresentationDirectorActive = false;
            isNasa30SecMode = false;
            directorElapsed = 0f;
            lastDirectorAct = -1;
            directorSubStep = -1;
            DirectorHudLabel = string.Empty;
            MarsInteractionSystem.Instance?.CloseInspector();
            dustOpticalDepthTau = 0.45f;
            ApplyMartianLighting();
            MarsAudioManager.Instance?.SetDustOpticalDepth(dustOpticalDepthTau);
            currentLayerCycleIdx = 0;
            ApplyLayerModeSelection(NasaDataLayerMode.SafestRouteFocus);
            MarsVisualRealismPolish.Instance?.TriggerViewMode(1);
            UpdateDirectorButtonLabel();
            RefreshLiveExplorationHud();
        }

        private void UpdateDirectorButtonLabel()
        {
            if (txtDemoBtnLabel != null)
            {
                txtDemoBtnLabel.text = (isPresentationDirectorActive && !isNasa30SecMode) ? "Stop [P]" : "[P] 1m Demo";
            }
            if (txtNasaDemoBtnLabel != null)
            {
                txtNasaDemoBtnLabel.text = (isPresentationDirectorActive && isNasa30SecMode) ? "Stop [N]" : "[N] 30s NASA";
            }
        }

        private void UpdatePresentationDirector()
        {
            var polish = MarsVisualRealismPolish.Instance;
            var astro = MarsAstronautController.Instance;
            var rover = MarsRoverVehicleController.Instance;
            var interaction = MarsInteractionSystem.Instance;

            float t = directorElapsed;
            directorElapsed += Time.deltaTime;

            if (isNasa30SecMode)
            {
                // Strictly <= 28.0 seconds for NASA Space Apps Global 30-Second Video Limit!
                if (t >= 28.0f)
                {
                    StopPresentationDirector();
                    return;
                }
                if (t < 5.5f)
                {
                    if (lastDirectorAct != 1)
                    {
                        lastDirectorAct = 1;
                        interaction?.CloseInspector();
                        if (rover != null && rover.IsDriverSeated) rover.DismountDriver();
                        polish?.SetOrbitView(true);
                    }
                    DirectorHudLabel = $"NASA 30s DEMO ({t:F1}/28s): ORBITAL SUN, PHOBOS, DEIMOS & JEZERO";
                }
                else if (t < 11.0f)
                {
                    if (lastDirectorAct != 2)
                    {
                        lastDirectorAct = 2;
                        polish?.TriggerViewMode(4);
                        ApplyLayerModeSelection(NasaDataLayerMode.AllThreeAStarRoutes);
                    }
                    DirectorHudLabel = $"NASA 30s DEMO ({t:F1}/28s): HIRISE 4m DTM & 3 A* SAFE ROUTES";
                }
                else if (t < 17.5f)
                {
                    if (lastDirectorAct != 3)
                    {
                        lastDirectorAct = 3;
                        directorSubStep = 0;
                        polish?.EnterExploreMode();
                        interaction?.InspectPointUnderPlayer();
                        step2OutcropScan = true;
                    }
                    if (t >= 14.5f && directorSubStep < 1)
                    {
                        directorSubStep = 1;
                        MarsInspectableTarget meteorite = FindInspectableByKeyword("cacao") ?? FindInspectableByKeyword("cheyava");
                        if (meteorite != null) interaction?.OpenInspector(meteorite);
                    }
                    DirectorHudLabel = $"NASA 30s DEMO ({t:F1}/28s): OMNI-PROBE RIMFAX X-RAY & METEORITE";
                }
                else if (t < 23.0f)
                {
                    if (lastDirectorAct != 4)
                    {
                        lastDirectorAct = 4;
                        interaction?.CloseInspector();
                        polish?.EnterExploreMode();
                        if (rover != null && !rover.IsDriverSeated) rover.BoardDriver();
                        step3RoverDrive = true;
                        dustOpticalDepthTau = 1.65f;
                        ApplyMartianLighting();
                        MarsAudioManager.Instance?.SetDustOpticalDepth(dustOpticalDepthTau);
                        step5StormShelter = true;
                    }
                    DirectorHudLabel = $"NASA 30s DEMO ({t:F1}/28s): 6x6 ROVER DRIVE & BEER-LAMBERT DUST";
                }
                else
                {
                    if (lastDirectorAct != 5)
                    {
                        lastDirectorAct = 5;
                        dustOpticalDepthTau = 0.45f;
                        ApplyMartianLighting();
                        if (rover != null && rover.IsDriverSeated) rover.DismountDriver();
                        polish?.EnterExploreMode();
                        astro?.TeleportToFacility("Agri");
                        step4HydroponicsO2 = true;
                    }
                    DirectorHudLabel = $"NASA 30s DEMO ({t:F1}/28s): AGRI O2/H2O + MED SHELTER · READY";
                }
                if (txtActiveCamera != null) txtActiveCamera.text = DirectorHudLabel;
                return;
            }

            if (t >= 60.0f)
            {
                StopPresentationDirector();
                return;
            }

            if (t < 10.0f)
            {
                // Act 1 (0-10s): Orbital Constellation [5] showing Sun, Phobos, Deimos & NASA orbiters
                if (lastDirectorAct != 1)
                {
                    lastDirectorAct = 1;
                    directorSubStep = 0;
                    interaction?.CloseInspector();
                    if (rover != null && rover.IsDriverSeated) rover.DismountDriver();
                    polish?.SetOrbitView(true);
                }
                DirectorHudLabel = $"1m DEMO ACT 1/6 ({t:F0}s): JEZERO ORBITAL CONSTELLATION [5]";
            }
            else if (t < 20.0f)
            {
                // Act 2 (10-20s): Route Orbit [4] (All 3 A* corridors) -> Surface Fleet [1]
                if (lastDirectorAct != 2)
                {
                    lastDirectorAct = 2;
                    directorSubStep = 0;
                    polish?.TriggerViewMode(4);
                    ApplyLayerModeSelection(NasaDataLayerMode.AllThreeAStarRoutes);
                }
                if (t >= 15.0f && directorSubStep < 1)
                {
                    directorSubStep = 1;
                    polish?.TriggerViewMode(1);
                    ApplyLayerModeSelection(NasaDataLayerMode.SafestRouteFocus);
                }
                DirectorHudLabel = t < 15.0f
                    ? $"1m DEMO ACT 2/6 ({t:F0}s): ALL 3 A* TRAVERSE ROUTES [4]"
                    : $"1m DEMO ACT 2/6 ({t:F0}s): JEZERO BASE & NASA FLEET [1]";
            }
            else if (t < 30.0f)
            {
                // Act 3 (20-30s): EVA Explore [F] + Cheyava Falls Biosignature Science Inspector
                if (lastDirectorAct != 3)
                {
                    lastDirectorAct = 3;
                    directorSubStep = 0;
                    if (rover != null && rover.IsDriverSeated) rover.DismountDriver();
                    polish?.EnterExploreMode();
                    MarsInspectableTarget cheyava = FindInspectableByKeyword("cheyava");
                    if (astro != null)
                    {
                        if (cheyava != null)
                        {
                            Vector3 tp = cheyava.transform.position;
                            astro.SnapToSurfaceAt(tp.x - 4.2f, tp.z - 4.2f);
                            astro.SetCameraAngles(45f, 8f);
                        }
                        else
                        {
                            astro.TeleportToFacility("Spawn");
                        }
                    }
                }
                if (t >= 22.0f && directorSubStep < 1)
                {
                    directorSubStep = 1;
                    MarsInspectableTarget cheyava = FindInspectableByKeyword("cheyava");
                    if (cheyava != null)
                    {
                        interaction?.OpenInspector(cheyava);
                        step2OutcropScan = true;
                    }
                }
                DirectorHudLabel = $"1m DEMO ACT 3/6 ({t:F0}s): EVA [F] + CHEYAVA FALLS INSPECTOR";
            }
            else if (t < 40.0f)
            {
                // Act 4 (30-40s): Close drawer + Board/Drive 6x6 Rover + Dust/Radiation demo
                if (lastDirectorAct != 4)
                {
                    lastDirectorAct = 4;
                    directorSubStep = 0;
                    interaction?.CloseInspector();
                    polish?.EnterExploreMode();
                    if (rover != null && !rover.IsDriverSeated) rover.BoardDriver();
                    step3RoverDrive = true;
                }
                if (t >= 34.0f && directorSubStep < 1)
                {
                    directorSubStep = 1;
                    dustOpticalDepthTau = 1.65f;
                    ApplyMartianLighting();
                    MarsAudioManager.Instance?.SetDustOpticalDepth(dustOpticalDepthTau);
                    step5StormShelter = true;
                }
                if (t >= 38.5f && directorSubStep < 2)
                {
                    directorSubStep = 2;
                    dustOpticalDepthTau = 0.45f;
                    ApplyMartianLighting();
                    MarsAudioManager.Instance?.SetDustOpticalDepth(dustOpticalDepthTau);
                }
                DirectorHudLabel = $"1m DEMO ACT 4/6 ({t:F0}s): 6x6 ROVER DRIVE + RAD STORM [D/C] Dust τ";
            }
            else if (t < 50.0f)
            {
                // Act 5 (40-50s): Hydroponics Agri Dome & NASA HDU Habitat
                if (lastDirectorAct != 5)
                {
                    lastDirectorAct = 5;
                    directorSubStep = 0;
                    if (rover != null && rover.IsDriverSeated) rover.DismountDriver();
                    polish?.EnterExploreMode();
                    astro?.TeleportToFacility("Agri");
                    step4HydroponicsO2 = true;
                }
                if (t >= 45.0f && directorSubStep < 1)
                {
                    directorSubStep = 1;
                    astro?.TeleportToFacility("Habitat");
                }
                DirectorHudLabel = t < 45.0f
                    ? $"1m DEMO ACT 5/6 ({t:F0}s): HYDROPONICS AGRI DOME · O2/H2O"
                    : $"1m DEMO ACT 5/6 ({t:F0}s): NASA HDU & MARK III SUITPORT";
            }
            else
            {
                // Act 6 (50-60s): Med Bay Solar Storm Shelter & Mars Ascent Rocket Pad
                if (lastDirectorAct != 6)
                {
                    lastDirectorAct = 6;
                    directorSubStep = 0;
                    polish?.EnterExploreMode();
                    astro?.TeleportToFacility("Medical");
                    step5StormShelter = true;
                }
                if (t >= 55.0f && directorSubStep < 1)
                {
                    directorSubStep = 1;
                    astro?.TeleportToFacility("Rocket");
                }
                DirectorHudLabel = t < 55.0f
                    ? $"1m DEMO ACT 6/6 ({t:F0}s): RAD SOLAR STORM MED BAY SHELTER"
                    : $"1m DEMO ACT 6/6 ({t:F0}s): MARS ASCENT ROCKET PAD · READY";
            }

            if (txtActiveCamera != null)
            {
                txtActiveCamera.text = DirectorHudLabel;
            }
        }

        private static MarsInspectableTarget FindInspectableByKeyword(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return null;
            string kw = keyword.ToLowerInvariant();
            for (int i = 0; i < MarsInspectableTarget.AllTargets.Count; i++)
            {
                var item = MarsInspectableTarget.AllTargets[i];
                if (item == null) continue;
                string id = (item.targetId ?? "").ToLowerInvariant();
                string title = (item.title ?? "").ToLowerInvariant();
                if (id.Contains(kw) || title.Contains(kw)) return item;
            }
            return MarsInspectableTarget.AllTargets.Count > 0 ? MarsInspectableTarget.AllTargets[0] : null;
        }

        public void CycleGeologyUnit()
        {
            if (datasetLoader != null && datasetLoader.GeologyUnits != null && datasetLoader.GeologyUnits.Count > 0)
            {
                currentGeologyUnitIndex = (currentGeologyUnitIndex + 1) % datasetLoader.GeologyUnits.Count;
                manualGeologyOverride = true;
                manualGeologyAnchorPos = GetActivePlayerWorldPosition();
                RefreshGeologyHudText();
            }
        }

        public void CycleDustStormOpticalDepth()
        {
            if (dustOpticalDepthTau < 0.7f) dustOpticalDepthTau = 0.90f;
            else if (dustOpticalDepthTau < 1.3f) dustOpticalDepthTau = 1.65f;
            else if (dustOpticalDepthTau < 2.2f) dustOpticalDepthTau = 2.80f;
            else dustOpticalDepthTau = 0.45f;

            step5StormShelter = true;
            ApplyMartianLighting();
            MarsAudioManager.Instance?.SetDustOpticalDepth(dustOpticalDepthTau);
            RefreshLiveExplorationHud();
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

            EnsureGameplaySystemsAndEventSystem();
        }

        private void EnsureGameplaySystemsAndEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<InputSystemUIInputModule>();
#else
                esGo.AddComponent<StandaloneInputModule>();
#endif
            }

            if (FindAnyObjectByType<MarsAudioManager>() == null)
            {
                GameObject go = new GameObject("MS_AudioManager");
                go.AddComponent<MarsAudioManager>();
            }

            if (FindAnyObjectByType<MarsBaseFacilitiesBuilder>() == null)
            {
                GameObject go = new GameObject("MS_BaseFacilitiesBuilder");
                go.AddComponent<MarsBaseFacilitiesBuilder>();
            }

            if (FindAnyObjectByType<MarsRoverVehicleController>() == null)
            {
                GameObject go = new GameObject("MS_Pressurized_Rover");
                go.AddComponent<MarsRoverVehicleController>();
            }

            if (FindAnyObjectByType<MarsAstronautController>() == null)
            {
                GameObject go = new GameObject("MS_Astronaut_Player");
                go.AddComponent<MarsAstronautController>();
            }

            if (FindAnyObjectByType<MarsInteractionSystem>() == null)
            {
                GameObject go = new GameObject("MS_InteractionSystem");
                go.AddComponent<MarsInteractionSystem>();
            }

            if (FindAnyObjectByType<MarsCtxNavSolarEngine>() == null)
            {
                GameObject go = new GameObject("MS_CtxNavSolarEngine");
                go.AddComponent<MarsCtxNavSolarEngine>();
            }

            if (FindAnyObjectByType<MarsSurvivalMissionController>() == null)
            {
                GameObject go = new GameObject("MS_SurvivalMissionController");
                go.AddComponent<MarsSurvivalMissionController>();
            }

            if (FindAnyObjectByType<MarsCinematicVfxAndPost>() == null)
            {
                GameObject go = new GameObject("MS_CinematicVfxAndPost");
                go.AddComponent<MarsCinematicVfxAndPost>();
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
            UpdateSpatialNasaAndChecklist();
            RefreshRoutesHudText();
            RefreshGeologyHudText();
            if (telemetryController != null)
            {
                UpdateTelemetryDisplay(telemetryController.CurrentTelemetry);
            }
        }

        private Vector3 GetActivePlayerWorldPosition()
        {
            if (MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated)
            {
                return MarsRoverVehicleController.Instance.transform.position;
            }
            if (MarsAstronautController.Instance != null)
            {
                return MarsAstronautController.Instance.transform.position;
            }
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        private int GetCompletedChecklistCount()
        {
            int count = 0;
            if (step1EvaMeda) count++;
            if (step2OutcropScan) count++;
            if (step3RoverDrive) count++;
            if (step4HydroponicsO2) count++;
            if (step5StormShelter) count++;
            return count;
        }

        private void UpdateSpatialNasaAndChecklist()
        {
            Vector3 playerPos = GetActivePlayerWorldPosition();

            if (manualGeologyOverride)
            {
                float movedXZ = Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.z),
                    new Vector2(manualGeologyAnchorPos.x, manualGeologyAnchorPos.z));
                if (movedXZ > 4.0f)
                {
                    manualGeologyOverride = false;
                }
            }

            if (telemetryController != null)
            {
                telemetryController.GetLiveNavigationMetrics(
                    playerPos,
                    out liveNearestOutcrop,
                    out liveNearestOutcropDistM,
                    out liveGeoSymbol,
                    out liveGeoName,
                    out liveGeoTrafficability,
                    out liveMountSharpNote,
                    out liveDistToRouteM,
                    out liveDistToGoalM,
                    out _);
            }

            // Evaluate 5-Step Marswalk Mission Checklist
            if (datasetLoader != null && datasetLoader.EnvironmentData != null)
            {
                step1EvaMeda = true;
            }
            if (MarsInspectableTarget.DiscoveredCount > 0 ||
                (MarsInteractionSystem.Instance != null && MarsInteractionSystem.Instance.SelectedTarget != null))
            {
                step2OutcropScan = true;
            }
            if (MarsRoverVehicleController.Instance != null &&
                (MarsRoverVehicleController.Instance.IsDriverSeated || MarsRoverVehicleController.Instance.TotalDistanceMeters > 1.0f))
            {
                step3RoverDrive = true;
            }

            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);
            string zoneName = MarsAstronautController.Instance != null ? (MarsAstronautController.Instance.ActiveZoneName ?? "") : "";
            string zoneLower = zoneName.ToLowerInvariant();

            if (Vector2.Distance(playerXZ, MarsBaseFacilitiesBuilder.POS_AGRICULTURE) <= 14.0f ||
                Vector2.Distance(playerXZ, MarsBaseFacilitiesBuilder.POS_HABITAT_HDU) <= 14.0f ||
                zoneLower.Contains("agri") || zoneLower.Contains("hydro") || zoneLower.Contains("hdu") || zoneLower.Contains("habitat"))
            {
                step4HydroponicsO2 = true;
            }

            if (Vector2.Distance(playerXZ, MarsBaseFacilitiesBuilder.POS_MEDICAL_BAY) <= 14.0f ||
                zoneLower.Contains("med") || zoneLower.Contains("shelter") || zoneLower.Contains("first aid") ||
                dustOpticalDepthTau > 0.65f)
            {
                step5StormShelter = true;
            }

            telemetryController?.SetChecklistProgress(GetCompletedChecklistCount());
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
                NasaDataLayerMode mode = telemetryController != null ? telemetryController.CurrentLayerMode : NasaDataLayerMode.SafestRouteFocus;
                string layerName = MarsTelemetryController.GetLayerModeDisplayName(mode);
                int doneSteps = GetCompletedChecklistCount();
                string chk = $"{(step1EvaMeda ? "[+]" : "[ ]")}EVA " +
                             $"{(step2OutcropScan ? "[+]" : "[ ]")}Scan " +
                             $"{(step3RoverDrive ? "[+]" : "[ ]")}Rover " +
                             $"{(step4HydroponicsO2 ? "[+]" : "[ ]")}O2 " +
                             $"{(step5StormShelter ? "[+]" : "[ ]")}Storm";

                string ctxSummary = MarsCtxNavSolarEngine.Instance != null
                    ? MarsCtxNavSolarEngine.Instance.GetCompactHudSummary()
                    : "CTX A* Engine Ready";

                txtRoutes.text = $"A* TRAVERSE ({r.safest.distance_m:F0}m) · MISSION {doneSteps}/5\n" +
                                 $"• [H] {layerName} | Offset {liveDistToRouteM:F1}m | Goal {liveDistToGoalM:F0}m\n" +
                                 $"• {ctxSummary}\n" +
                                 $"• {chk} | Tubes {tubes}/22 | MSL {curKm:F1}km | 72 Flt | Fix {dropouts}";
            }
        }

        private void RefreshGeologyHudText()
        {
            if (txtGeology != null)
            {
                if (datasetLoader != null && datasetLoader.GeologyUnits != null && datasetLoader.GeologyUnits.Count > 0)
                {
                    if (!manualGeologyOverride && !string.IsNullOrEmpty(liveGeoSymbol))
                    {
                        for (int i = 0; i < datasetLoader.GeologyUnits.Count; i++)
                        {
                            if (string.Equals(datasetLoader.GeologyUnits[i].symbol, liveGeoSymbol, StringComparison.OrdinalIgnoreCase))
                            {
                                currentGeologyUnitIndex = i;
                                break;
                            }
                        }
                    }

                    int idx = Mathf.Clamp(currentGeologyUnitIndex, 0, datasetLoader.GeologyUnits.Count - 1);
                    var g = datasetLoader.GeologyUnits[idx];
                    string modeTag = manualGeologyOverride ? "Manual [G]" : "Spatial Live";
                    string probeLine = MarsInteractionSystem.Instance != null ? MarsInteractionSystem.Instance.LastOmniProbeOneLiner : "RIMFAX X-Ray [X]";
                    txtGeology.text = $"USGS SIM 3292 [{idx + 1}/{datasetLoader.GeologyUnits.Count}] ({modeTag}) ([G] Next)\n" +
                                      $"Unit: {g.symbol} ({g.name})\n" +
                                      $"Trafficability: {g.trafficability}/5 | H2O Ice: {g.water_ice_potential}\n" +
                                      $"{liveMountSharpNote}\n" +
                                      $"{probeLine}";
                }
                else if (datasetLoader != null && !datasetLoader.IsLoaded)
                {
                    txtGeology.text = "USGS SIM 3292 GEOLOGY\nLoading geological dataset...";
                }
                else
                {
                    Debug.LogWarning("[MarsSceneManager] No geology units loaded from dataset.");
                    txtGeology.text = "USGS SIM 3292 GEOLOGY\n[Warning] No geology units loaded from dataset.";
                }
            }
        }

        public void RefreshLiveExplorationHud()
        {
            hudRefreshTimer = 0f;
            UpdateSpatialNasaAndChecklist();
            RefreshRoutesHudText();
            RefreshGeologyHudText();

            if (telemetryController != null)
            {
                UpdateTelemetryDisplay(telemetryController.CurrentTelemetry);
            }

            if (txtActiveCamera != null)
            {
                if (isPresentationDirectorActive)
                {
                    txtActiveCamera.text = DirectorHudLabel;
                }
                else if (MarsVisualRealismPolish.Instance != null && MarsVisualRealismPolish.Instance.IsExploreModeActive)
                {
                    bool driving = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;
                    string modeStr = driving
                        ? "ROVER DRIVE [E Dismount]"
                        : (MarsAstronautController.Instance != null && MarsAstronautController.Instance.IsFirstPerson
                            ? "EVA VISOR [V 3rd-Person]"
                            : "EVA EXPLORER [WASD / E]");
                    txtActiveCamera.text = $"ACTIVE VIEW: [F] {modeStr}";
                }
            }

            if (txtMuteBtnLabel != null && MarsAudioManager.Instance != null)
            {
                txtMuteBtnLabel.text = MarsAudioManager.Instance.IsMuted ? "Audio: OFF" : "Audio: ON";
            }
            if (txtRoverBtnLabel != null && MarsRoverVehicleController.Instance != null)
            {
                txtRoverBtnLabel.text = MarsRoverVehicleController.Instance.IsDriverSeated ? "Exit [E]" : "Rover [E]";
            }
            if (txtAutoBtnLabel != null && MarsRoverVehicleController.Instance != null)
            {
                txtAutoBtnLabel.text = MarsRoverVehicleController.Instance.IsAutopilotActive ? "Auto: ON" : "Auto [R]";
            }
            UpdateDirectorButtonLabel();
        }

        private void UpdateSurvivalBarsAndObjectiveMarker()
        {
            var surv = MarsSurvivalMissionController.Instance;
            var ctx = MarsCtxNavSolarEngine.Instance;
            if (surv != null)
            {
                float o2 = Mathf.Clamp01(surv.OxygenPercent / 100f);
                float pwr = Mathf.Clamp01(surv.PowerPercent / 100f);
                float rad = Mathf.Clamp01(surv.RadiationTraumaPercent / 100f);
                float rf = ctx != null && ctx.IsLoaded ? Mathf.Clamp01(ctx.ActiveRfLos.signalPercent / 100f) : 0.96f;
                bool rfClear = ctx == null || !ctx.IsLoaded || ctx.ActiveRfLos.clear;

                if (barFillO2 != null) barFillO2.anchorMax = new Vector2(o2, 1f);
                if (barFillPwr != null) barFillPwr.anchorMax = new Vector2(pwr, 1f);
                if (barFillRad != null) barFillRad.anchorMax = new Vector2(rad, 1f);
                if (barFillRf != null) barFillRf.anchorMax = new Vector2(rf, 1f);

                if (imgFillO2 != null) imgFillO2.color = o2 < 0.28f ? new Color(0.95f, 0.24f, 0.24f) : new Color(0.18f, 0.85f, 0.90f);
                if (imgFillRad != null) imgFillRad.color = rad > 0.60f ? new Color(0.95f, 0.22f, 0.22f) : new Color(0.96f, 0.66f, 0.18f);
                if (imgFillRf != null) imgFillRf.color = rfClear ? new Color(0.18f, 0.86f, 0.48f) : new Color(0.95f, 0.30f, 0.26f);

                if (txtVitalO2 != null) txtVitalO2.text = $"O2   {surv.OxygenPercent:F0}%";
                if (txtVitalPwr != null) txtVitalPwr.text = $"PWR  {surv.PowerPercent:F0}%";
                if (txtVitalRad != null) txtVitalRad.text = $"RAD  {surv.RadiationTraumaPercent:F0}%";
                if (txtVitalRf != null) txtVitalRf.text = rfClear ? $"RF   {rf * 100f:F0}% CLR" : $"RF   {rf * 100f:F0}% BLK";

                if (txtMissionObjective != null)
                {
                    string ctxSnippet = ctx != null && ctx.IsLoaded
                        ? $"  |  CTX {ctx.ActivePresetName}: {ctx.SafestRoute.distanceMeters:F0}m ({ctx.SafestRoute.maxSlopeDeg:F1}°) · Ls {ctx.SolarLongitudeLs:F0}°"
                        : "";
                    txtMissionObjective.text = $"OBJ: {surv.ActiveObjectiveLabel} ({surv.ActiveObjectiveDistanceM:F0}m · {surv.ActiveObjectiveBearingDeg:F0}°) · {surv.MissionScore} PTS{ctxSnippet}";
                }

                if (panelAlertToast != null && txtAlertToast != null)
                {
                    bool showAlert = !string.IsNullOrEmpty(surv.ActiveAlertBanner);
                    if (!showAlert && ctx != null && ctx.IsLoaded && !ctx.ActiveRfLos.clear)
                    {
                        showAlert = true;
                        txtAlertToast.text = "RF LINK OCCLUDED BY JEZERO TERRAIN — MOVE TO HIGHER ELEVATION";
                    }
                    else if (showAlert)
                    {
                        txtAlertToast.text = surv.ActiveAlertBanner;
                    }
                    panelAlertToast.SetActive(showAlert);
                }

                // Project 3D objective waypoint onto screen space
                if (objectiveMarkerRect != null && txtObjectiveMarker != null && Camera.main != null)
                {
                    bool explore = MarsVisualRealismPolish.Instance == null || MarsVisualRealismPolish.Instance.IsExploreModeActive;
                    bool inSpace = Camera.main.transform.position.y > 400f;
                    if (!explore || inSpace || isPresentationDirectorActive)
                    {
                        objectiveMarkerRect.gameObject.SetActive(false);
                    }
                    else
                    {
                        Vector3 sp = Camera.main.WorldToViewportPoint(surv.ActiveObjectiveWorldPos);
                        bool inFront = sp.z > 0.5f;
                        objectiveMarkerRect.gameObject.SetActive(inFront && surv.ActiveObjectiveDistanceM > 3.5f);
                        if (inFront)
                        {
                            float cx = Mathf.Clamp(sp.x, 0.06f, 0.94f);
                            float cy = Mathf.Clamp(sp.y, 0.14f, 0.86f);
                            objectiveMarkerRect.anchorMin = new Vector2(cx, cy);
                            objectiveMarkerRect.anchorMax = new Vector2(cx, cy);
                            objectiveMarkerRect.anchoredPosition = Vector2.zero;
                            txtObjectiveMarker.text = $"◈ {surv.ActiveObjectiveDistanceM:F0}m";
                        }
                    }
                }
            }
        }

        private void UpdateTelemetryDisplay(RoverTelemetry t)
        {
            if (txtVisorTitle != null)
            {
                int scanned = MarsInspectableTarget.DiscoveredCount;
                int total = MarsInspectableTarget.TotalCount;
                NasaDataLayerMode mode = telemetryController != null ? telemetryController.CurrentLayerMode : NasaDataLayerMode.SafestRouteFocus;
                string layerName = MarsTelemetryController.GetLayerModeDisplayName(mode);
                int doneSteps = GetCompletedChecklistCount();
                txtVisorTitle.text = $"ORBITX · MARS SENTINEL | SITES {scanned}/{total} | MISSION {doneSteps}/5 | {layerName.ToUpperInvariant()}";
            }

            float elevDeg = celestialController != null ? celestialController.SolarElevationDeg : 45.0f;
            float trans = ComputeBeerLambertTransmission(dustOpticalDepthTau, elevDeg);
            float safeRadius = ComputeEvaSafeRadiusMeters(dustOpticalDepthTau, t.batteryPercent / 100.0f);
            var env = datasetLoader != null ? datasetLoader.EnvironmentData : null;
            float radDay = env != null ? env.radGcrMsvPerDay : 0.67f;
            float pressPa = env != null ? env.pressureMeanPa : 715f;
            float airC = env != null ? env.airTempMeanC : -56f;

            string zoneLabel = MarsAstronautController.Instance != null ? MarsAstronautController.Instance.ActiveZoneName : "Jezero Crater EVA";
            bool driving = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;
            float liveSpeed = driving
                ? Mathf.Abs(MarsRoverVehicleController.Instance.CurrentSpeedMps)
                : (MarsAstronautController.Instance != null ? MarsAstronautController.Instance.CurrentSpeedMps : t.currentSpeedMps);
            float liveSlope = driving
                ? MarsRoverVehicleController.Instance.CurrentSlopeDeg
                : (MarsAstronautController.Instance != null ? MarsAstronautController.Instance.CurrentGroundSlopeDeg : t.currentSlopeDeg);

            if (txtCoordinates != null)
            {
                float solHr = celestialController != null ? celestialController.SolHour : 12.0f;
                txtCoordinates.text = $"SOL {t.sol} | LMST {solHr:F1}h | SUN {elevDeg:F1}° | DUST τ={dustOpticalDepthTau:F2} ({trans * 100f:F0}%) | ZONE: {zoneLabel}";
            }

            if (txtTelemetry != null)
            {
                string roleTitle = driving ? "PRESSURIZED ROVER TELEMETRY" : "EVA ASTRONAUT + BASE TELEMETRY";
                txtTelemetry.text = $"{roleTitle}\n" +
                                    $"• Speed: {liveSpeed:F2} m/s | Hdg: {t.headingDeg:F1}° | Slope: {liveSlope:F1}°\n" +
                                    $"• MEDA Surface: {pressPa:F0} Pa | Air {airC:F0}°C | RAD: {radDay:F2} mSv/day\n" +
                                    $"• Solar Flux: {586.2f * trans:F0} W/m² | Safe PNR: {safeRadius:F0} m | O2: {t.batteryPercent:F0}%\n" +
                                    $"• Traveled: {t.distanceTraveledMeters:F1} m | {t.trafficabilityStatus}";
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

            // Compact Modern Visor Header Strip (Top-Center, sleek glass — >88% open 3D viewport!)
            GameObject headerPanel = CreatePanel("VisorHeaderPanel", canvasGo.transform,
                new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f),
                new Vector2(0, -14), new Vector2(960, 74));
            headerPanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 0.72f);

            txtVisorTitle = CreateText("TxtTitle", headerPanel.transform, "ORBITX · MARS SENTINEL | JEZERO SURVIVAL TWIN",
                14, FontStyle.Bold, new Color(1.0f, 0.85f, 0.52f), TextAnchor.MiddleCenter, defaultFont);
            txtVisorTitle.rectTransform.anchoredPosition = new Vector2(0, 22);

            txtCoordinates = CreateText("TxtCoordinates", headerPanel.transform, "SOL 100 | LMST 12.0h | SUN 45.0° | DUST τ=0.45",
                12, FontStyle.Normal, new Color(0.32f, 0.90f, 0.96f), TextAnchor.MiddleCenter, defaultFont);
            txtCoordinates.rectTransform.anchoredPosition = new Vector2(0, 1);

            txtMissionObjective = CreateText("TxtMissionObjective", headerPanel.transform, "OBJ: Collect Jezero Core Samples (0/3)",
                11, FontStyle.Bold, new Color(0.52f, 0.96f, 0.74f), TextAnchor.MiddleCenter, defaultFont);
            txtMissionObjective.rectTransform.anchoredPosition = new Vector2(0, -21);

            // Dynamic Alert Toast Banner (hidden unless triggered by pickup/RF/storm/MedEvac)
            panelAlertToast = CreatePanel("AlertToastPanel", canvasGo.transform,
                new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f),
                new Vector2(0, -96), new Vector2(640, 34));
            panelAlertToast.GetComponent<Image>().color = new Color(0.55f, 0.18f, 0.08f, 0.88f);
            txtAlertToast = CreateText("TxtAlertToast", panelAlertToast.transform, "",
                12, FontStyle.Bold, new Color(1.0f, 0.95f, 0.82f), TextAnchor.MiddleCenter, defaultFont);
            txtAlertToast.rectTransform.sizeDelta = new Vector2(-12, -4);
            panelAlertToast.SetActive(false);

            // 3D World-Space Objective Screen Marker
            GameObject markerGo = new GameObject("ObjectiveWaypointMarker");
            markerGo.transform.SetParent(canvasGo.transform, false);
            objectiveMarkerRect = markerGo.AddComponent<RectTransform>();
            objectiveMarkerRect.sizeDelta = new Vector2(96f, 26f);
            Image mBg = markerGo.AddComponent<Image>();
            mBg.color = new Color(0.05f, 0.14f, 0.18f, 0.78f);
            mBg.raycastTarget = false;
            txtObjectiveMarker = CreateText("TxtObjMarker", markerGo.transform, "◈ 20m",
                12, FontStyle.Bold, new Color(0.22f, 0.95f, 0.88f), TextAnchor.MiddleCenter, defaultFont);
            txtObjectiveMarker.rectTransform.sizeDelta = new Vector2(-6f, -2f);

            // Sleek Bottom-Left Survival Vitals Gauges Card (always clean & compact!)
            GameObject vitalsPanel = CreatePanel("SurvivalVitalsPanel", canvasGo.transform,
                new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f),
                new Vector2(18, 18), new Vector2(215, 114));
            vitalsPanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 0.76f);
            CreateVitalBarRow(vitalsPanel.transform, defaultFont, 88f, "O2   100%", new Color(0.18f, 0.85f, 0.90f), out txtVitalO2, out barFillO2, out imgFillO2);
            CreateVitalBarRow(vitalsPanel.transform, defaultFont, 62f, "PWR  96%", new Color(0.95f, 0.76f, 0.22f), out txtVitalPwr, out barFillPwr, out _);
            CreateVitalBarRow(vitalsPanel.transform, defaultFont, 36f, "RAD  4%", new Color(0.96f, 0.66f, 0.18f), out txtVitalRad, out barFillRad, out imgFillRad);
            CreateVitalBarRow(vitalsPanel.transform, defaultFont, 10f, "RF   96% CLR", new Color(0.18f, 0.86f, 0.48f), out txtVitalRf, out barFillRf, out imgFillRf);

            // Collapsible Full Telemetry Panels (Hidden by default in Clean 3D-First Mode; press [M] to expand!)
            panelTelemetry = CreatePanel("TelemetryPanel", canvasGo.transform,
                new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f),
                new Vector2(18, -104), new Vector2(365, 180));
            txtTelemetry = CreateText("TxtTelemetry", panelTelemetry.transform,
                "EVA ASTRONAUT + BASE TELEMETRY",
                12, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, defaultFont);

            panelRoutes = CreatePanel("RoutesPanel", canvasGo.transform,
                new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f),
                new Vector2(18, 140), new Vector2(415, 165));
            txtRoutes = CreateText("TxtRoutes", panelRoutes.transform,
                "A* TRAVERSE · MISSION 1/5",
                12, FontStyle.Normal, new Color(0.2f, 1.0f, 0.8f), TextAnchor.UpperLeft, defaultFont);

            panelGeology = CreatePanel("GeologyPanel", canvasGo.transform,
                new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(-18, 76), new Vector2(410, 165));
            txtGeology = CreateText("TxtGeology", panelGeology.transform,
                "USGS SIM 3292 GEOLOGY\nLoading geological dataset...",
                12, FontStyle.Normal, new Color(1.0f, 0.8f, 0.4f), TextAnchor.UpperLeft, defaultFont);

            panelStatus = CreatePanel("StatusAndControlsPanel", canvasGo.transform,
                new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f),
                new Vector2(-18, -104), new Vector2(405, 205));
            txtUgsStatus = CreateText("TxtUgsStatus", panelStatus.transform,
                "UGS IDENTITY: CONNECTING...",
                12, FontStyle.Bold, new Color(0.4f, 0.8f, 1.0f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtUgsStatus.rectTransform, 10f, 38f);

            txtActiveCamera = CreateText("TxtActiveCamera", panelStatus.transform,
                "ACTIVE VIEW: [F] EVA EXPLORER",
                12, FontStyle.Bold, new Color(1.0f, 0.95f, 0.2f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtActiveCamera.rectTransform, 50f, 36f);

            txtControlsLegend = CreateText("TxtControlsLegend", panelStatus.transform,
                "[WASD] Move | [E] Rover/Sample | [R] Autopilot | [X] X-Ray\n[Q] CTX A* Preset | [J] Mars Ls Season | [Y] Full HUD\n[1-5] Cams | [H] Layers | [D/C] Storm | [K] Mute | [N] 30s Demo",
                11, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperLeft, defaultFont);
            SetTopTextSlot(txtControlsLegend.rectTransform, 92f, 100f);

            BuildQuickNavBar(canvasGo.transform, defaultFont);
            SetFullHudVisible(false);
        }

        private void CreateVitalBarRow(Transform parent, Font font, float yFromBottom, string label, Color fillCol,
            out Text lblTxt, out RectTransform fillRt, out Image fillImg)
        {
            GameObject row = new GameObject("VitalRow_" + label.Substring(0, 3));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yFromBottom);
            rt.sizeDelta = new Vector2(-18f, 20f);

            lblTxt = CreateText("Lbl", row.transform, label, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, font);
            lblTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            lblTxt.rectTransform.anchorMax = new Vector2(0.46f, 1f);
            lblTxt.rectTransform.sizeDelta = Vector2.zero;

            GameObject track = new GameObject("Track");
            track.transform.SetParent(row.transform, false);
            RectTransform trt = track.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.48f, 0.22f);
            trt.anchorMax = new Vector2(1f, 0.78f);
            trt.sizeDelta = Vector2.zero;
            Image tImg = track.AddComponent<Image>();
            tImg.color = new Color(1f, 1f, 1f, 0.14f);
            tImg.raycastTarget = false;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(track.transform, false);
            fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            fillImg = fill.AddComponent<Image>();
            fillImg.color = fillCol;
            fillImg.raycastTarget = false;
        }

        private void BuildQuickNavBar(Transform canvasTransform, Font font)
        {
            GameObject navBar = CreatePanel("ORBITX_QuickNavBar", canvasTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(72f, 18f), new Vector2(1140f, 40f));
            navBar.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 0.78f);

            HorizontalLayoutGroup hlg = navBar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(6, 6, 4, 4);
            hlg.spacing = 5f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            AddNavButton(navBar.transform, font, "Explore [F]", new Color(0.12f, 0.45f, 0.65f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsAstronautController.Instance?.TeleportToFacility("Spawn");
            });

            txtRoverBtnLabel = AddNavButton(navBar.transform, font, "Rover [E]", new Color(0.75f, 0.40f, 0.14f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsRoverVehicleController.Instance?.ToggleBoardOrDismount();
                RefreshLiveExplorationHud();
            });

            txtAutoBtnLabel = AddNavButton(navBar.transform, font, "Auto [R]", new Color(0.16f, 0.52f, 0.42f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsRoverVehicleController.Instance?.ToggleAutopilot();
            });

            AddNavButton(navBar.transform, font, "CTX [Q]", new Color(0.14f, 0.44f, 0.56f, 0.90f), () =>
            {
                MarsCtxNavSolarEngine.Instance?.CyclePreset();
                MarsSurvivalMissionController.Instance?.ShowAlert(
                    MarsCtxNavSolarEngine.Instance != null ? MarsCtxNavSolarEngine.Instance.GetCompactHudSummary() : "CTX Nav", 3.5f);
            });

            AddNavButton(navBar.transform, font, "Agri Dome", new Color(0.14f, 0.52f, 0.36f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsAstronautController.Instance?.TeleportToFacility("Agri");
            });

            AddNavButton(navBar.transform, font, "Med Bay", new Color(0.68f, 0.18f, 0.22f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsAstronautController.Instance?.TeleportToFacility("Medical");
            });

            AddNavButton(navBar.transform, font, "NASA HDU", new Color(0.22f, 0.34f, 0.52f, 0.90f), () =>
            {
                MarsVisualRealismPolish.Instance?.EnterExploreMode();
                MarsAstronautController.Instance?.TeleportToFacility("Habitat");
            });

            AddNavButton(navBar.transform, font, "Orbit [5]", new Color(0.18f, 0.25f, 0.42f, 0.90f), () =>
            {
                MarsAudioManager.Instance?.PlayUiClick();
                MarsVisualRealismPolish.Instance?.TriggerViewMode(5);
            });

            AddNavButton(navBar.transform, font, "X-Ray [X]", new Color(0.08f, 0.48f, 0.44f, 0.90f), () =>
            {
                MarsInteractionSystem.Instance?.InspectPointUnderPlayer();
            });

            txtHudToggleBtnLabel = AddNavButton(navBar.transform, font, "HUD: CLEAN [Y]", new Color(0.26f, 0.30f, 0.40f, 0.90f), () =>
            {
                ToggleFullHud();
            });

            txtNasaDemoBtnLabel = AddNavButton(navBar.transform, font, "[N] 30s NASA", new Color(0.09f, 0.42f, 0.62f, 0.90f), () =>
            {
                ToggleNasa30SecDemo();
            });

            txtDemoBtnLabel = AddNavButton(navBar.transform, font, "[P] 1m Demo", new Color(0.32f, 0.22f, 0.54f, 0.90f), () =>
            {
                TogglePresentationDirector();
            });

            txtMuteBtnLabel = AddNavButton(navBar.transform, font, "Audio: ON", new Color(0.20f, 0.24f, 0.30f, 0.90f), () =>
            {
                MarsAudioManager.Instance?.ToggleMute();
                RefreshLiveExplorationHud();
            });
        }

        private Text AddNavButton(Transform parent, Font font, string label, Color bgCol, Action onClick)
        {
            GameObject btnGo = new GameObject("NavBtn_" + label.Replace(" ", ""));
            btnGo.transform.SetParent(parent, false);
            Image bg = btnGo.AddComponent<Image>();
            bg.color = bgCol;
            Button btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            Text txt = CreateText("Label", btnGo.transform, label, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, font);
            txt.rectTransform.sizeDelta = new Vector2(-4f, -2f);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            return txt;
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
