using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarsSentinel.NASA
{
    public enum MarsGameState
    {
        Title = 0,
        Playing = 1,
        Paused = 2,
        Ended = 3
    }

    /// <summary>
    /// Vision-class frosted-glass 4-state Game Shell (Title -> Playing -> Paused -> Ended)
    /// following Unity craft-game-floors game-shell standards:
    /// - Live 3D orbiting Jezero Crater backdrop on Title with pulsing prompt
    /// - 0.3s unscaled-time palette fade transition between states
    /// - [Esc] Pause Menu with Time.timeScale=0, -6 dB audio ducking, Master/Ambience/SFX sliders,
    ///   EN/AR language switch, and URP Render Graph X-Ray Scanner toggle
    /// - Mission Complete Debrief banner displaying live run telemetry + persistent Personal Best record.
    /// </summary>
    [DefaultExecutionOrder(450)]
    public class MarsGameShellUI : MonoBehaviour
    {
        public static MarsGameShellUI Instance { get; private set; }

        public MarsGameState CurrentState { get; private set; } = MarsGameState.Title;
        public float MissionElapsedSec { get; private set; } = 0f;
        public event Action<MarsGameState> OnGameStateChanged;

        private Canvas shellCanvas;
        private Font uiFont;

        private GameObject titlePanel;
        private Text titleHeading;
        private Text titleSub;
        private Text titlePrompt;
        private Text btnLaunchLabel;
        private Text btnExploreLabel;
        private Text btnTitleLangLabel;

        private GameObject pausePanel;
        private Text pauseHeading;
        private Text btnResumeLabel;
        private Text btnRestartLabel;
        private Text btnXRayLabel;
        private Text btnPauseLangLabel;
        private Text btnQuitTitleLabel;
        private Text lblMaster;
        private Text lblAmbience;
        private Text lblSfx;

        private GameObject endedPanel;
        private Text endedHeading;
        private Text endedSub;
        private Text endedStatsBody;
        private Text endedBestBody;

        private Image fadeOverlay;
        private float fadeAlpha = 0f;
        private float fadePhase = 0f; // 0=idle, >0 fading
        private float titleOrbitAngle = 0f;

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

            BuildShellCanvas();

            if (MarsLocalizationManager.Instance != null)
            {
                MarsLocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            // Start cleanly on Title screen in interactive builds; stay immediately controllable
            ApplyStateImmediate(MarsGameState.Title);
            RefreshLocalizedText();
        }

        private void OnDestroy()
        {
            if (MarsLocalizationManager.Instance != null)
            {
                MarsLocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            Time.timeScale = 1f;
        }

        private void HandleLanguageChanged(MarsLanguage _)
        {
            RefreshLocalizedText();
        }

        public void SetState(MarsGameState nextState)
        {
            if (CurrentState == nextState) return;
            ApplyStateImmediate(nextState);
            fadeAlpha = 0.65f;
            fadePhase = 1f;
        }

        public void TriggerMissionEnded(int score, float cumulativeRadMsv, int samplesCollected)
        {
            if (MarsMissionPersistence.Instance != null)
            {
                MarsMissionPersistence.Instance.RecordCompletedMission(score, MissionElapsedSec, cumulativeRadMsv, samplesCollected);
            }
            SetState(MarsGameState.Ended);
            if (endedStatsBody != null)
            {
                endedStatsBody.text =
                    $"Score: {score} pts   |   Time: {MissionElapsedSec:F1} s   |   Samples: {samplesCollected}/3   |   Rad: {cumulativeRadMsv:F2} mSv";
            }
            if (endedBestBody != null && MarsMissionPersistence.Instance != null)
            {
                endedBestBody.text = MarsMissionPersistence.Instance.GetCompactSummary();
            }
        }

        public void RestartMission()
        {
            MissionElapsedSec = 0f;
            if (MarsSurvivalMissionController.Instance != null)
            {
                MarsSurvivalMissionController.Instance.ResetMission();
            }
            SetState(MarsGameState.Playing);
        }

        private void ApplyStateImmediate(MarsGameState state)
        {
            CurrentState = state;

            if (titlePanel != null) titlePanel.SetActive(state == MarsGameState.Title);
            if (pausePanel != null) pausePanel.SetActive(state == MarsGameState.Paused);
            if (endedPanel != null) endedPanel.SetActive(state == MarsGameState.Ended);

            bool paused = state == MarsGameState.Paused;
            Time.timeScale = paused ? 0f : 1f;

            if (MarsAudioManager.Instance != null)
            {
                MarsAudioManager.Instance.SetPauseDucking(paused);
            }

            if (state == MarsGameState.Paused || state == MarsGameState.Title || state == MarsGameState.Ended)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            OnGameStateChanged?.Invoke(state);
        }

        private void Update()
        {
            // 0.3s unscaled-time transition curtain
            if (fadePhase > 0f && fadeOverlay != null)
            {
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, Time.unscaledDeltaTime / 0.30f);
                fadeOverlay.color = new Color(0.03f, 0.04f, 0.07f, fadeAlpha);
                if (fadeAlpha <= 0.001f) fadePhase = 0f;
            }

            if (CurrentState == MarsGameState.Playing)
            {
                MissionElapsedSec += Time.deltaTime;
            }
            else if (CurrentState == MarsGameState.Title)
            {
                // Pulsing interactive prompt (0.5 <-> 1.0 over 1.6s unscaled time)
                // NOTE: Never hijack Camera.main transform here so Cinemachine / MarsAstronautController stay rock-solid!
                if (titlePrompt != null)
                {
                    float a = 0.50f + 0.50f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI / 0.8f));
                    Color c = titlePrompt.color;
                    c.a = a;
                    titlePrompt.color = c;
                }
            }

            HandleShellHotkeys();
        }

        private void HandleShellHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (CurrentState == MarsGameState.Title)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                    kb.fKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame ||
                    kb.nKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
                {
                    SetState(MarsGameState.Playing);
                    MarsVisualRealismPolish.Instance?.EnterExploreMode();
                    MarsStoryDirector.Instance?.BeginCampaign();
                }
            }
            else if (CurrentState == MarsGameState.Playing)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    SetState(MarsGameState.Paused);
                }
            }
            else if (CurrentState == MarsGameState.Paused)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    SetState(MarsGameState.Playing);
                }
            }
            else if (CurrentState == MarsGameState.Ended)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    RestartMission();
                }
            }
        }

        private void RefreshLocalizedText()
        {
            if (titleHeading != null) titleHeading.text = MarsLocalizationManager.Get("title_main");
            if (titleSub != null) titleSub.text = MarsLocalizationManager.Get("title_sub");
            if (titlePrompt != null) titlePrompt.text = MarsLocalizationManager.Get("press_any");
            if (btnLaunchLabel != null) btnLaunchLabel.text = MarsLocalizationManager.Get("btn_launch");
            if (btnExploreLabel != null) btnExploreLabel.text = MarsLocalizationManager.Get("btn_explore");
            if (btnTitleLangLabel != null) btnTitleLangLabel.text = MarsLocalizationManager.Get("btn_lang");

            if (pauseHeading != null) pauseHeading.text = MarsLocalizationManager.Get("pause_title");
            if (btnResumeLabel != null) btnResumeLabel.text = MarsLocalizationManager.Get("btn_resume");
            if (btnRestartLabel != null) btnRestartLabel.text = MarsLocalizationManager.Get("btn_restart");
            if (btnXRayLabel != null) btnXRayLabel.text = MarsLocalizationManager.Get("btn_xray");
            if (btnPauseLangLabel != null) btnPauseLangLabel.text = MarsLocalizationManager.Get("btn_lang");
            if (btnQuitTitleLabel != null) btnQuitTitleLabel.text = MarsLocalizationManager.Get("btn_title");
            if (lblMaster != null) lblMaster.text = MarsLocalizationManager.Get("lbl_master");
            if (lblAmbience != null) lblAmbience.text = MarsLocalizationManager.Get("lbl_ambience");
            if (lblSfx != null) lblSfx.text = MarsLocalizationManager.Get("lbl_sfx");

            if (endedHeading != null) endedHeading.text = MarsLocalizationManager.Get("ended_title");
            if (endedSub != null) endedSub.text = MarsLocalizationManager.Get("ended_sub");
        }

        private void BuildShellCanvas()
        {
            GameObject existing = GameObject.Find("MarsGameShellCanvas");
            if (existing != null) Destroy(existing);

            GameObject go = new GameObject("MarsGameShellCanvas");
            shellCanvas = go.AddComponent<Canvas>();
            shellCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            shellCanvas.sortingOrder = 500;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            BuildTitlePanel(go.transform);
            BuildPausePanel(go.transform);
            BuildEndedPanel(go.transform);

            // Transition fade curtain (topmost, non-raycast)
            GameObject fadeGo = new GameObject("ShellFadeCurtain");
            fadeGo.transform.SetParent(go.transform, false);
            var fRt = fadeGo.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fadeOverlay = fadeGo.AddComponent<Image>();
            fadeOverlay.color = new Color(0.03f, 0.04f, 0.07f, 0f);
            fadeOverlay.raycastTarget = false;
        }

        private void BuildTitlePanel(Transform root)
        {
            // Light, crisp atmospheric scrim (0.14 alpha) so sunlit Jezero Crater base shines through cleanly
            titlePanel = CreateFullScreenScrim(root, "TitleScreenPanel", new Color(0.03f, 0.04f, 0.07f, 0.14f));

            GameObject card = CreateGlassCard(titlePanel.transform, "TitleHeroCard", new Vector2(840f, 380f), new Vector2(0f, 30f));

            titleHeading = CreateText(card.transform, "TitleHeading", "ORBITX · MARS SENTINEL", 52, FontStyle.Bold,
                new Color(1.0f, 0.56f, 0.28f), new Vector2(0f, 110f), new Vector2(800f, 70f));
            titleSub = CreateText(card.transform, "TitleSub", "SOL 1042: ECHOES OF JEZERO — 5-CHAPTER SURVIVAL STORY", 19, FontStyle.Normal,
                new Color(0.88f, 0.92f, 0.98f), new Vector2(0f, 54f), new Vector2(800f, 34f));

            btnLaunchLabel = CreateGlassButton(card.transform, "BtnLaunch", "LAUNCH STORY MISSION",
                new Vector2(-150f, -20f), new Vector2(280f, 52f), new Color(0.92f, 0.36f, 0.16f, 0.94f),
                () =>
                {
                    SetState(MarsGameState.Playing);
                    MarsVisualRealismPolish.Instance?.EnterExploreMode();
                    MarsStoryDirector.Instance?.BeginCampaign();
                });

            btnExploreLabel = CreateGlassButton(card.transform, "BtnExplore", "FREE EXPLORE JEZERO",
                new Vector2(150f, -20f), new Vector2(280f, 52f), new Color(0.14f, 0.22f, 0.34f, 0.90f),
                () =>
                {
                    SetState(MarsGameState.Playing);
                    MarsVisualRealismPolish.Instance?.EnterExploreMode();
                });

            btnTitleLangLabel = CreateGlassButton(card.transform, "BtnTitleLang", "LANGUAGE: EN / عربي",
                new Vector2(0f, -92f), new Vector2(260f, 42f), new Color(0.10f, 0.16f, 0.24f, 0.86f),
                () => MarsLocalizationManager.Instance?.ToggleLanguage());

            titlePrompt = CreateText(card.transform, "TitlePrompt", "PRESS [ENTER] OR CLICK TO DEPLOY TO JEZERO CRATER", 16, FontStyle.Bold,
                new Color(0.75f, 0.88f, 0.98f, 0.85f), new Vector2(0f, -158f), new Vector2(820f, 34f));
        }

        private void BuildPausePanel(Transform root)
        {
            pausePanel = CreateFullScreenScrim(root, "PauseScreenPanel", new Color(0.025f, 0.035f, 0.06f, 0.72f));
            GameObject card = CreateGlassCard(pausePanel.transform, "PauseCard", new Vector2(620f, 540f), Vector2.zero);

            pauseHeading = CreateText(card.transform, "PauseHeading", "MISSION PAUSED", 34, FontStyle.Bold,
                new Color(1.0f, 0.56f, 0.28f), new Vector2(0f, 215f), new Vector2(560f, 50f));

            btnResumeLabel = CreateGlassButton(card.transform, "BtnResume", "RESUME EVA",
                new Vector2(0f, 145f), new Vector2(360f, 46f), new Color(0.92f, 0.36f, 0.16f, 0.92f),
                () => SetState(MarsGameState.Playing));

            btnRestartLabel = CreateGlassButton(card.transform, "BtnRestart", "RESTART MISSION",
                new Vector2(0f, 88f), new Vector2(360f, 42f), new Color(0.14f, 0.22f, 0.34f, 0.90f),
                RestartMission);

            btnXRayLabel = CreateGlassButton(card.transform, "BtnXRay", "TOGGLE X-RAY SCANNER [X]",
                new Vector2(0f, 34f), new Vector2(360f, 42f), new Color(0.08f, 0.34f, 0.42f, 0.90f),
                () =>
                {
                    Vector3 origin = MarsAstronautController.Instance != null ? MarsAstronautController.Instance.transform.position : Vector3.zero;
                    MarsXRayScannerRendererFeature.TogglePersistentOverlay(origin);
                });

            lblMaster = CreateSliderRow(card.transform, "MasterRow", "Master Volume", -25f, 0.72f,
                v => MarsAudioManager.Instance?.SetMasterVolume(v));
            lblAmbience = CreateSliderRow(card.transform, "AmbienceRow", "Martian Wind & HVAC", -72f, 0.85f,
                v => MarsAudioManager.Instance?.SetAmbienceGain(v));
            lblSfx = CreateSliderRow(card.transform, "SfxRow", "Rover & Scan SFX", -119f, 0.90f,
                v => MarsAudioManager.Instance?.SetSfxGain(v));

            btnPauseLangLabel = CreateGlassButton(card.transform, "BtnPauseLang", "LANGUAGE: EN / عربي",
                new Vector2(-105f, -188f), new Vector2(195f, 42f), new Color(0.10f, 0.18f, 0.26f, 0.90f),
                () => MarsLocalizationManager.Instance?.ToggleLanguage());

            btnQuitTitleLabel = CreateGlassButton(card.transform, "BtnQuitTitle", "RETURN TO TITLE",
                new Vector2(105f, -188f), new Vector2(195f, 42f), new Color(0.28f, 0.14f, 0.16f, 0.90f),
                () => SetState(MarsGameState.Title));
        }

        private void BuildEndedPanel(Transform root)
        {
            endedPanel = CreateFullScreenScrim(root, "EndedScreenPanel", new Color(0.025f, 0.04f, 0.06f, 0.76f));
            GameObject card = CreateGlassCard(endedPanel.transform, "EndedCard", new Vector2(780f, 440f), Vector2.zero);

            endedHeading = CreateText(card.transform, "EndedHeading", "JEZERO CORE CAMPAIGN COMPLETE", 36, FontStyle.Bold,
                new Color(0.22f, 0.94f, 0.68f), new Vector2(0f, 150f), new Vector2(720f, 52f));
            endedSub = CreateText(card.transform, "EndedSub", "ALL 3 SCIENTIFIC CORES SECURED — READY FOR MARS SAMPLE RETURN", 17, FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f), new Vector2(0f, 102f), new Vector2(720f, 34f));

            endedStatsBody = CreateText(card.transform, "EndedStats", "Score: -- | Time: -- | Samples: 3/3 | Rad: --", 20, FontStyle.Bold,
                Color.white, new Vector2(0f, 28f), new Vector2(720f, 48f));
            endedBestBody = CreateText(card.transform, "EndedBest", "Personal Best: --", 17, FontStyle.Normal,
                new Color(1.0f, 0.80f, 0.42f), new Vector2(0f, -32f), new Vector2(720f, 44f));

            CreateGlassButton(card.transform, "BtnPlayAgain", "PLAY AGAIN",
                new Vector2(-130f, -135f), new Vector2(230f, 50f), new Color(0.92f, 0.36f, 0.16f, 0.94f),
                RestartMission);

            CreateGlassButton(card.transform, "BtnEndedTitle", "RETURN TO TITLE",
                new Vector2(130f, -135f), new Vector2(230f, 50f), new Color(0.14f, 0.22f, 0.34f, 0.90f),
                () => SetState(MarsGameState.Title));
        }

        private GameObject CreateFullScreenScrim(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private GameObject CreateGlassCard(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.055f, 0.075f, 0.125f, 0.88f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1.0f, 0.54f, 0.28f, 0.42f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);
            return go;
        }

        private Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Color color, Vector2 pos, Vector2 dims)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
            var txt = go.AddComponent<Text>();
            txt.font = uiFont;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = content;
            return txt;
        }

        private Text CreateGlassButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return CreateText(go.transform, name + "_Txt", label, 16, FontStyle.Bold, Color.white, Vector2.zero, size);
        }

        private Text CreateSliderRow(Transform parent, string name, string label, float yPos, float initial, Action<float> onChanged)
        {
            Text lbl = CreateText(parent, name + "_Lbl", label, 15, FontStyle.Normal,
                new Color(0.84f, 0.90f, 0.96f), new Vector2(-135f, yPos), new Vector2(220f, 30f));
            lbl.alignment = TextAnchor.MiddleLeft;

            GameObject slGo = new GameObject(name + "_Slider");
            slGo.transform.SetParent(parent, false);
            var rt = slGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(210f, 22f);
            rt.anchoredPosition = new Vector2(105f, yPos);

            var bg = slGo.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.22f, 0.30f, 0.90f);

            GameObject fillArea = new GameObject("Fill");
            fillArea.transform.SetParent(slGo.transform, false);
            var fRt = fillArea.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            var fillImg = fillArea.AddComponent<Image>();
            fillImg.color = new Color(0.96f, 0.48f, 0.20f, 0.92f);

            var slider = slGo.AddComponent<Slider>();
            slider.fillRect = fRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initial;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            return lbl;
        }
    }
}
