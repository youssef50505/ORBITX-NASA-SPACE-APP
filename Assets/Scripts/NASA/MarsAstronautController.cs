using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Playable NASA EVA Astronaut Controller on Mars surface (0.38g gravity = 3.72076 m/s^2).
    /// Supports WASD/Arrows locomotion, Shift sprint, Space jump, Third-Person Orbit / First-Person
    /// Helmet Visor camera [V], Cursor Lock toggle [Tab], Headlamp [L], and procedural walking animation.
    /// Exclusively uses UnityEngine.InputSystem (New Input System).
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class MarsAstronautController : MonoBehaviour
    {
        public static MarsAstronautController Instance { get; private set; }

        [Header("Spawn & Physics")]
        [SerializeField] private Vector2 spawnXZ = new Vector2(-3.5f, -16.0f);
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 7.8f;
        [SerializeField] private float jumpSpeed = 4.4f;
        [SerializeField] private float marsGravity = 3.72076f;

        [Header("Camera Orbit")]
        [SerializeField] private float camDistance = 4.6f;
        [SerializeField] private float camYaw = 15.0f;
        [SerializeField] private float camPitch = 16.0f;
        [SerializeField] private bool firstPersonVisor = false;
        [SerializeField] private bool cursorLocked = false;

        public bool IsBoardedInRover { get; private set; }
        public bool IsFirstPerson => firstPersonVisor;
        public bool IsCursorLocked => cursorLocked;
        public bool IsGrounded => charCtrl != null && charCtrl.isGrounded;
        public float CurrentSpeedMps { get; private set; }
        public float CurrentGroundSlopeDeg { get; private set; }
        public string ActiveZoneName { get; private set; } = "Jezero Crater EVA Surface";

        private CharacterController charCtrl;
        private Transform visualRoot;
        private Transform leftArmPivot;
        private Transform rightArmPivot;
        private Transform leftLegPivot;
        private Transform rightLegPivot;
        private Transform headGroup;
        private Light headlamp;
        private bool headlampOn = false;

        private float verticalVelocity = 0f;
        private float walkCycle = 0f;
        private float nextStepAt = 0.5f;
        private float smoothedCamYaw = 15f;
        private float smoothedCamPitch = 16f;
        private bool rigBuilt = false;

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
            BuildAstronautIfNeeded();
            SnapToSurfaceAt(spawnXZ.x, spawnXZ.y);
            smoothedCamYaw = camYaw;
            smoothedCamPitch = camPitch;
        }

        public void BuildAstronautIfNeeded()
        {
            if (rigBuilt) return;
            rigBuilt = true;

            charCtrl = GetComponent<CharacterController>();
            if (charCtrl == null) charCtrl = gameObject.AddComponent<CharacterController>();
            charCtrl.height = 1.88f;
            charCtrl.radius = 0.34f;
            charCtrl.center = new Vector3(0f, 0.94f, 0f);
            charCtrl.stepOffset = 0.55f;
            charCtrl.slopeLimit = 50f;
            charCtrl.skinWidth = 0.04f;

            // Use official NASA Astronaut GLB if mounted in scene (or via AssetDatabase in Editor); fallback cleanly otherwise
            Transform glbSuit = transform.Find("EVA_NASA_Astronaut_GLB");
#if UNITY_EDITOR
            if (glbSuit == null)
            {
                GameObject astroAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Spacecraft/Astronaut.glb");
                if (astroAsset != null)
                {
                    GameObject glbGo = Instantiate(astroAsset, transform);
                    glbGo.name = "EVA_NASA_Astronaut_GLB";
                    glbSuit = glbGo.transform;
                }
            }
#endif
            Transform existingVis = transform.Find("EVA_Suit_Visual");
            if (existingVis != null) Destroy(existingVis.gameObject);

            if (glbSuit != null && glbSuit.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                if (glbSuit.parent != transform) glbSuit.SetParent(transform, false);
                foreach (Collider c in glbSuit.GetComponentsInChildren<Collider>(true)) Destroy(c);

                // Normalize scale cleanly to 1.82m and ground feet at local Y = 0, facing +Z
                glbSuit.localPosition = Vector3.zero;
                glbSuit.localRotation = Quaternion.identity;
                glbSuit.localScale = Vector3.one;
                Renderer[] rends = glbSuit.GetComponentsInChildren<Renderer>(true);
                Bounds gb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) gb.Encapsulate(rends[i].bounds);
                float glbScale = 1.82f / Mathf.Max(0.05f, gb.size.y);
                glbSuit.localScale = Vector3.one * glbScale;
                Bounds sb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) sb.Encapsulate(rends[i].bounds);
                glbSuit.position += new Vector3(
                    transform.position.x - sb.center.x,
                    transform.position.y - sb.min.y,
                    transform.position.z - sb.center.z);

                // Crisp white suit shading lift + slight ambient so helmet & suit never go muddy in dark shadows
                foreach (Renderer r in rends)
                {
                    Material[] mats = r.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        if (m == null) continue;
                        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), 0.22f));
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp(m.GetFloat("_Smoothness"), 0.35f, 0.58f));
                        if (m.HasProperty("_BaseColor") && m.GetTexture("_BaseMap") != null)
                        {
                            Color bc = m.GetColor("_BaseColor");
                            m.SetColor("_BaseColor", new Color(Mathf.Max(bc.r, 0.94f), Mathf.Max(bc.g, 0.94f), Mathf.Max(bc.b, 0.96f), 1f));
                        }
                        if (m.HasProperty("_EmissionColor"))
                        {
                            m.EnableKeyword("_EMISSION");
                            m.SetColor("_EmissionColor", new Color(0.11f, 0.10f, 0.095f, 1f));
                        }
                    }
                }

                // Crisp NASA Meatball & ORBITX emblem decal mounted flush onto the PLSS backpack rear
                Material nasaDecalMat = MarsBaseFacilitiesBuilder.CreateNasaBadgeMaterial();
                if (nasaDecalMat != null)
                {
                    GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    badge.name = "EVA_PLSS_NASABadge";
                    badge.transform.SetParent(glbSuit, false);
                    Collider bc = badge.GetComponent<Collider>();
                    if (bc != null) Destroy(bc);
                    // Astronaut.glb PLSS backpack back surface extends to z ~ -0.335m at 1.82m height
                    float invS = 1f / Mathf.Max(0.001f, glbSuit.localScale.x);
                    badge.transform.localPosition = new Vector3(0f, 1.24f * invS, -0.345f * invS);
                    badge.transform.localRotation = Quaternion.identity;
                    badge.transform.localScale = new Vector3(0.18f * invS, 0.18f * invS, 0.18f * invS);
                    Renderer br = badge.GetComponent<Renderer>();
                    if (br != null) br.sharedMaterial = nasaDecalMat;
                }

                // Helmet Headlamp SpotLight
                GameObject glbLamp = new GameObject("Helmet_Headlamp");
                glbLamp.transform.SetParent(transform, false);
                glbLamp.transform.localPosition = new Vector3(0.14f, 1.72f, 0.20f);
                headlamp = glbLamp.AddComponent<Light>();
                headlamp.type = LightType.Spot;
                headlamp.spotAngle = 52f;
                headlamp.range = 28f;
                headlamp.intensity = 18f;
                headlamp.color = new Color(0.95f, 0.97f, 1.0f);
                headlamp.enabled = headlampOn;

                visualRoot = glbSuit;
                headGroup = null;
                leftArmPivot = null;
                rightArmPivot = null;
                leftLegPivot = null;
                rightLegPivot = null;
                return;
            }

            // Fallback procedural suit only if Astronaut.glb asset is unavailable
            GameObject visGo = new GameObject("EVA_Suit_Visual");
            visGo.transform.SetParent(transform, false);
            visualRoot = visGo.transform;

            Material suitWhite = MakeMat("EVA_SuitWhite", new Color(0.92f, 0.93f, 0.95f), 0.15f, 0.45f);
            Material suitTrimOrange = MakeMat("EVA_NASA_Orange", new Color(0.88f, 0.35f, 0.12f), 0.25f, 0.52f);
            Material suitJointDark = MakeMat("EVA_JointDark", new Color(0.20f, 0.22f, 0.26f), 0.35f, 0.35f);
            Material goldVisor = MakeMat("EVA_GoldVisor", new Color(0.94f, 0.68f, 0.14f), 0.95f, 0.92f);
            Material ledCyan = MakeEmissive("EVA_LED_Cyan", new Color(0.18f, 0.85f, 1.0f), new Color(0.18f, 0.85f, 1.0f) * 2.0f);

            // Torso + NASA/ORBITX Chest Display Pack
            MakePart("Torso", PrimitiveType.Capsule, visualRoot, new Vector3(0f, 1.18f, 0f), Vector3.zero, new Vector3(0.46f, 0.34f, 0.30f), suitWhite);
            MakePart("ChestTelemetry", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.24f, 0.16f), Vector3.zero, new Vector3(0.22f, 0.16f, 0.08f), suitJointDark);
            MakePart("ChestStatusLED", PrimitiveType.Cube, visualRoot, new Vector3(0.05f, 1.26f, 0.205f), Vector3.zero, new Vector3(0.08f, 0.03f, 0.01f), ledCyan);
            MakePart("SuitStripeL", PrimitiveType.Cube, visualRoot, new Vector3(-0.18f, 1.20f, 0f), Vector3.zero, new Vector3(0.06f, 0.32f, 0.26f), suitTrimOrange);
            MakePart("SuitStripeR", PrimitiveType.Cube, visualRoot, new Vector3(0.18f, 1.20f, 0f), Vector3.zero, new Vector3(0.06f, 0.32f, 0.26f), suitTrimOrange);

            // PLSS Portable Life Support Backpack w/ NASA Blue Insignia & Thermal Accent
            MakePart("PLSS_Backpack", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.22f, -0.22f), Vector3.zero, new Vector3(0.38f, 0.50f, 0.20f), suitWhite);
            MakePart("PLSS_O2Gauge", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.40f, -0.325f), Vector3.zero, new Vector3(0.24f, 0.05f, 0.02f), ledCyan);
            Material nasaBlue = MakeMat("EVA_NASABlue", new Color(0.05f, 0.24f, 0.58f), 0.25f, 0.55f);
            MakePart("PLSS_NASADisc", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.18f, -0.324f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.015f, 0.16f), nasaBlue);
            MakePart("PLSS_NASAChevron", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.18f, -0.333f), new Vector3(0f, 0f, 32f), new Vector3(0.15f, 0.025f, 0.01f), suitTrimOrange);
            MakePart("PLSS_Antenna", PrimitiveType.Cylinder, visualRoot, new Vector3(0.15f, 1.62f, -0.26f), Vector3.zero, new Vector3(0.02f, 0.22f, 0.02f), suitJointDark);

            // Head & Helmet w/ Gold Reflective Visor
            GameObject headGo = new GameObject("HeadGroup");
            headGo.transform.SetParent(visualRoot, false);
            headGo.transform.localPosition = new Vector3(0f, 1.63f, 0f);
            headGroup = headGo.transform;

            MakePart("NeckRing", PrimitiveType.Cylinder, headGroup, new Vector3(0f, -0.11f, 0f), Vector3.zero, new Vector3(0.24f, 0.04f, 0.24f), suitTrimOrange);
            MakePart("HelmetShell", PrimitiveType.Sphere, headGroup, Vector3.zero, Vector3.zero, new Vector3(0.34f, 0.36f, 0.36f), suitWhite);
            MakePart("GoldVisor", PrimitiveType.Sphere, headGroup, new Vector3(0f, 0.01f, 0.08f), Vector3.zero, new Vector3(0.28f, 0.25f, 0.26f), goldVisor);

            // Helmet Headlamp SpotLight
            GameObject lampGo = new GameObject("Helmet_Headlamp");
            lampGo.transform.SetParent(headGroup, false);
            lampGo.transform.localPosition = new Vector3(0.14f, 0.14f, 0.16f);
            headlamp = lampGo.AddComponent<Light>();
            headlamp.type = LightType.Spot;
            headlamp.spotAngle = 52f;
            headlamp.range = 28f;
            headlamp.intensity = 18f;
            headlamp.color = new Color(0.95f, 0.97f, 1.0f);
            headlamp.enabled = headlampOn;

            // Articulated Arms
            leftArmPivot = CreateLimb("LeftArm", visualRoot, new Vector3(-0.27f, 1.38f, 0f), new Vector3(0f, -0.20f, 0f), new Vector3(0.12f, 0.22f, 0.12f), suitWhite, suitTrimOrange, false);
            rightArmPivot = CreateLimb("RightArm", visualRoot, new Vector3(0.27f, 1.38f, 0f), new Vector3(0f, -0.20f, 0f), new Vector3(0.12f, 0.22f, 0.12f), suitWhite, suitTrimOrange, false);

            // Articulated Legs & Boots
            leftLegPivot = CreateLimb("LeftLeg", visualRoot, new Vector3(-0.12f, 0.88f, 0f), new Vector3(0f, -0.36f, 0f), new Vector3(0.16f, 0.36f, 0.16f), suitWhite, suitJointDark, true);
            rightLegPivot = CreateLimb("RightLeg", visualRoot, new Vector3(0.12f, 0.88f, 0f), new Vector3(0f, -0.36f, 0f), new Vector3(0.16f, 0.36f, 0.16f), suitWhite, suitJointDark, true);
        }

        private static Transform CreateLimb(string name, Transform parent, Vector3 pivotPos, Vector3 meshOffset, Vector3 meshScale, Material upperMat, Material tipMat, bool isLeg)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotPos;

            MakePart(name + "_Segment", PrimitiveType.Capsule, pivot.transform, meshOffset, Vector3.zero, meshScale, upperMat);
            if (isLeg)
            {
                MakePart(name + "_Boot", PrimitiveType.Cube, pivot.transform, meshOffset + new Vector3(0f, -0.38f, 0.05f), Vector3.zero, new Vector3(0.16f, 0.14f, 0.26f), tipMat);
            }
            else
            {
                MakePart(name + "_Glove", PrimitiveType.Sphere, pivot.transform, meshOffset + new Vector3(0f, -0.22f, 0f), Vector3.zero, new Vector3(0.11f, 0.12f, 0.11f), tipMat);
            }
            return pivot.transform;
        }

        public void SnapToSurfaceAt(float x, float z)
        {
            float y = MarsVisualRealismPolish.SampleTerrainElevation(x, z) + 0.05f;
            bool wasEnabled = charCtrl != null && charCtrl.enabled;
            if (charCtrl != null) charCtrl.enabled = false;
            transform.position = new Vector3(x, y, z);
            verticalVelocity = 0f;
            if (charCtrl != null) charCtrl.enabled = wasEnabled;
        }

        public void SetCameraAngles(float yaw, float pitch)
        {
            camYaw = yaw;
            smoothedCamYaw = yaw;
            camPitch = pitch;
            smoothedCamPitch = pitch;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void TeleportToFacility(string key)
        {
            if (IsBoardedInRover && MarsRoverVehicleController.Instance != null)
            {
                MarsRoverVehicleController.Instance.DismountDriver();
            }

            Vector2 dest = spawnXZ;
            float facingYaw = 15f;
            float targetPitch = 16.0f;
            switch (key)
            {
                case "Agri":
                    dest = MarsBaseFacilitiesBuilder.POS_AGRICULTURE + new Vector2(10.8f, 0f);
                    facingYaw = -90f;
                    break;
                case "Medical":
                    dest = MarsBaseFacilitiesBuilder.POS_MEDICAL_BAY + new Vector2(0f, -6.4f);
                    facingYaw = 0f;
                    break;
                case "Habitat":
                    dest = MarsBaseFacilitiesBuilder.POS_HABITAT_HDU + new Vector2(8.8f, -5.2f);
                    facingYaw = -52f;
                    break;
                case "EDL":
                    dest = MarsBaseFacilitiesBuilder.POS_EDL_AIRCRAFT + new Vector2(-7.5f, 0f);
                    facingYaw = 90f;
                    break;
                case "Rocket":
                    dest = MarsBaseFacilitiesBuilder.POS_ROCKET_PAD + new Vector2(-28.0f, -24.0f);
                    facingYaw = 45f;
                    targetPitch = -5.5f;
                    break;
                case "Rover":
                    if (MarsRoverVehicleController.Instance != null)
                    {
                        Vector3 rp = MarsRoverVehicleController.Instance.transform.position;
                        dest = new Vector2(rp.x - 3.6f, rp.z - 2.0f);
                        facingYaw = 60f;
                    }
                    break;
                default:
                    dest = spawnXZ;
                    facingYaw = 10f;
                    break;
            }

            SnapToSurfaceAt(dest.x, dest.y);
            camYaw = facingYaw;
            smoothedCamYaw = facingYaw;
            camPitch = targetPitch;
            smoothedCamPitch = targetPitch;
            transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
        }

        public void SetBoardedInRover(bool boarded, Vector3 dismountPos)
        {
            IsBoardedInRover = boarded;
            if (charCtrl != null) charCtrl.enabled = !boarded;
            if (visualRoot != null) visualRoot.gameObject.SetActive(!boarded);

            if (!boarded)
            {
                float y = MarsVisualRealismPolish.SampleTerrainElevation(dismountPos.x, dismountPos.z) + 0.05f;
                transform.position = new Vector3(dismountPos.x, y, dismountPos.z);
                verticalVelocity = 0f;
            }
        }

        private void Update()
        {
            if (IsBoardedInRover)
            {
                CurrentSpeedMps = 0f;
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool exploreActive = MarsVisualRealismPolish.Instance == null || MarsVisualRealismPolish.Instance.IsExploreModeActive;
            if (!exploreActive)
            {
                if (cursorLocked) SetCursorLock(false);
                return;
            }

            // Camera View & Headlamp & Cursor Lock hotkeys
            if (kb != null)
            {
                if (kb.vKey.wasPressedThisFrame)
                {
                    firstPersonVisor = !firstPersonVisor;
                    if (headGroup != null) headGroup.gameObject.SetActive(!firstPersonVisor);
                    else if (visualRoot != null) visualRoot.gameObject.SetActive(!firstPersonVisor);
                    MarsAudioManager.Instance?.PlayUiClick();
                }
                if (kb.bKey.wasPressedThisFrame)
                {
                    headlampOn = !headlampOn;
                    if (headlamp != null) headlamp.enabled = headlampOn;
                    MarsAudioManager.Instance?.PlayUiClick();
                }
                if (kb.tabKey.wasPressedThisFrame)
                {
                    SetCursorLock(!cursorLocked);
                }
                if (kb.escapeKey.wasPressedThisFrame && cursorLocked)
                {
                    SetCursorLock(false);
                }
            }

            // Mouse Orbit + Zoom
            if (mouse != null)
            {
                bool looking = cursorLocked || mouse.rightButton.isPressed;
                if (looking)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    camYaw += delta.x * 0.22f;
                    camPitch = Mathf.Clamp(camPitch - delta.y * 0.18f, -40f, 68f);
                }

                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    camDistance = Mathf.Clamp(camDistance - scrollY * 0.004f, 2.2f, 14.0f);
                }
            }

            // Movement Input (WASD + Arrow keys)
            float moveX = 0f;
            float moveZ = 0f;
            bool sprinting = false;
            bool jumpPressed = false;

            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveZ += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveZ -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveX -= 1f;
                sprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jumpPressed = kb.spaceKey.wasPressedThisFrame;
            }

            Vector3 inputDir = new Vector3(moveX, 0f, moveZ);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            Quaternion yawRot = Quaternion.Euler(0f, camYaw, 0f);
            Vector3 desiredHoriz = (yawRot * inputDir) * (sprinting ? sprintSpeed : walkSpeed);
            CurrentSpeedMps = desiredHoriz.magnitude;

            if (charCtrl.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -1.2f;
                if (jumpPressed)
                {
                    verticalVelocity = jumpSpeed;
                }
            }
            verticalVelocity -= marsGravity * Time.deltaTime;

            Vector3 stepMove = (desiredHoriz + Vector3.up * verticalVelocity) * Time.deltaTime;
            charCtrl.Move(stepMove);

            // Terrain grounding failsafe (never fall under Mars terrain mesh)
            float terrainY = MarsVisualRealismPolish.SampleTerrainElevation(transform.position.x, transform.position.z);
            if (transform.position.y < terrainY - 0.15f)
            {
                charCtrl.enabled = false;
                transform.position = new Vector3(transform.position.x, terrainY + 0.02f, transform.position.z);
                charCtrl.enabled = true;
                verticalVelocity = 0f;
            }

            // Calculate terrain slope under astronaut
            float hDx = MarsVisualRealismPolish.SampleTerrainElevation(transform.position.x + 0.5f, transform.position.z)
                      - MarsVisualRealismPolish.SampleTerrainElevation(transform.position.x - 0.5f, transform.position.z);
            float hDz = MarsVisualRealismPolish.SampleTerrainElevation(transform.position.x, transform.position.z + 0.5f)
                      - MarsVisualRealismPolish.SampleTerrainElevation(transform.position.x, transform.position.z - 0.5f);
            CurrentGroundSlopeDeg = Mathf.Atan(Mathf.Sqrt(hDx * hDx + hDz * hDz)) * Mathf.Rad2Deg;

            // Smooth facing direction toward movement (or camera in 1st person)
            if (firstPersonVisor)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, yawRot, Time.deltaTime * 16f);
            }
            else if (desiredHoriz.sqrMagnitude > 0.05f)
            {
                Quaternion targetFacing = Quaternion.LookRotation(desiredHoriz.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetFacing, Time.deltaTime * 10f);
            }

            // Procedural Limb Walking / Jumping Animation + Footsteps
            AnimateLimbsAndFootsteps(CurrentSpeedMps, sprinting);

            // Interior shelter & audio zone sync
            bool inside = false;
            string zName = "Jezero Crater EVA Surface";
            if (MarsBaseFacilitiesBuilder.Instance != null)
            {
                inside = MarsBaseFacilitiesBuilder.Instance.EvaluateInteriorShelter(transform.position, out zName);
            }
            ActiveZoneName = zName;
            MarsAudioManager.Instance?.SetInteriorState(inside, zName);
            MarsAudioManager.Instance?.SetBreathingIntensity(sprinting);
        }

        private void LateUpdate()
        {
            if (IsBoardedInRover) return;
            if (MarsVisualRealismPolish.Instance != null && !MarsVisualRealismPolish.Instance.IsExploreModeActive) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            smoothedCamYaw = Mathf.LerpAngle(smoothedCamYaw, camYaw, Time.deltaTime * 14f);
            smoothedCamPitch = Mathf.Lerp(smoothedCamPitch, camPitch, Time.deltaTime * 14f);

            if (firstPersonVisor)
            {
                Vector3 eyePos = transform.position + new Vector3(0f, 1.66f, 0f) + transform.forward * 0.14f;
                mainCam.transform.position = eyePos;
                mainCam.transform.rotation = Quaternion.Euler(smoothedCamPitch, smoothedCamYaw, 0f);
            }
            else
            {
                Quaternion rot = Quaternion.Euler(smoothedCamPitch, smoothedCamYaw, 0f);
                Vector3 focus = transform.position + new Vector3(0f, 1.48f, 0f) + rot * new Vector3(0.42f, 0f, 0f);
                Vector3 desiredPos = focus - rot * Vector3.forward * camDistance;

                // Keep camera above terrain
                float camFloor = MarsVisualRealismPolish.SampleTerrainElevation(desiredPos.x, desiredPos.z) + 0.55f;
                if (desiredPos.y < camFloor) desiredPos.y = camFloor;

                mainCam.transform.position = desiredPos;
                mainCam.transform.LookAt(focus);
            }
        }

        private void AnimateLimbsAndFootsteps(float speed, bool sprinting)
        {
            if (leftArmPivot == null || rightArmPivot == null || leftLegPivot == null || rightLegPivot == null)
            {
                if (visualRoot != null)
                {
                    if (speed > 0.25f && charCtrl.isGrounded)
                    {
                        float rate = sprinting ? 11.2f : 7.6f;
                        walkCycle += Time.deltaTime * rate;
                        float bob = Mathf.Abs(Mathf.Sin(walkCycle)) * 0.045f;
                        float roll = Mathf.Cos(walkCycle) * 2.2f;
                        float pitch = sprinting ? 6.5f : 3.2f;
                        visualRoot.localPosition = new Vector3(0f, bob, 0f);
                        visualRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
                        if (walkCycle >= nextStepAt)
                        {
                            nextStepAt += Mathf.PI;
                            bool deck = MarsAudioManager.Instance != null && MarsAudioManager.Instance.IsInsideFacility;
                            MarsAudioManager.Instance?.PlayFootstep(deck);
                        }
                    }
                    else
                    {
                        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, Time.deltaTime * 10f);
                        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                    }
                }
                return;
            }

            if (speed > 0.25f && charCtrl.isGrounded)
            {
                float rate = sprinting ? 11.5f : 7.8f;
                walkCycle += Time.deltaTime * rate;
                float swing = Mathf.Sin(walkCycle) * (sprinting ? 40f : 28f);

                leftLegPivot.localRotation = Quaternion.Euler(swing, 0f, 0f);
                rightLegPivot.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                leftArmPivot.localRotation = Quaternion.Euler(-swing * 0.72f, 0f, 4f);
                rightArmPivot.localRotation = Quaternion.Euler(swing * 0.72f, 0f, -4f);

                if (walkCycle >= nextStepAt)
                {
                    nextStepAt += Mathf.PI;
                    bool deck = MarsAudioManager.Instance != null && MarsAudioManager.Instance.IsInsideFacility;
                    MarsAudioManager.Instance?.PlayFootstep(deck);
                }
            }
            else if (!charCtrl.isGrounded)
            {
                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, Quaternion.Euler(-16f, 0f, 0f), Time.deltaTime * 8f);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, Quaternion.Euler(18f, 0f, 0f), Time.deltaTime * 8f);
                leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, Quaternion.Euler(-22f, 0f, 16f), Time.deltaTime * 8f);
                rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, Quaternion.Euler(-22f, 0f, -16f), Time.deltaTime * 8f);
            }
            else
            {
                float idleBreath = Mathf.Sin(Time.time * 1.8f) * 1.6f;
                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, Quaternion.Euler(idleBreath, 0f, 5f), Time.deltaTime * 10f);
                rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, Quaternion.Euler(-idleBreath, 0f, -5f), Time.deltaTime * 10f);
            }
        }

        public void SetCursorLock(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static GameObject MakePart(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
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
    }
}
