using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Drivable 6-Wheel Pressurized Mars Exploration Rover ("ORBITX Field Traverse Rover").
    /// Supports boarding/dismounting by MarsAstronautController via [E] or HUD toggle,
    /// terrain slope alignment, Ackermann steering & wheel rotation, headlights [L],
    /// and USGS SIM 3292 slope/trafficability telemetry feedback.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class MarsRoverVehicleController : MonoBehaviour
    {
        public static MarsRoverVehicleController Instance { get; private set; }

        [Header("Spawn & Drive Tuning")]
        [SerializeField] private Vector2 spawnXZ = new Vector2(8.5f, -15.0f);
        [SerializeField] private float maxForwardSpeed = 12.5f;
        [SerializeField] private float maxReverseSpeed = 5.5f;
        [SerializeField] private float acceleration = 11.0f;
        [SerializeField] private float dragDecel = 7.5f;
        [SerializeField] private float turnRateDegPerSec = 58.0f;

        [Header("Camera Orbit")]
        [SerializeField] private float chaseDistance = 8.5f;
        [SerializeField] private float chaseYawOffset = 0f;
        [SerializeField] private float chasePitch = 18f;

        public bool IsDriverSeated { get; private set; }
        public bool IsAutopilotActive { get; private set; }
        public float CurrentSpeedMps => currentSpeed;
        public float CurrentSlopeDeg { get; private set; }
        public float TotalDistanceMeters { get; private set; }
        public float WheelSlipPercent { get; private set; }
        public float TractionFactor { get; private set; } = 1.0f;

        private float currentSpeed = 0f;
        private float steerAngle = 0f;
        private float wheelSpinDeg = 0f;
        private bool headlightsOn = true;
        private bool built = false;
        private bool usingNasaGlb = false;

        private Transform suspensionRoot;
        private float suspPitchVel = 0f;
        private float suspRollVel = 0f;
        private float suspPitch = 0f;
        private float suspRoll = 0f;
        private float suspHeave = 0f;
        private float dustEmitTimer = 0f;
        private int autopilotWpIdx = 0;

        private readonly Transform[] wheelVisuals = new Transform[6];
        private readonly Transform[] wheelSteerPivots = new Transform[4]; // Front L/R + Rear L/R
        private Light headLightL;
        private Light headLightR;
        private Transform dismountAnchor;
        private MarsInspectableTarget inspectTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildRoverIfNeeded();
            float y = MarsVisualRealismPolish.SampleTerrainElevation(spawnXZ.x, spawnXZ.y);
            transform.position = new Vector3(spawnXZ.x, y, spawnXZ.y);
            transform.rotation = Quaternion.Euler(0f, -25f, 0f);
            AlignToTerrain(100f);
        }

        public void BuildRoverIfNeeded()
        {
            if (built) return;
            built = true;

            Transform old = transform.Find("Rover_Body_Root");
            if (old != null) Destroy(old.gameObject);

            GameObject root = new GameObject("Rover_Body_Root");
            root.transform.SetParent(transform, false);
            suspensionRoot = root.transform;

            // Try authentic NASA Perseverance GLB (pre-mounted child, Editor AssetDatabase, or cloned from MS_Perseverance_Root)
            Transform glbNode = transform.Find("Rover_NASA_Perseverance_GLB");
#if UNITY_EDITOR
            if (glbNode == null)
            {
                GameObject roverAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Fleet/mars-2020-perseverance-rover.glb");
                if (roverAsset != null)
                {
                    GameObject inst = Instantiate(roverAsset, root.transform);
                    inst.name = "Rover_NASA_Perseverance_GLB";
                    glbNode = inst.transform;
                }
            }
#endif
            if (glbNode == null)
            {
                GameObject scenePersev = GameObject.Find("MS_Perseverance_Root");
                if (scenePersev != null && scenePersev.transform.childCount > 0)
                {
                    GameObject cloned = Instantiate(scenePersev.transform.GetChild(0).gameObject, root.transform);
                    cloned.name = "Rover_NASA_Perseverance_GLB";
                    glbNode = cloned.transform;
                }
            }

            if (glbNode != null && glbNode.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                usingNasaGlb = true;
                glbNode.SetParent(root.transform, false);
                foreach (Collider c in glbNode.GetComponentsInChildren<Collider>(true)) Destroy(c);

                glbNode.localPosition = Vector3.zero;
                glbNode.localRotation = Quaternion.identity;
                glbNode.localScale = Vector3.one;

                Renderer[] rends = glbNode.GetComponentsInChildren<Renderer>(true);
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                float targetLen = 3.4f;
                float maxHoriz = Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z));
                float scale = targetLen / maxHoriz;
                glbNode.localScale = Vector3.one * scale;

                // Ensure longest axis faces +Z forward
                if (b.size.x > b.size.z * 1.15f)
                {
                    glbNode.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }

                Bounds sb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) sb.Encapsulate(rends[i].bounds);
                glbNode.position += new Vector3(
                    root.transform.position.x - sb.center.x,
                    root.transform.position.y - sb.min.y,
                    root.transform.position.z - sb.center.z);

                // Upgrade metallic/smoothness balance so NASA PBR model pops crisply under URP lighting
                foreach (Renderer r in rends)
                {
                    Material[] mats = r.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        if (m == null) continue;
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp(m.GetFloat("_Smoothness"), 0.35f, 0.72f));
                    }
                }

                headLightL = CreateHeadlight(root.transform, new Vector3(-0.72f, 1.08f, 1.65f));
                headLightR = CreateHeadlight(root.transform, new Vector3(0.72f, 1.08f, 1.65f));
            }
            else
            {
                usingNasaGlb = false;
                BuildFallbackRoverMesh(root.transform);
            }

            // Dismount anchor on left crew side (-X)
            GameObject dmGo = new GameObject("Rover_DismountAnchor");
            dmGo.transform.SetParent(transform, false);
            dmGo.transform.localPosition = new Vector3(-2.6f, 0f, 0.2f);
            dismountAnchor = dmGo.transform;

            // Main interaction collider + metadata
            BoxCollider interactCol = gameObject.GetComponent<BoxCollider>();
            if (interactCol == null) interactCol = gameObject.AddComponent<BoxCollider>();
            interactCol.center = new Vector3(0f, 1.25f, 0.1f);
            interactCol.size = new Vector3(2.6f, 2.2f, 3.8f);

            inspectTarget = gameObject.GetComponent<MarsInspectableTarget>() ?? gameObject.AddComponent<MarsInspectableTarget>();
            inspectTarget.Configure(
                id: "vehicle_pressurized_rover",
                title: "ORBITX Mars Exploration Rover (NASA M20 PBR)",
                category: "Crew Surface Mobility · 6x6 Rocker-Bogie",
                subtitle: "Press [E] to Drive / Dismount · [U] A* Safest Autopilot",
                coords: "Jezero Base Motor Pool · 18.4446°N, 77.4509°E",
                geology: "Trafficability calibrated to USGS SIM 3292 & nav-module CTX slope limit (<15° safe / >25° hard cutback).",
                mineralogy: "Onboard Laser Raman + Mastcam-Z stereo suite for mobile Jezero rock & crater reconnaissance.",
                environment: "6x6 Rocker-Bogie Suspension · Solar + MMRTG Aux Power · Headlights [B] · Autopilot [U]",
                summary: "Walk near this rover and press [E] to drive manually (WASD) or press [U] to engage A* Safest Route Autopilot across Jezero Crater.",
                sourceUrl: "https://science.nasa.gov/mission/mars-2020-perseverance/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.BoardRover,
                actionHint: "Board & Drive Rover [E]",
                interactionRadius: 6.5f
            );
        }

        private void BuildFallbackRoverMesh(Transform root)
        {
            Material chassisDark = MakeMat("Rover_Chassis", new Color(0.20f, 0.22f, 0.25f), 0.72f, 0.45f);
            Material cabWhite = MakeMat("Rover_CabWhite", new Color(0.90f, 0.91f, 0.93f), 0.30f, 0.60f);
            Material accentOrange = MakeMat("Rover_OrangeTrim", new Color(0.88f, 0.36f, 0.12f), 0.25f, 0.52f);
            Material wheelTire = MakeMat("Rover_Tire", new Color(0.16f, 0.15f, 0.15f), 0.55f, 0.28f);
            Material wheelHub = MakeMat("Rover_WheelHub", new Color(0.75f, 0.55f, 0.22f), 0.80f, 0.65f);
            Material solarMat = MakeMat("Rover_SolarDeck", new Color(0.08f, 0.15f, 0.34f), 0.75f, 0.82f);
            Material glassMat = MakeGlassMat("Rover_CockpitGlass", new Color(0.28f, 0.55f, 0.78f, 0.42f));
            Material consoleGlow = MakeEmissive("Rover_ConsoleGlow", new Color(0.15f, 0.85f, 1.0f), new Color(0.15f, 0.85f, 1.0f) * 1.4f);

            MakePart("Lower_Chassis", PrimitiveType.Cube, root, new Vector3(0f, 0.76f, 0f), Vector3.zero, new Vector3(1.98f, 0.30f, 4.45f), chassisDark, true);
            MakePart("Crew_Cabin", PrimitiveType.Cube, root, new Vector3(0f, 1.48f, 0.25f), Vector3.zero, new Vector3(2.26f, 1.14f, 2.80f), cabWhite, true);
            MakePart("Cockpit_Glass", PrimitiveType.Cube, root, new Vector3(0f, 1.64f, 1.72f), new Vector3(-28f, 0f, 0f), new Vector3(1.90f, 0.74f, 0.10f), glassMat, false);
            MakePart("Accent_Band_L", PrimitiveType.Cube, root, new Vector3(-1.14f, 1.24f, 0.25f), Vector3.zero, new Vector3(0.05f, 0.18f, 2.74f), accentOrange, false);
            MakePart("Accent_Band_R", PrimitiveType.Cube, root, new Vector3(1.14f, 1.24f, 0.25f), Vector3.zero, new Vector3(0.05f, 0.18f, 2.74f), accentOrange, false);
            MakePart("Roof_SolarPanel", PrimitiveType.Cube, root, new Vector3(0f, 2.16f, -0.10f), Vector3.zero, new Vector3(2.05f, 0.06f, 2.20f), solarMat, false);
            MakePart("Cockpit_Console", PrimitiveType.Cube, root, new Vector3(0f, 1.42f, 1.38f), Vector3.zero, new Vector3(1.2f, 0.22f, 0.22f), consoleGlow, false);

            headLightL = CreateHeadlight(root, new Vector3(-0.78f, 1.05f, 2.25f));
            headLightR = CreateHeadlight(root, new Vector3(0.78f, 1.05f, 2.25f));

            float[] axleZ = { 1.68f, 0.05f, -1.62f };
            float[] sideX = { -1.32f, 1.32f };
            int wheelIdx = 0;
            int steerIdx = 0;

            for (int a = 0; a < 3; a++)
            {
                for (int s = 0; s < 2; s++)
                {
                    float wx = sideX[s];
                    float wz = axleZ[a];
                    GameObject steerPivot = new GameObject($"WheelSteer_{a}_{s}");
                    steerPivot.transform.SetParent(root, false);
                    steerPivot.transform.localPosition = new Vector3(wx, 0.50f, wz);
                    if (a == 0 || a == 2) wheelSteerPivots[steerIdx++] = steerPivot.transform;
                    GameObject wheelSpin = new GameObject($"WheelSpin_{a}_{s}");
                    wheelSpin.transform.SetParent(steerPivot.transform, false);
                    wheelVisuals[wheelIdx++] = wheelSpin.transform;
                    MakePart($"Tire_{a}_{s}", PrimitiveType.Cylinder, wheelSpin.transform, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(1.00f, 0.24f, 1.00f), wheelTire, false);
                    MakePart($"Hub_{a}_{s}", PrimitiveType.Cylinder, wheelSpin.transform, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.54f, 0.27f, 0.54f), wheelHub, false);
                }
            }
        }

        public void ToggleAutopilot()
        {
            if (!IsDriverSeated) BoardDriver();
            IsAutopilotActive = !IsAutopilotActive;
            if (IsAutopilotActive)
            {
                autopilotWpIdx = FindNearestSafestWaypointIndex();
                MarsSurvivalMissionController.Instance?.ShowAlert("ROVER AUTOPILOT ENGAGED · FOLLOWING A* SAFEST ROUTE", 3.0f);
            }
            else
            {
                MarsSurvivalMissionController.Instance?.ShowAlert("ROVER AUTOPILOT DISENGAGED · MANUAL CONTROL", 2.2f);
            }
            MarsAudioManager.Instance?.PlayUiClick();
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        private int FindNearestSafestWaypointIndex()
        {
            var pts = MarsDatasetLoader.Instance?.RoutesData?.safest?.unityPositions;
            if (pts == null || pts.Count == 0) return 0;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(pts[i].x, pts[i].z));
                if (d < bestD) { bestD = d; best = i; }
            }
            return Mathf.Min(best + 1, pts.Count - 1);
        }

        public void ToggleBoardOrDismount()
        {
            if (IsDriverSeated) DismountDriver();
            else BoardDriver();
        }

        public void BoardDriver()
        {
            if (IsDriverSeated) return;
            IsDriverSeated = true;
            chaseYawOffset = 0f;
            if (MarsAstronautController.Instance != null)
            {
                MarsAstronautController.Instance.SetBoardedInRover(true, transform.position);
            }
            if (inspectTarget != null) inspectTarget.MarkDiscovered();
            MarsAudioManager.Instance?.PlayUiClick();
            MarsAudioManager.Instance?.SetInteriorState(true, "Pressurized Rover Cabin");
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        public void DismountDriver()
        {
            if (!IsDriverSeated) return;
            IsDriverSeated = false;
            currentSpeed = 0f;
            MarsAudioManager.Instance?.UpdateRoverAudio(false, 0f);
            MarsAudioManager.Instance?.PlayUiClick();

            Vector3 exitPos = dismountAnchor != null
                ? dismountAnchor.position
                : transform.position - transform.right * 2.8f;

            if (MarsAstronautController.Instance != null)
            {
                MarsAstronautController.Instance.SetBoardedInRover(false, exitPos);
            }
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        private void Update()
        {
            if (!IsDriverSeated)
            {
                MarsAudioManager.Instance?.UpdateRoverAudio(false, 0f);
                return;
            }

            if (MarsVisualRealismPolish.Instance != null && !MarsVisualRealismPolish.Instance.IsExploreModeActive)
            {
                MarsAudioManager.Instance?.UpdateRoverAudio(false, 0f);
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            float throttle = 0f;
            float steerInput = 0f;
            bool braking = false;

            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerInput += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerInput -= 1f;
                braking = kb.spaceKey.isPressed;

                if (kb.bKey.wasPressedThisFrame)
                {
                    headlightsOn = !headlightsOn;
                    if (headLightL != null) headLightL.enabled = headlightsOn;
                    if (headLightR != null) headLightR.enabled = headlightsOn;
                    MarsAudioManager.Instance?.PlayUiClick();
                }

                if (kb.rKey.wasPressedThisFrame)
                {
                    ToggleAutopilot();
                }

                if (kb.eKey.wasPressedThisFrame)
                {
                    IsAutopilotActive = false;
                    DismountDriver();
                    return;
                }
            }

            // Manual input immediately yields autopilot back to driver
            if (Mathf.Abs(throttle) > 0.05f || Mathf.Abs(steerInput) > 0.05f || braking)
            {
                if (IsAutopilotActive)
                {
                    IsAutopilotActive = false;
                    MarsSceneManager.Instance?.RefreshLiveExplorationHud();
                }
            }
            else if (IsAutopilotActive)
            {
                var pts = MarsDatasetLoader.Instance?.RoutesData?.safest?.unityPositions;
                if (pts != null && pts.Count > 1)
                {
                    autopilotWpIdx = Mathf.Clamp(autopilotWpIdx, 0, pts.Count - 1);
                    Vector3 wp = pts[autopilotWpIdx];
                    Vector2 toWp = new Vector2(wp.x - transform.position.x, wp.z - transform.position.z);
                    if (toWp.magnitude < 3.2f)
                    {
                        if (autopilotWpIdx < pts.Count - 1) autopilotWpIdx++;
                        else
                        {
                            IsAutopilotActive = false;
                            MarsSurvivalMissionController.Instance?.ShowAlert("AUTOPILOT ARRIVED AT A* SAFEST TARGET", 3.0f);
                        }
                    }
                    if (IsAutopilotActive)
                    {
                        Vector3 desiredDir = new Vector3(toWp.x, 0f, toWp.y).normalized;
                        Vector3 fwdHoriz = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                        float signedAng = Vector3.SignedAngle(fwdHoriz, desiredDir, Vector3.up);
                        steerInput = Mathf.Clamp(signedAng / 30f, -1f, 1f);
                        throttle = Mathf.Abs(signedAng) > 45f ? 0.35f : 0.62f;
                    }
                }
                else
                {
                    IsAutopilotActive = false;
                }
            }

            if (mouse != null)
            {
                bool locked = MarsAstronautController.Instance != null && MarsAstronautController.Instance.IsCursorLocked;
                if (locked || mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    chaseYawOffset += delta.x * 0.20f;
                    chasePitch = Mathf.Clamp(chasePitch - delta.y * 0.16f, 6f, 55f);
                }
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    chaseDistance = Mathf.Clamp(chaseDistance - scrollY * 0.005f, 4.5f, 20.0f);
                }
            }

            // Realistic Martian gravity (3.72 m/s²) hill-climb drag + regolith slip + hard 25° nav-module slope safety limit
            float slopeGrade = Mathf.Sin(CurrentSlopeDeg * Mathf.Deg2Rad);
            float forwardClimbFactor = Vector3.Dot(transform.forward, Vector3.up);
            float slopeLimitScale = CurrentSlopeDeg > 25.0f ? 0.22f : (CurrentSlopeDeg > 15.0f ? 0.68f : 1.0f);
            // Low-gravity normal force reduces regolith shear grip on loose slopes > 10 deg
            TractionFactor = Mathf.Clamp(1.0f - Mathf.Max(0f, CurrentSlopeDeg - 8f) * 0.024f, 0.34f, 1.0f);
            float prevSpeed = currentSpeed;

            if (braking)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dragDecel * 2.6f * TractionFactor * Time.deltaTime);
            }
            else if (Mathf.Abs(throttle) > 0.01f)
            {
                float targetMax = (throttle > 0f ? maxForwardSpeed : -maxReverseSpeed) * slopeLimitScale;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetMax * Mathf.Abs(throttle), acceleration * TractionFactor * Time.deltaTime);
                // Gravity resistance climbing uphill / gravity assist downhill
                currentSpeed -= forwardClimbFactor * 3.72f * Time.deltaTime * 0.65f;
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, (dragDecel + slopeGrade * 3.2f) * Time.deltaTime);
            }

            // Wheel slip ratio (%) driven by torque vs. low-G regolith traction
            float commandedRatio = Mathf.Abs(throttle);
            WheelSlipPercent = Mathf.Clamp((1f - TractionFactor) * 85f * commandedRatio + Mathf.Abs(steerInput) * Mathf.Clamp01(Mathf.Abs(currentSpeed) / 10f) * 14f, 0f, 72f);
            if (WheelSlipPercent > 22f && Mathf.Abs(currentSpeed) > 1.2f)
            {
                MarsCinematicVfxAndPost.Instance?.EmitDustPuff(transform.position - transform.forward * 1.1f, 3, 1.35f);
            }

            float accelRate = (currentSpeed - prevSpeed) / Mathf.Max(0.005f, Time.deltaTime);
            suspPitchVel += (-accelRate * 0.35f) * Time.deltaTime;
            suspRollVel += (steerInput * currentSpeed * 0.25f) * Time.deltaTime;

            // Counter-steer / turn in proportion to speed (with gentle pivot ability at low speed)
            steerAngle = Mathf.Lerp(steerAngle, steerInput * 28f, Time.deltaTime * 8f);
            float turnSign = currentSpeed >= -0.05f ? 1f : -1f;
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 3.0f) * 0.8f + (Mathf.Abs(throttle) > 0.05f ? 0.2f : 0f);
            transform.Rotate(0f, steerInput * turnRateDegPerSec * turnSign * speedFactor * Time.deltaTime, 0f, Space.Self);

            // Advance along horizontal projection with swept obstacle collision + wall sliding + terrain grounding
            Vector3 fwdFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 deltaPos = fwdFlat * (currentSpeed * Time.deltaTime);
            float moveDist = deltaPos.magnitude;
            if (moveDist > 0.0001f)
            {
                Vector3 castDir = deltaPos / moveDist;
                Vector3 boxCenter = transform.position + Vector3.up * 1.45f;
                Vector3 halfExtents = new Vector3(1.18f, 0.72f, 1.85f);
                RaycastHit[] hits = Physics.BoxCastAll(
                    boxCenter, halfExtents, castDir, transform.rotation, moveDist + 0.35f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                float nearest = float.MaxValue;
                Vector3 hitNormal = Vector3.zero;
                for (int h = 0; h < hits.Length; h++)
                {
                    Collider hc = hits[h].collider;
                    if (hc == null) continue;
                    if (hc.transform.IsChildOf(transform)) continue;
                    if (MarsAstronautController.Instance != null && hc.transform.IsChildOf(MarsAstronautController.Instance.transform)) continue;
                    string hn = hc.gameObject.name;
                    if (hn.Contains("Terrain") || hn.Contains("Jezero_Horizon") || hn.Contains("Foundation_Pad") || hn.Contains("RegolithPad")) continue;
                    // Only collide with genuine vertical obstacles (avoid shallow ground slopes)
                    if (Mathf.Abs(hits[h].normal.y) > 0.65f) continue;
                    if (hits[h].distance < nearest)
                    {
                        nearest = hits[h].distance;
                        hitNormal = new Vector3(hits[h].normal.x, 0f, hits[h].normal.z).normalized;
                    }
                }

                if (nearest < float.MaxValue && hitNormal.sqrMagnitude > 0.01f)
                {
                    float allowed = Mathf.Max(0f, nearest - 0.32f);
                    Vector3 slideRemain = Vector3.ProjectOnPlane(deltaPos - castDir * allowed, hitNormal);
                    deltaPos = castDir * allowed + slideRemain * 0.65f;
                    currentSpeed *= 0.45f;
                }
            }
            transform.position += deltaPos;
            TotalDistanceMeters += deltaPos.magnitude;

            AlignToTerrain(10f);

            // Rotate wheels + Ackermann front/rear counter steer
            wheelSpinDeg += (currentSpeed / 0.50f) * Mathf.Rad2Deg * Time.deltaTime;
            for (int i = 0; i < wheelVisuals.Length; i++)
            {
                if (wheelVisuals[i] != null)
                {
                    wheelVisuals[i].localRotation = Quaternion.Euler(wheelSpinDeg, 0f, 0f);
                }
            }
            if (wheelSteerPivots[0] != null) wheelSteerPivots[0].localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            if (wheelSteerPivots[1] != null) wheelSteerPivots[1].localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            if (wheelSteerPivots[2] != null) wheelSteerPivots[2].localRotation = Quaternion.Euler(0f, -steerAngle * 0.55f, 0f);
            if (wheelSteerPivots[3] != null) wheelSteerPivots[3].localRotation = Quaternion.Euler(0f, -steerAngle * 0.55f, 0f);

            // Sync astronaut proxy transform so zone checks & map follow stay accurate
            if (MarsAstronautController.Instance != null)
            {
                MarsAstronautController.Instance.transform.position = transform.position;
            }

            // Emit pooled rear-wheel Martian regolith dust puffs when rolling
            if (Mathf.Abs(currentSpeed) > 1.2f)
            {
                dustEmitTimer += Time.deltaTime * Mathf.Clamp(Mathf.Abs(currentSpeed) / 3.5f, 0.5f, 3.5f);
                if (dustEmitTimer >= 0.14f)
                {
                    dustEmitTimer = 0f;
                    Vector3 rearL = transform.position - transform.forward * 1.45f - transform.right * 1.1f;
                    Vector3 rearR = transform.position - transform.forward * 1.45f + transform.right * 1.1f;
                    MarsCinematicVfxAndPost.Instance?.EmitDustPuff(rearL, 2, 1.15f);
                    MarsCinematicVfxAndPost.Instance?.EmitDustPuff(rearR, 2, 1.15f);
                }
            }

            // Spring-damper suspension bounce on chassis root (tactile Martian terrain feel)
            if (suspensionRoot != null)
            {
                float dt = Time.deltaTime;
                suspPitchVel += (-suspPitch * 34f - suspPitchVel * 6.5f) * dt;
                suspRollVel += (-suspRoll * 36f - suspRollVel * 6.5f) * dt;
                suspPitch = Mathf.Clamp(suspPitch + suspPitchVel * dt, -5.5f, 5.5f);
                suspRoll = Mathf.Clamp(suspRoll + suspRollVel * dt, -6.5f, 6.5f);
                float rumble = Mathf.Abs(currentSpeed) > 0.5f ? Mathf.Sin(Time.time * 18f) * 0.018f * Mathf.Clamp01(Mathf.Abs(currentSpeed) / 8f) : 0f;
                suspHeave = Mathf.Lerp(suspHeave, rumble, dt * 14f);
                suspensionRoot.localPosition = new Vector3(0f, suspHeave, 0f);
                suspensionRoot.localRotation = Quaternion.Euler(suspPitch, 0f, suspRoll);
            }

            // Telemetry & audio updates
            float heading = (transform.eulerAngles.y % 360f + 360f) % 360f;
            MarsTelemetryController.Instance?.UpdateRoverMotionTelemetry(Mathf.Abs(currentSpeed), heading, TotalDistanceMeters);
            MarsAudioManager.Instance?.UpdateRoverAudio(true, currentSpeed);

            if (CurrentSlopeDeg > 20.0f && Mathf.Abs(currentSpeed) > 1.2f)
            {
                MarsAudioManager.Instance?.PlayHazardBeep();
            }
        }

        private void LateUpdate()
        {
            if (!IsDriverSeated) return;
            if (MarsVisualRealismPolish.Instance != null && !MarsVisualRealismPolish.Instance.IsExploreModeActive) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            float targetYaw = transform.eulerAngles.y + chaseYawOffset;
            Quaternion camRot = Quaternion.Euler(chasePitch, targetYaw, 0f);
            Vector3 focus = transform.position + Vector3.up * 1.95f;
            Vector3 dir = -(camRot * Vector3.forward);
            float dist = chaseDistance;

            // SphereCast prevents chase camera clipping inside Habitat / Agri / Rock obstacles
            if (Physics.SphereCast(focus, 0.35f, dir, out RaycastHit camHit, chaseDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (camHit.collider != null && !camHit.collider.transform.IsChildOf(transform))
                {
                    dist = Mathf.Clamp(camHit.distance - 0.35f, 2.2f, chaseDistance);
                }
            }

            Vector3 desiredPos = focus + dir * dist;
            float groundFloor = MarsVisualRealismPolish.SampleTerrainElevation(desiredPos.x, desiredPos.z) + 0.65f;
            if (desiredPos.y < groundFloor) desiredPos.y = groundFloor;

            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, desiredPos, Time.deltaTime * 11f);
            mainCam.transform.LookAt(focus);
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, 60f + Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxForwardSpeed) * 7f, Time.deltaTime * 6f);
        }

        private void AlignToTerrain(float smoothSpeed)
        {
            Vector3 p = transform.position;
            Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 right = new Vector3(transform.right.x, 0f, transform.right.z).normalized;

            // 6-point Rocker-Bogie wheel terrain sampling (FL, FR, ML, MR, RL, RR)
            float hFL = MarsVisualRealismPolish.SampleTerrainElevation((p + fwd * 1.55f - right * 1.15f).x, (p + fwd * 1.55f - right * 1.15f).z);
            float hFR = MarsVisualRealismPolish.SampleTerrainElevation((p + fwd * 1.55f + right * 1.15f).x, (p + fwd * 1.55f + right * 1.15f).z);
            float hML = MarsVisualRealismPolish.SampleTerrainElevation((p - right * 1.20f).x, (p - right * 1.20f).z);
            float hMR = MarsVisualRealismPolish.SampleTerrainElevation((p + right * 1.20f).x, (p + right * 1.20f).z);
            float hRL = MarsVisualRealismPolish.SampleTerrainElevation((p - fwd * 1.55f - right * 1.15f).x, (p - fwd * 1.55f - right * 1.15f).z);
            float hRR = MarsVisualRealismPolish.SampleTerrainElevation((p - fwd * 1.55f + right * 1.15f).x, (p - fwd * 1.55f + right * 1.15f).z);

            float avgY = (hFL + hFR + hML + hMR + hRL + hRR) / 6f;
            float hF = (hFL + hFR) * 0.5f;
            float hB = (hRL + hRR) * 0.5f;
            float hL = (hFL + hML + hRL) / 3f;
            float hR = (hFR + hMR + hRR) / 3f;

            transform.position = new Vector3(p.x, Mathf.Lerp(p.y, avgY, Mathf.Min(1f, Time.deltaTime * 18f)), p.z);

            Vector3 vFwd = new Vector3(fwd.x * 3.1f, hF - hB, fwd.z * 3.1f).normalized;
            Vector3 vRight = new Vector3(right.x * 2.3f, hR - hL, right.z * 2.3f).normalized;
            Vector3 surfaceNormal = Vector3.Cross(vFwd, vRight).normalized;
            if (surfaceNormal.y < 0f) surfaceNormal = -surfaceNormal;

            CurrentSlopeDeg = Vector3.Angle(surfaceNormal, Vector3.up);
            Quaternion targetRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, surfaceNormal).normalized, surfaceNormal);
            transform.rotation = smoothSpeed >= 50f
                ? targetRot
                : Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
        }

        private static Light CreateHeadlight(Transform parent, Vector3 pos)
        {
            GameObject go = new GameObject("Rover_Headlight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 56f;
            l.range = 42f;
            l.intensity = 28f;
            l.color = new Color(0.96f, 0.98f, 1.0f);
            return l;
        }

        private static GameObject MakePart(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool keepCol)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            if (!keepCol)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static Material MakeMat(string name, Color col, float metallic, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Material MakeEmissive(string name, Color col, Color emission)
        {
            Material m = MakeMat(name, col, 0.2f, 0.7f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            return m;
        }

        private static Material MakeGlassMat(string name, Color col)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1.0f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0.0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0.0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.94f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }
    }
}
