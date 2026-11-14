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
        public float CurrentSpeedMps => currentSpeed;
        public float CurrentSlopeDeg { get; private set; }
        public float TotalDistanceMeters { get; private set; }

        private float currentSpeed = 0f;
        private float steerAngle = 0f;
        private float wheelSpinDeg = 0f;
        private bool headlightsOn = true;
        private bool built = false;

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

            Material chassisDark = MakeMat("Rover_Chassis", new Color(0.20f, 0.22f, 0.25f), 0.72f, 0.45f);
            Material cabWhite = MakeMat("Rover_CabWhite", new Color(0.90f, 0.91f, 0.93f), 0.30f, 0.60f);
            Material accentOrange = MakeMat("Rover_OrangeTrim", new Color(0.88f, 0.36f, 0.12f), 0.25f, 0.52f);
            Material goldFoil = MakeMat("Rover_GoldMLI", new Color(0.86f, 0.65f, 0.15f), 0.88f, 0.75f);
            Material wheelTire = MakeMat("Rover_Tire", new Color(0.16f, 0.15f, 0.15f), 0.55f, 0.28f);
            Material wheelHub = MakeMat("Rover_WheelHub", new Color(0.75f, 0.55f, 0.22f), 0.80f, 0.65f);
            Material solarMat = MakeMat("Rover_SolarDeck", new Color(0.08f, 0.15f, 0.34f), 0.75f, 0.82f);
            Material glassMat = MakeGlassMat("Rover_CockpitGlass", new Color(0.28f, 0.55f, 0.78f, 0.42f));
            Material consoleGlow = MakeEmissive("Rover_ConsoleGlow", new Color(0.15f, 0.85f, 1.0f), new Color(0.15f, 0.85f, 1.0f) * 1.4f);

            // Pressurized Cabin Body + Lower Rocker-Bogie Frame
            MakePart("Lower_Chassis", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.78f, 0f), Vector3.zero, new Vector3(1.95f, 0.32f, 4.35f), chassisDark, true);
            MakePart("Crew_Cabin", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.48f, 0.30f), Vector3.zero, new Vector3(2.25f, 1.15f, 2.75f), cabWhite, true);
            MakePart("Nose_Hood", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.12f, 1.95f), new Vector3(18f, 0f, 0f), new Vector3(2.05f, 0.55f, 0.95f), cabWhite, false);
            MakePart("Cockpit_Glass", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.62f, 1.72f), new Vector3(-28f, 0f, 0f), new Vector3(1.88f, 0.75f, 0.12f), glassMat, false);
            MakePart("Cockpit_Console", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.42f, 1.38f), Vector3.zero, new Vector3(1.2f, 0.22f, 0.22f), consoleGlow, false);

            // Side NASA / ORBITX Orange Accent Bars + Rear Airlock Hatch
            MakePart("Accent_Band_L", PrimitiveType.Cube, root.transform, new Vector3(-1.14f, 1.45f, 0.30f), Vector3.zero, new Vector3(0.05f, 0.22f, 2.65f), accentOrange, false);
            MakePart("Accent_Band_R", PrimitiveType.Cube, root.transform, new Vector3(1.14f, 1.45f, 0.30f), Vector3.zero, new Vector3(0.05f, 0.22f, 2.65f), accentOrange, false);
            MakePart("Rear_AirlockPod", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.48f, -1.42f), new Vector3(90f, 0f, 0f), new Vector3(1.55f, 0.55f, 1.25f), goldFoil, true);

            // Roof Solar Array + Mastcam-Z Sensor Mast + Twin Headlights
            MakePart("Roof_SolarPanel", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.12f, -0.05f), Vector3.zero, new Vector3(2.15f, 0.08f, 2.30f), solarMat, false);
            MakePart("Mast_Post", PrimitiveType.Cylinder, root.transform, new Vector3(0.55f, 2.55f, 1.15f), Vector3.zero, new Vector3(0.10f, 0.45f, 0.10f), cabWhite, false);
            MakePart("Mast_SensorHead", PrimitiveType.Cube, root.transform, new Vector3(0.55f, 3.05f, 1.18f), Vector3.zero, new Vector3(0.46f, 0.22f, 0.28f), cabWhite, false);
            MakePart("Mast_LensL", PrimitiveType.Cylinder, root.transform, new Vector3(0.42f, 3.05f, 1.33f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.04f, 0.08f), consoleGlow, false);
            MakePart("Mast_LensR", PrimitiveType.Cylinder, root.transform, new Vector3(0.68f, 3.05f, 1.33f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.04f, 0.08f), consoleGlow, false);

            // Headlights
            headLightL = CreateHeadlight(root.transform, new Vector3(-0.78f, 1.05f, 2.25f));
            headLightR = CreateHeadlight(root.transform, new Vector3(0.78f, 1.05f, 2.25f));

            // 6 Articulated Wheels (Front L/R, Mid L/R, Rear L/R) on suspension bogies
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

                    // Strut arm from chassis to wheel axle
                    MakePart($"BogieStrut_{a}_{s}", PrimitiveType.Cube, root.transform,
                        new Vector3(wx * 0.72f, 0.62f, wz), new Vector3(0f, 0f, s == 0 ? 22f : -22f), new Vector3(0.55f, 0.10f, 0.14f), chassisDark, false);

                    GameObject steerPivot = new GameObject($"WheelSteer_{a}_{s}");
                    steerPivot.transform.SetParent(root.transform, false);
                    steerPivot.transform.localPosition = new Vector3(wx, 0.50f, wz);

                    if (a == 0 || a == 2)
                    {
                        wheelSteerPivots[steerIdx++] = steerPivot.transform;
                    }

                    GameObject wheelSpin = new GameObject($"WheelSpin_{a}_{s}");
                    wheelSpin.transform.SetParent(steerPivot.transform, false);
                    wheelVisuals[wheelIdx++] = wheelSpin.transform;

                    MakePart($"Tire_{a}_{s}", PrimitiveType.Cylinder, wheelSpin.transform,
                        Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(1.00f, 0.24f, 1.00f), wheelTire, false);
                    MakePart($"Hub_{a}_{s}", PrimitiveType.Cylinder, wheelSpin.transform,
                        Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.54f, 0.27f, 0.54f), wheelHub, false);
                }
            }

            // Dismount anchor on left crew side (-X)
            GameObject dmGo = new GameObject("Rover_DismountAnchor");
            dmGo.transform.SetParent(transform, false);
            dmGo.transform.localPosition = new Vector3(-2.6f, 0f, 0.2f);
            dismountAnchor = dmGo.transform;

            // Main interaction collider + metadata
            BoxCollider interactCol = gameObject.GetComponent<BoxCollider>();
            if (interactCol == null) interactCol = gameObject.AddComponent<BoxCollider>();
            interactCol.center = new Vector3(0f, 1.35f, 0.2f);
            interactCol.size = new Vector3(2.8f, 2.3f, 4.6f);

            inspectTarget = gameObject.GetComponent<MarsInspectableTarget>() ?? gameObject.AddComponent<MarsInspectableTarget>();
            inspectTarget.Configure(
                id: "vehicle_pressurized_rover",
                title: "ORBITX Pressurized Mars Exploration Rover",
                category: "Crew Surface Mobility · Pressurized 6x6",
                subtitle: "Press [E] to Drive / Dismount · Ackermann 6-Wheel Rocker-Bogie",
                coords: "Jezero Base Motor Pool · 18.4446°N, 77.4509°E",
                geology: "Trafficability calibrated to USGS SIM 3292 units & HiRISE 1m slope hazard thresholds (<15 deg safe / >28 deg no-go).",
                mineralogy: "Onboard Laser Raman + Mastcam-Z stereo suite for mobile Jezero rock & crater reconnaissance.",
                environment: "6x6 Electric Hub Drive · Solar + MMRTG Aux Power · Headlights [B] · Range: 45 km/sol",
                summary: "Walk near this pressurized crew rover and press [E] (or click 'Drive Rover [E]' on the HUD) to take full WASD driving control across Jezero Crater.",
                sourceUrl: "https://science.nasa.gov/mission/mars-2020-perseverance/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.BoardRover,
                actionHint: "Board & Drive Rover [E]",
                interactionRadius: 6.5f
            );
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

                if (kb.eKey.wasPressedThisFrame)
                {
                    DismountDriver();
                    return;
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

            if (braking)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dragDecel * 2.5f * Time.deltaTime);
            }
            else if (Mathf.Abs(throttle) > 0.01f)
            {
                float targetMax = throttle > 0f ? maxForwardSpeed : -maxReverseSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetMax * Mathf.Abs(throttle), acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dragDecel * Time.deltaTime);
            }

            // Counter-steer / turn in proportion to speed (with gentle pivot ability at low speed)
            steerAngle = Mathf.Lerp(steerAngle, steerInput * 28f, Time.deltaTime * 8f);
            float turnSign = currentSpeed >= -0.05f ? 1f : -1f;
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 3.0f) * 0.8f + (Mathf.Abs(throttle) > 0.05f ? 0.2f : 0f);
            transform.Rotate(0f, steerInput * turnRateDegPerSec * turnSign * speedFactor * Time.deltaTime, 0f, Space.Self);

            // Advance along horizontal projection + ground on terrain
            Vector3 fwdFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 deltaPos = fwdFlat * (currentSpeed * Time.deltaTime);
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

            // Telemetry & audio updates
            float heading = (transform.eulerAngles.y % 360f + 360f) % 360f;
            MarsTelemetryController.Instance?.UpdateRoverMotionTelemetry(Mathf.Abs(currentSpeed), heading, TotalDistanceMeters);
            MarsAudioManager.Instance?.UpdateRoverAudio(true, currentSpeed);

            if (CurrentSlopeDeg > 18.0f && Mathf.Abs(currentSpeed) > 1.5f)
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
            Vector3 focus = transform.position + Vector3.up * 1.85f;
            Vector3 desiredPos = focus - camRot * Vector3.forward * chaseDistance;

            float groundFloor = MarsVisualRealismPolish.SampleTerrainElevation(desiredPos.x, desiredPos.z) + 0.65f;
            if (desiredPos.y < groundFloor) desiredPos.y = groundFloor;

            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, desiredPos, Time.deltaTime * 10f);
            mainCam.transform.LookAt(focus);
        }

        private void AlignToTerrain(float smoothSpeed)
        {
            Vector3 p = transform.position;
            Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 right = new Vector3(transform.right.x, 0f, transform.right.z).normalized;

            Vector3 pF = p + fwd * 1.6f;
            Vector3 pB = p - fwd * 1.6f;
            Vector3 pL = p - right * 1.3f;
            Vector3 pR = p + right * 1.3f;

            float hC = MarsVisualRealismPolish.SampleTerrainElevation(p.x, p.z);
            float hF = MarsVisualRealismPolish.SampleTerrainElevation(pF.x, pF.z);
            float hB = MarsVisualRealismPolish.SampleTerrainElevation(pB.x, pB.z);
            float hL = MarsVisualRealismPolish.SampleTerrainElevation(pL.x, pL.z);
            float hR = MarsVisualRealismPolish.SampleTerrainElevation(pR.x, pR.z);

            transform.position = new Vector3(p.x, Mathf.Lerp(p.y, hC, Mathf.Min(1f, Time.deltaTime * 18f)), p.z);

            Vector3 vFwd = new Vector3(pF.x - pB.x, hF - hB, pF.z - pB.z).normalized;
            Vector3 vRight = new Vector3(pR.x - pL.x, hR - hL, pR.z - pL.z).normalized;
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
