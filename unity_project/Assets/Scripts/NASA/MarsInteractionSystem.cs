using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Handles scientific pointer/reticle raycasting, proximity target scanning,
    /// rover boarding prompt [E], NASA raw image loading from StreamingAssets,
    /// discovery progression tracking, and the interactive Science Inspector Drawer.
    /// </summary>
    public class MarsInteractionSystem : MonoBehaviour
    {
        public static MarsInteractionSystem Instance { get; private set; }

        [Header("Scanning")]
        [SerializeField] private float raycastMaxDistance = 2500.0f;

        public MarsInspectableTarget HoveredTarget { get; private set; }
        public MarsInspectableTarget SelectedTarget { get; private set; }
        public bool IsInspectorOpen => inspectorPanel != null && inspectorPanel.activeSelf;
        public string LastOmniProbeOneLiner { get; private set; } = "RIMFAX X-Ray Ready [Click Ground/Sky or X]";

        private Canvas hudCanvas;
        private Font uiFont;

        private GameObject promptBanner;
        private Text promptText;
        private GameObject reticleDot;

        private GameObject inspectorPanel;
        private Text inspCategory;
        private Text inspTitle;
        private Text inspSubtitle;
        private RawImage inspImage;
        private Text inspImageCaption;
        private Text inspBody;
        private Text inspSourceUrlText;
        private Button inspSourceBtn;
        private string currentSourceUrl;
        private GameObject cachedGeologyPanel;

        private MarsInspectableTarget runtimeOmniProbeTarget;
        private GameObject omniScanRing;
        private float omniScanTimer = 0f;

        private readonly Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
        private Coroutine imageLoadCoroutine;

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
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            BuildInspectorUi();
            SpawnMeteoriteAndCraterTargets();
            EnsureOmniProbeTarget();
        }

        private void Update()
        {
            UpdateHoverAndProximity();
            HandleInteractionInputs();
            UpdateOmniScanRingVisual();
        }

        private void UpdateHoverAndProximity()
        {
            MarsInspectableTarget found = null;
            Camera cam = Camera.main;

            if (cam != null)
            {
                bool locked = MarsAstronautController.Instance != null && MarsAstronautController.Instance.IsCursorLocked;
                Vector2 screenPt = locked || Mouse.current == null
                    ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                    : Mouse.current.position.ReadValue();

                Ray ray = cam.ScreenPointToRay(screenPt);
                if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance))
                {
                    found = hit.collider.GetComponentInParent<MarsInspectableTarget>();
                }
            }

            // Fallback to closest interactable within proximity of Astronaut/Rover
            if (found == null)
            {
                Vector3 playerPos = GetPlayerWorldPosition();
                float bestDist = float.MaxValue;
                foreach (var t in MarsInspectableTarget.AllTargets)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    // Skip rover while already seated inside it
                    if (t.actionType == MarsInspectActionType.BoardRover &&
                        MarsRoverVehicleController.Instance != null &&
                        MarsRoverVehicleController.Instance.IsDriverSeated)
                    {
                        continue;
                    }

                    float d = Vector3.Distance(playerPos, t.transform.position);
                    if (d <= t.interactionRadius && d < bestDist)
                    {
                        bestDist = d;
                        found = t;
                    }
                }
            }

            if (HoveredTarget != found)
            {
                if (HoveredTarget != null) HoveredTarget.SetHighlighted(false);
                HoveredTarget = found;
                if (HoveredTarget != null) HoveredTarget.SetHighlighted(true);
            }

            UpdatePromptVisual(HoveredTarget);
        }

        private void HandleInteractionInputs()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsInspectorOpen)
            {
                CloseInspector();
                return;
            }

            // While driving rover, [E] dismounts via MarsRoverVehicleController; skip here to avoid double-triggering
            bool driving = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;

            if (!driving && kb != null && kb.eKey.wasPressedThisFrame && HoveredTarget != null)
            {
                ActivateTarget(HoveredTarget, allowRoverBoard: true);
                return;
            }

            // [X] always runs an instant Omni-Probe Planetary X-Ray scan right under player or crosshair!
            if (kb != null && kb.xKey.wasPressedThisFrame)
            {
                InspectPointUnderPlayer();
                return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    if (HoveredTarget != null)
                    {
                        ActivateTarget(HoveredTarget, allowRoverBoard: false);
                    }
                    else
                    {
                        RunOmniProbeAtCrosshairOrPointer();
                    }
                }
            }
        }

        public void ActivateTarget(MarsInspectableTarget target, bool allowRoverBoard)
        {
            if (target == null) return;

            if (allowRoverBoard && target.actionType == MarsInspectActionType.BoardRover && MarsRoverVehicleController.Instance != null)
            {
                MarsRoverVehicleController.Instance.BoardDriver();
                OpenInspector(target);
                return;
            }

            OpenInspector(target);
        }

        public void OpenInspector(MarsInspectableTarget target)
        {
            if (target == null) return;
            SelectedTarget = target;
            bool isNew = target.MarkDiscovered();
            MarsAudioManager.Instance?.PlayScienceScan(isNew);

            if (inspectorPanel == null) BuildInspectorUi();
            if (inspectorPanel == null) return;

            inspectorPanel.SetActive(true);
            if (cachedGeologyPanel == null) cachedGeologyPanel = GameObject.Find("GeologyPanel");
            if (cachedGeologyPanel != null) cachedGeologyPanel.SetActive(false);
            string badge = isNew ? "NEW DISCOVERY LOGGED" : "NASA SCIENCE LOGGED";
            inspCategory.text = $"{target.category.ToUpperInvariant()}  ·  [{badge}]";
            inspTitle.text = target.title;
            inspSubtitle.text = target.subtitle;

            inspBody.text =
                $"<color=#4CC9F0><b>IAU2000 COORDINATES & SITE</b></color>\n{target.coordinatesText}\n\n" +
                $"<color=#FFB703><b>GEOLOGICAL STRATIGRAPHY</b></color>\n{target.geologyText}\n\n" +
                $"<color=#72EFDD><b>MINERALOGY & GEOCHEMISTRY</b></color>\n{target.mineralogyText}\n\n" +
                $"<color=#90E0EF><b>ENVIRONMENT / TELEMETRY</b></color>\n{target.environmentText}\n\n" +
                $"<color=#E2E8F0><b>SCIENTIFIC FINDINGS</b></color>\n{target.summaryText}";

            currentSourceUrl = target.sourceUrl;
            inspSourceUrlText.text = $"Source: {target.sourceUrl}";
            inspImageCaption.text = $"NASA Image: {target.imageFile}";

            LoadTargetImage(target.imageFile);
        }

        public void CloseInspector()
        {
            if (inspectorPanel != null) inspectorPanel.SetActive(false);
            if (cachedGeologyPanel != null) cachedGeologyPanel.SetActive(true);
            MarsAudioManager.Instance?.PlayUiClick();
        }

        public void InspectNextTarget()
        {
            var list = MarsInspectableTarget.AllTargets;
            if (list == null || list.Count == 0) return;

            int idx = 0;
            if (SelectedTarget != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == SelectedTarget)
                    {
                        idx = (i + 1) % list.Count;
                        break;
                    }
                }
            }
            for (int step = 0; step < list.Count; step++)
            {
                int candidate = (idx + step) % list.Count;
                if (list[candidate] != null && list[candidate].gameObject.activeInHierarchy)
                {
                    OpenInspector(list[candidate]);
                    return;
                }
            }
        }

        private void LoadTargetImage(string imageFile)
        {
            if (inspImage == null) return;
            if (string.IsNullOrEmpty(imageFile)) imageFile = "perseverance_sample_raw.jpg";

            if (imageCache.TryGetValue(imageFile, out Texture2D cached) && cached != null)
            {
                inspImage.texture = cached;
                inspImage.color = Color.white;
                return;
            }

            if (imageLoadCoroutine != null) StopCoroutine(imageLoadCoroutine);
            imageLoadCoroutine = StartCoroutine(LoadImageRoutine(imageFile));
        }

        private IEnumerator LoadImageRoutine(string imageFile)
        {
            string fullPath = MarsDatasetLoader.Instance != null
                ? MarsDatasetLoader.Instance.GetOutcropImagePath(imageFile)
                : Path.Combine(Application.streamingAssetsPath, "NASA_Data", "imagery", imageFile);

            if (string.IsNullOrEmpty(fullPath)) yield break;

            if (fullPath.Contains("://") || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(fullPath))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(req);
                        imageCache[imageFile] = tex;
                        inspImage.texture = tex;
                        inspImage.color = Color.white;
                    }
                }
            }
            else if (File.Exists(fullPath))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    imageCache[imageFile] = tex;
                    inspImage.texture = tex;
                    inspImage.color = Color.white;
                }
            }
        }

        private Vector3 GetPlayerWorldPosition()
        {
            if (MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated)
                return MarsRoverVehicleController.Instance.transform.position;
            if (MarsAstronautController.Instance != null)
                return MarsAstronautController.Instance.transform.position;
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        private void UpdatePromptVisual(MarsInspectableTarget target)
        {
            if (promptBanner == null || promptText == null) return;

            bool exploreActive = MarsVisualRealismPolish.Instance == null || MarsVisualRealismPolish.Instance.IsExploreModeActive;
            if (reticleDot != null) reticleDot.SetActive(exploreActive);

            if (!exploreActive || target == null)
            {
                promptBanner.SetActive(false);
                return;
            }

            promptBanner.SetActive(true);
            string scannedTag = target.IsDiscovered ? " [SCANNED]" : " [NEW]";
            if (target.actionType == MarsInspectActionType.BoardRover)
            {
                promptText.text = $"<b>[E]</b> Board & Drive Rover  |  <b>[Click]</b> Inspect Telemetry";
            }
            else
            {
                promptText.text = $"<b>[Click / E]</b> {target.actionHint}: <color=#72EFDD>{target.title}</color>{scannedTag}";
            }
        }

        private void BuildInspectorUi()
        {
            GameObject canvasGo = GameObject.Find("MarsSentinel_InspectorCanvas");
            if (canvasGo != null) Destroy(canvasGo);

            canvasGo = new GameObject("MarsSentinel_InspectorCanvas");
            hudCanvas = canvasGo.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 60;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Center Crosshair Reticle
            reticleDot = new GameObject("CenterReticle");
            reticleDot.transform.SetParent(canvasGo.transform, false);
            Image rImg = reticleDot.AddComponent<Image>();
            rImg.color = new Color(0.45f, 0.92f, 1.0f, 0.72f);
            rImg.raycastTarget = false;
            RectTransform rRt = reticleDot.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0.5f, 0.5f);
            rRt.anchorMax = new Vector2(0.5f, 0.5f);
            rRt.sizeDelta = new Vector2(6f, 6f);

            // Hover / Proximity Prompt Banner at bottom-center above footer
            promptBanner = new GameObject("InteractionPromptBanner");
            promptBanner.transform.SetParent(canvasGo.transform, false);
            Image pBg = promptBanner.AddComponent<Image>();
            pBg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
            pBg.raycastTarget = false;
            RectTransform pRt = promptBanner.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0f);
            pRt.anchorMax = new Vector2(0.5f, 0f);
            pRt.pivot = new Vector2(0.5f, 0f);
            pRt.anchoredPosition = new Vector2(0f, 96f);
            pRt.sizeDelta = new Vector2(680f, 44f);

            promptText = CreateText("PromptText", promptBanner.transform, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            RectTransform ptRt = promptText.GetComponent<RectTransform>();
            ptRt.anchorMin = Vector2.zero;
            ptRt.anchorMax = Vector2.one;
            ptRt.offsetMin = new Vector2(12f, 4f);
            ptRt.offsetMax = new Vector2(-12f, -4f);
            promptText.raycastTarget = false;
            promptBanner.SetActive(false);

            // Right-Side Scientific Inspector Drawer (appears cleanly only when inspecting a target)
            inspectorPanel = new GameObject("ScienceInspectorDrawer");
            inspectorPanel.transform.SetParent(canvasGo.transform, false);
            Image dBg = inspectorPanel.AddComponent<Image>();
            dBg.color = new Color(0.04f, 0.06f, 0.12f, 0.94f);
            RectTransform dRt = inspectorPanel.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(1f, 0.5f);
            dRt.anchorMax = new Vector2(1f, 0.5f);
            dRt.pivot = new Vector2(1f, 0.5f);
            dRt.anchoredPosition = new Vector2(-18f, -52f);
            dRt.sizeDelta = new Vector2(455f, 576f);

            // Category Banner
            inspCategory = CreateText("InspCategory", inspectorPanel.transform, 11, TextAnchor.MiddleLeft, new Color(0.45f, 0.92f, 1f), FontStyle.Bold);
            SetAnchorPos(inspCategory.rectTransform, 18f, -12f, 220f, 22f);

            // Close Button
            Button closeBtn = CreateButton(inspectorPanel.transform, "CloseBtn", "X", new Color(0.65f, 0.18f, 0.18f, 0.9f), CloseInspector);
            RectTransform cRt = closeBtn.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-14f, -10f);
            cRt.sizeDelta = new Vector2(34f, 26f);

            // Next Target Button
            Button nextBtn = CreateButton(inspectorPanel.transform, "NextBtn", "Next >", new Color(0.16f, 0.35f, 0.55f, 0.92f), InspectNextTarget);
            RectTransform nRt = nextBtn.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(1f, 1f);
            nRt.anchorMax = new Vector2(1f, 1f);
            nRt.pivot = new Vector2(1f, 1f);
            nRt.anchoredPosition = new Vector2(-54f, -10f);
            nRt.sizeDelta = new Vector2(72f, 26f);

            // Omni-Probe Foot/Ground X-Ray Button
            Button probeBtn = CreateButton(inspectorPanel.transform, "ProbeFootBtn", "Probe [X]", new Color(0.08f, 0.48f, 0.44f, 0.92f), InspectPointUnderPlayer);
            RectTransform pfRt = probeBtn.GetComponent<RectTransform>();
            pfRt.anchorMin = new Vector2(1f, 1f);
            pfRt.anchorMax = new Vector2(1f, 1f);
            pfRt.pivot = new Vector2(1f, 1f);
            pfRt.anchoredPosition = new Vector2(-132f, -10f);
            pfRt.sizeDelta = new Vector2(78f, 26f);

            inspTitle = CreateText("InspTitle", inspectorPanel.transform, 18, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            SetAnchorPos(inspTitle.rectTransform, 18f, -36f, 424f, 40f);

            inspSubtitle = CreateText("InspSubtitle", inspectorPanel.transform, 12, TextAnchor.UpperLeft, new Color(0.75f, 0.84f, 0.94f), FontStyle.Italic);
            SetAnchorPos(inspSubtitle.rectTransform, 18f, -76f, 424f, 22f);

            // Real NASA Imagery Preview Frame
            GameObject imgGo = new GameObject("InspImage");
            imgGo.transform.SetParent(inspectorPanel.transform, false);
            inspImage = imgGo.AddComponent<RawImage>();
            inspImage.color = new Color(0.12f, 0.14f, 0.18f);
            SetAnchorPos(inspImage.rectTransform, 18f, -100f, 418f, 132f);

            inspImageCaption = CreateText("InspImageCaption", inspectorPanel.transform, 11, TextAnchor.MiddleLeft, new Color(0.60f, 0.72f, 0.84f), FontStyle.Normal);
            SetAnchorPos(inspImageCaption.rectTransform, 18f, -234f, 418f, 18f);

            // Rich Multi-Section Scientific Text (strict vertical bounds above Reference button)
            inspBody = CreateText("InspBody", inspectorPanel.transform, 11, TextAnchor.UpperLeft, new Color(0.92f, 0.95f, 0.98f), FontStyle.Normal);
            inspBody.supportRichText = true;
            inspBody.verticalOverflow = VerticalWrapMode.Truncate;
            SetAnchorPos(inspBody.rectTransform, 18f, -254f, 418f, 268f);

            // Citation Link Bar
            inspSourceBtn = CreateButton(inspectorPanel.transform, "SourceUrlBtn", "Open Official NASA Reference", new Color(0.10f, 0.42f, 0.62f, 0.92f), () =>
            {
                if (!string.IsNullOrEmpty(currentSourceUrl) && currentSourceUrl.StartsWith("https://"))
                {
                    Application.OpenURL(currentSourceUrl);
                }
            });
            RectTransform sRt = inspSourceBtn.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(0f, 0f);
            sRt.pivot = new Vector2(0f, 0f);
            sRt.anchoredPosition = new Vector2(18f, 12f);
            sRt.sizeDelta = new Vector2(212f, 30f);

            inspSourceUrlText = CreateText("SourceUrlLabel", inspectorPanel.transform, 10, TextAnchor.MiddleLeft, new Color(0.56f, 0.80f, 0.96f), FontStyle.Normal);
            inspSourceUrlText.horizontalOverflow = HorizontalWrapMode.Wrap;
            inspSourceUrlText.verticalOverflow = VerticalWrapMode.Overflow;
            inspSourceUrlText.lineSpacing = 0.95f;
            RectTransform suRt = inspSourceUrlText.GetComponent<RectTransform>();
            suRt.anchorMin = new Vector2(0f, 0f);
            suRt.anchorMax = new Vector2(0f, 0f);
            suRt.pivot = new Vector2(0f, 0f);
            suRt.anchoredPosition = new Vector2(236f, 8f);
            suRt.sizeDelta = new Vector2(208f, 38f);

            inspectorPanel.SetActive(false);
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor align, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = uiFont;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button CreateButton(Transform parent, string name, string label, Color bgCol, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image bg = go.AddComponent<Image>();
            bg.color = bgCol;
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            Text txt = CreateText("Label", go.transform, 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            txt.text = label;
            return btn;
        }

        private static void SetAnchorPos(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, yFromTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        #region Omni-Probe Universal Planetary X-Ray & Meteorites
        private void EnsureOmniProbeTarget()
        {
            if (runtimeOmniProbeTarget == null)
            {
                GameObject go = new GameObject("MS_OmniProbe_RuntimeTarget");
                go.transform.SetParent(transform, false);
                runtimeOmniProbeTarget = go.AddComponent<MarsInspectableTarget>();
                runtimeOmniProbeTarget.Configure(
                    "omni_probe_surface",
                    "Jezero Surface X-Ray",
                    "Omni-Probe RIMFAX / PIXL",
                    "Live Coordinate & Subsurface Radar Profile",
                    "18.4447°N, 77.4508°E · -2569.9 m MOLA",
                    "USGS SIM 3292 Nh_lf",
                    "Olivine, Fe-Mg Smectite, Ca-Sulfate",
                    "MEDA 715 Pa · -56°C · RAD 0.67 mSv/day",
                    "Press [X] or click any surface terrain, rock, meteorite, Sun, or moon to inspect live NASA telemetry.",
                    "https://science.nasa.gov/mission/mars-2020-perseverance/science-instruments/rimfax/",
                    "perseverance_sample_raw.jpg");
            }

            if (omniScanRing == null)
            {
                omniScanRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                omniScanRing.name = "MS_OmniScanRing";
                Destroy(omniScanRing.GetComponent<Collider>());
                omniScanRing.transform.localScale = new Vector3(3.2f, 0.04f, 3.2f);
                Renderer r = omniScanRing.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                    Material m = new Material(s);
                    m.color = new Color(0.18f, 0.92f, 0.88f, 0.72f);
                    r.material = m;
                }
                omniScanRing.SetActive(false);
            }
        }

        private void UpdateOmniScanRingVisual()
        {
            if (omniScanRing == null || !omniScanRing.activeSelf) return;
            omniScanTimer += Time.deltaTime;
            if (omniScanTimer > 6.5f)
            {
                omniScanRing.SetActive(false);
                return;
            }
            float pulse = 1.0f + 0.22f * Mathf.Sin(omniScanTimer * 6.0f);
            omniScanRing.transform.localScale = new Vector3(2.8f * pulse, 0.04f, 2.8f * pulse);
        }

        private void SpawnMeteoriteAndCraterTargets()
        {
            GameObject root = GameObject.Find("Jezero_Meteorites_Root");
            if (root != null) return;
            root = new GameObject("Jezero_Meteorites_Root");

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            CreateMeteoriteTarget(
                root.transform,
                litShader,
                "meteorite_cacao",
                "\"Cacao\" Iron-Nickel Meteorite",
                "Iron Meteorite · Perseverance Sol 691",
                "Jezero Floor · Octavia-to-Delta Traverse",
                new Vector3(-24.0f, 0f, -18.0f),
                new Vector3(1.55f, 0.92f, 1.25f),
                new Color(0.24f, 0.24f, 0.28f),
                0.78f,
                "18.4580°N, 77.4190°E · -2564.2 m MOLA",
                "Exogenic Magmatic Iron-Nickel Bolide resting on Noachian Maaz lava cap",
                "Kamacite (91.2% Fe), Taenite (8.1% Ni), Co/Ir siderophiles · Widmanstatten wind-pitted cavities",
                "Albedo 0.14 · High Thermal Inertia 680 TIU · Zero aqueous corrosion since Hesperian emplacement",
                "Discovered by NASA Perseverance on Sol 691 (~30 cm metallic iron-nickel meteorite). Regmaglypts prove minimal chemical weathering over ~3 Ga.",
                "https://mars.nasa.gov/resources/27319/",
                "perseverance_outcrops_raw.jpg");

            CreateMeteoriteTarget(
                root.transform,
                litShader,
                "meteorite_lebanon",
                "\"Lebanon\" Heavy Iron Bolide",
                "IAB Iron Meteorite · Gale/Jezero Class",
                "Aerodynamically Ablated Metallic Specimen",
                new Vector3(34.0f, 0f, -22.0f),
                new Vector3(1.90f, 1.15f, 1.45f),
                new Color(0.20f, 0.21f, 0.25f),
                0.85f,
                "18.4310°N, 77.4790°E · -2567.8 m MOLA",
                "Dense Fe-Ni bolide with angular aeolian ventifact pitting across windward facet",
                "Fe 89.4 wt%, Ni 9.4 wt%, P 0.5%, Troilite & Schreibersite inclusions (ChemCam/PIXL)",
                "Thermal Inertia 720 TIU · Acoustic velocity 5,100 m/s · High radar reflectivity (>0 dB)",
                "Representative of 2-meter Martian iron-nickel meteorites resilient enough to survive thin CO2 entry (~600 Pa) without catastrophic airburst.",
                "https://science.nasa.gov/mars/",
                "perseverance_outcrops_raw.jpg");

            CreateMeteoriteTarget(
                root.transform,
                litShader,
                "crater_belva_ejecta",
                "Belva Impact Crater & Ejecta Blanket",
                "Fresh Complex Impact Structure · 0.9 km",
                "45m Exposed Deltaic Foreset Strata (Sol 789)",
                new Vector3(-46.0f, 0f, 44.0f),
                new Vector3(4.2f, 0.55f, 4.2f),
                new Color(0.46f, 0.28f, 0.20f),
                0.15f,
                "18.4910°N, 77.3860°E · -2518.4 m MOLA (Rim)",
                "0.9 km young impact excavating deep steeply dipping (~25°) fluvial-deltaic foreset sandstone beds",
                "Shocked Plagioclase (maskelynite), Olivine-Carbonate megablocks, Fe/Mg Smectite ejecta breccia",
                "Rim Slope 22.8° (Hazard >15° limit) · RIMFAX fracture radar depth >12 m · High dust trap risk in bowl",
                "Perseverance Mastcam-Z stereo mosaic of Belva Crater revealed deep delta cross-bedding excavated naturally by hypervelocity asteroid impact.",
                "https://mars.nasa.gov/mars2020/mission/science/landing-site/",
                "perseverance_delta_raw.jpg");
        }

        private static void CreateMeteoriteTarget(
            Transform parent, Shader litShader, string id, string title, string category, string subtitle,
            Vector3 xzPos, Vector3 scale, Color baseColor, float metallic,
            string coords, string geo, string mineral, string env, string summary, string url, string img)
        {
            float y = MarsVisualRealismPolish.SampleTerrainElevation(xzPos.x, xzPos.z);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MS_Target_" + id;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(xzPos.x, y + scale.y * 0.35f, xzPos.z);
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(14f, xzPos.x * 11f, -9f);

            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(litShader);
                mat.color = baseColor;
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", metallic > 0.5f ? 0.55f : 0.18f);
                rend.material = mat;
            }

            var target = go.AddComponent<MarsInspectableTarget>();
            target.Configure(id, title, category, subtitle, coords, geo, mineral, env, summary, url, img, MarsInspectActionType.InspectOnly, "Inspect Meteorite/Crater", 10.0f);
        }

        public void RunOmniProbeAtCrosshairOrPointer()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                InspectPointUnderPlayer();
                return;
            }

            bool locked = MarsAstronautController.Instance != null && MarsAstronautController.Instance.IsCursorLocked;
            Vector2 screenPt = locked || Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();

            Ray ray = cam.ScreenPointToRay(screenPt);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance))
            {
                MarsInspectableTarget existing = hit.collider.GetComponentInParent<MarsInspectableTarget>();
                if (existing != null)
                {
                    OpenInspector(existing);
                    return;
                }
                InspectTerrainSurfacePoint(hit.point, hit.normal, hit.collider.gameObject.name);
                return;
            }

            if (ray.direction.y > 0.04f)
            {
                InspectCelestialRay(ray);
                return;
            }

            InspectPointUnderPlayer();
        }

        public void InspectPointUnderPlayer()
        {
            Vector3 p = GetPlayerWorldPosition();
            Ray downRay = new Ray(p + Vector3.up * 5.0f, Vector3.down);
            Vector3 normal = Vector3.up;
            string hitObj = "Jezero Regolith Floor";
            if (Physics.Raycast(downRay, out RaycastHit hit, 50.0f))
            {
                p = hit.point;
                normal = hit.normal;
                hitObj = hit.collider.gameObject.name;
            }
            else
            {
                p.y = MarsVisualRealismPolish.SampleTerrainElevation(p.x, p.z);
            }
            InspectTerrainSurfacePoint(p, normal, hitObj);
        }

        private void InspectTerrainSurfacePoint(Vector3 worldPt, Vector3 surfaceNormal, string objectName)
        {
            EnsureOmniProbeTarget();

            if (omniScanRing != null)
            {
                omniScanRing.transform.position = worldPt + Vector3.up * 0.08f;
                omniScanRing.SetActive(true);
                omniScanTimer = 0f;
            }

            double latDeg = 18.4447 + worldPt.z * 0.00018;
            double lonDeg = 77.4508 + worldPt.x * 0.00019;
            float elevM = -2569.9f + worldPt.y;
            float slopeDeg = Vector3.Angle(surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal.normalized : Vector3.up, Vector3.up);

            string nearestOutcrop = "Jezero Regolith";
            float outcropDist = 0f;
            string geoSymbol = "HNu_df";
            string geoName = "Noachian-Hesperian Jezero Delta";
            int traffic = 4;
            string mslAnalog = "MSL Gale Analog · Mount Sharp";

            if (MarsTelemetryController.Instance != null)
            {
                MarsTelemetryController.Instance.GetLiveNavigationMetrics(
                    worldPt, out nearestOutcrop, out outcropDist, out geoSymbol, out geoName, out traffic, out mslAnalog, out _, out _, out _);
            }

            float tiu = Mathf.Clamp(220f + Mathf.Abs(Mathf.Sin(worldPt.x * 0.09f + worldPt.z * 0.07f)) * 410f, 180f, 650f);
            float smectitePct = Mathf.Clamp(12f + Mathf.Cos(worldPt.x * 0.06f) * 8f, 4f, 24f);
            float basaltPct = 100f - smectitePct - 3.2f;
            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float flux = 586.2f * MarsSceneManager.ComputeBeerLambertTransmission(tau, 45f);
            string iceIndex = tiu > 380f ? "Moderate Brine/Ice Horizon (8-14m)" : "Low (Dessicated Aeolian Mantle)";
            string hazard = slopeDeg > 15.0f ? $"CAUTION SLOPE {slopeDeg:F1}° (>15° limit)" : $"TRAFFICABLE SLOPE {slopeDeg:F1}° ({traffic}/5)";

            LastOmniProbeOneLiner = $"X-RAY [{geoSymbol}] {latDeg:F4}°N {lonDeg:F4}°E · {elevM:F1}m · Slope {slopeDeg:F1}° · TI {tiu:F0} TIU";

            runtimeOmniProbeTarget.Configure(
                "omni_probe_surface",
                $"Omni-Probe: {objectName}",
                "RIMFAX X-Ray + PIXL Scan",
                $"{geoSymbol} ({geoName}) · Near {nearestOutcrop} ({outcropDist:F0}m)",
                $"{latDeg:F5}°N, {lonDeg:F5}°E · MOLA {elevM:F1} m · Slope {slopeDeg:F1}° ({hazard})",
                $"RIMFAX Subsurface Radar (0–15m): [0–1.8m Aeolian Perchlorate ClO4- 0.42%] -> [1.8–6.5m Deltaic Clay Sandstone] -> [6.5–15m Basalt Bedrock · {iceIndex}]",
                $"PIXL/SHERLOC Est: SiO2 ~44.8%, FeO ~18.6%, MgO ~11.4%, SO3 ~4.1% · {smectitePct:F1}% Phyllosilicate / {basaltPct:F1}% Mafic ({mslAnalog})",
                $"TI {tiu:F0} TIU · Beer-Lambert Sun Flux {flux:F0} W/m² (τ={tau:F2}) · MEDA 715 Pa · RAD 0.67 mSv/day",
                $"Every square meter on Mars is inspectable! Coordinates mapped onto NASA HiRISE 4m DTM (hash ed5e2c...354dd) & USGS SIM 3292 stratigraphy.",
                "https://science.nasa.gov/mission/mars-2020-perseverance/science-instruments/rimfax/",
                "perseverance_sample_raw.jpg");

            OpenInspector(runtimeOmniProbeTarget);
        }

        private void InspectCelestialRay(Ray ray)
        {
            EnsureOmniProbeTarget();

            Vector3 dir = ray.direction.normalized;
            Vector3 sunDir = RenderSettings.sun != null
                ? -RenderSettings.sun.transform.forward.normalized
                : new Vector3(0.45f, 0.55f, 0.65f).normalized;

            float bestDot = Vector3.Dot(dir, sunDir);
            string bodyKey = "sun";

            GameObject phobosGo = GameObject.Find("Phobos") ?? GameObject.Find("MS_Phobos");
            if (phobosGo != null)
            {
                float d = Vector3.Dot(dir, (phobosGo.transform.position - ray.origin).normalized);
                if (d > bestDot) { bestDot = d; bodyKey = "phobos"; }
            }

            GameObject deimosGo = GameObject.Find("Deimos") ?? GameObject.Find("MS_Deimos");
            if (deimosGo != null)
            {
                float d = Vector3.Dot(dir, (deimosGo.transform.position - ray.origin).normalized);
                if (d > bestDot) { bestDot = d; bodyKey = "deimos"; }
            }

            // If ray isn't close to Sun/Moons (< ~30 deg), cycle between Sun -> Phobos -> Deimos -> Earth/DSN
            if (bestDot < 0.85f)
            {
                string prev = runtimeOmniProbeTarget != null ? runtimeOmniProbeTarget.title : "";
                if (prev.Contains("Sun")) bodyKey = "phobos";
                else if (prev.Contains("Phobos")) bodyKey = "deimos";
                else if (prev.Contains("Deimos")) bodyKey = "earth";
                else bodyKey = "sun";
            }

            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float flux = 586.2f * MarsSceneManager.ComputeBeerLambertTransmission(tau, 45f);

            if (bodyKey == "phobos")
            {
                LastOmniProbeOneLiner = "CELESTIAL X-RAY: PHOBOS · 9,376 km · Orbit 7h 39m (West->East)";
                runtimeOmniProbeTarget.Configure(
                    "omni_celestial_phobos",
                    "Phobos (Inner Martian Moon)",
                    "Celestial Body · Carbonaceous D-Type",
                    "Semi-Major Axis 9,376 km · Stickney Crater (9 km)",
                    "Period: 7h 39m 12s (3.97x Mars sidereal rate) · Inclination 1.08°",
                    "Captive rubble-pile asteroid morphology · 27 x 22 x 18 km triaxial ellipsoid · Tidal orbit decay -1.8 cm/yr",
                    "Albedo 0.071 (dark phyllosilicate/carbonaceous chondrite regolith)",
                    "Mars-Sky angular diameter ~0.14° (~1/3 Earth Full Moon) · Rises in the WEST, sets in the EAST",
                    "NASA MRO HiRISE & Viking spectroscopy confirm Phobos as a prime stepping-stone propellant/ISRU waypoint.",
                    "https://science.nasa.gov/mars/moons/phobos/",
                    "mars_globe.jpg");
            }
            else if (bodyKey == "deimos")
            {
                LastOmniProbeOneLiner = "CELESTIAL X-RAY: DEIMOS · 23,463 km · Orbit 30h 18m";
                runtimeOmniProbeTarget.Configure(
                    "omni_celestial_deimos",
                    "Deimos (Outer Martian Moon)",
                    "Celestial Body · Low-Albedos Regolith",
                    "Semi-Major Axis 23,463 km · Swift & Voltaire Craters",
                    "Period: 30h 18m (0.81x Mars sidereal rate) · Inclination 1.79°",
                    "15 x 12.2 x 10.4 km smooth ejecta-mantled body; craters muted by ~100m regolith blanket",
                    "Basaltic/chondritic dust blanket (Albedo 0.068, Escape velocity 5.6 m/s)",
                    "Rises in the East and lingers above the Jezero horizon for ~2.7 sols before setting",
                    "Low gravity makes Deimos ideal for aerobraking relay telecommunications before permanent surface bases.",
                    "https://science.nasa.gov/mars/moons/deimos/",
                    "mars_globe.jpg");
            }
            else if (bodyKey == "earth")
            {
                LastOmniProbeOneLiner = "CELESTIAL X-RAY: EARTH / DSN · 1.84 AU · OWLT 15m 18s";
                runtimeOmniProbeTarget.Configure(
                    "omni_celestial_earth",
                    "Earth & NASA Deep Space Network",
                    "Interplanetary Comms Link · Pale Blue Dot",
                    "Distance ~1.84 AU (275 Million km) · Goldstone / Madrid / Canberra",
                    "One-Way Light Time: 15m 18s · Round-Trip Delay: 30m 36s",
                    "Direct-to-Earth (DTE) X-Band & Ka-Band optical/RF trunk relayed via MRO & TGO orbiters",
                    "Autonomous crew survival systems REQUIRED — Earth real-time teleoperation impossible due to lightspeed latency",
                    "Link Budget: 2.0 Mbps optical relay via Mars Relay Network · DONKI Solar Conjunction blackout monitoring active",
                    "Because Earth lies 15+ light-minutes away, astronauts rely on ORBITX local autonomous routing, medical shelter & agriculture twins.",
                    "https://eyes.nasa.gov/apps/dsn-now/dsn.html",
                    "mars_globe.jpg");
            }
            else
            {
                LastOmniProbeOneLiner = $"CELESTIAL X-RAY: SOL (SUN) · 1.52 AU · Flux {flux:F0} W/m²";
                runtimeOmniProbeTarget.Configure(
                    "omni_celestial_sun",
                    "Sol (The Sun from Mars)",
                    "Heliocentric G2V Star · 1.524 AU",
                    "Beer-Lambert Attenuated Surface Flux & DONKI Space Weather",
                    "Heliocentric Distance: 1.524 AU · Angular Size on Mars: 0.35° (66% Earth)",
                    "Forward Mie scattering of Fe2O3 dust produces a cool bluish halo around sunset/sunrise on butterscotch skies",
                    "Peak TOA Irradiance 586.2 W/m² vs Earth 1,361 W/m² (43% solar energy)",
                    $"Live Surface Flux: {flux:F0} W/m² at Dust τ={tau:F2} · DONKI SEP Alert & RAD 0.67 mSv/day GCR Monitor",
                    "Controls Martian photobiology (requiring 450/660nm LED supplementation in Agri Dome) and triggers dust-storm solar power derating.",
                    "https://science.nasa.gov/sun/",
                    "perseverance_delta_raw.jpg");
            }

            OpenInspector(runtimeOmniProbeTarget);
        }
        #endregion
    }
}
