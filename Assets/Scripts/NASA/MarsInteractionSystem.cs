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
        [SerializeField] private float raycastMaxDistance = 85.0f;

        public MarsInspectableTarget HoveredTarget { get; private set; }
        public MarsInspectableTarget SelectedTarget { get; private set; }
        public bool IsInspectorOpen => inspectorPanel != null && inspectorPanel.activeSelf;

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
        }

        private void Update()
        {
            UpdateHoverAndProximity();
            HandleInteractionInputs();
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

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi && HoveredTarget != null)
                {
                    ActivateTarget(HoveredTarget, allowRoverBoard: false);
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
            dRt.anchoredPosition = new Vector2(-18f, -34f);
            dRt.sizeDelta = new Vector2(455f, 616f);

            // Category Banner
            inspCategory = CreateText("InspCategory", inspectorPanel.transform, 12, TextAnchor.MiddleLeft, new Color(0.45f, 0.92f, 1f), FontStyle.Bold);
            SetAnchorPos(inspCategory.rectTransform, 18f, -12f, 340f, 20f);

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

            inspTitle = CreateText("InspTitle", inspectorPanel.transform, 18, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            SetAnchorPos(inspTitle.rectTransform, 18f, -36f, 424f, 40f);

            inspSubtitle = CreateText("InspSubtitle", inspectorPanel.transform, 12, TextAnchor.UpperLeft, new Color(0.75f, 0.84f, 0.94f), FontStyle.Italic);
            SetAnchorPos(inspSubtitle.rectTransform, 18f, -76f, 424f, 22f);

            // Real NASA Imagery Preview Frame
            GameObject imgGo = new GameObject("InspImage");
            imgGo.transform.SetParent(inspectorPanel.transform, false);
            inspImage = imgGo.AddComponent<RawImage>();
            inspImage.color = new Color(0.12f, 0.14f, 0.18f);
            SetAnchorPos(inspImage.rectTransform, 18f, -100f, 418f, 138f);

            inspImageCaption = CreateText("InspImageCaption", inspectorPanel.transform, 11, TextAnchor.MiddleLeft, new Color(0.60f, 0.72f, 0.84f), FontStyle.Normal);
            SetAnchorPos(inspImageCaption.rectTransform, 18f, -240f, 418f, 18f);

            // Rich Multi-Section Scientific Text (strict vertical bounds above Reference button)
            inspBody = CreateText("InspBody", inspectorPanel.transform, 11, TextAnchor.UpperLeft, new Color(0.92f, 0.95f, 0.98f), FontStyle.Normal);
            inspBody.supportRichText = true;
            inspBody.verticalOverflow = VerticalWrapMode.Truncate;
            SetAnchorPos(inspBody.rectTransform, 18f, -262f, 418f, 298f);

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

            inspSourceUrlText = CreateText("SourceUrlLabel", inspectorPanel.transform, 11, TextAnchor.MiddleLeft, new Color(0.56f, 0.80f, 0.96f), FontStyle.Normal);
            inspSourceUrlText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform suRt = inspSourceUrlText.GetComponent<RectTransform>();
            suRt.anchorMin = new Vector2(0f, 0f);
            suRt.anchorMax = new Vector2(0f, 0f);
            suRt.pivot = new Vector2(0f, 0f);
            suRt.anchoredPosition = new Vector2(238f, 12f);
            suRt.sizeDelta = new Vector2(204f, 30f);

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
    }
}
