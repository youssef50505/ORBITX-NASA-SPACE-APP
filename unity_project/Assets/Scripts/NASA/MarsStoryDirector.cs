using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarsSentinel.NASA
{
    public enum MarsStoryChapter
    {
        Ch1_DustfallArrival = 1,
        Ch2_CheyavaBiosignature = 2,
        Ch3_SolarFlareTriage = 3,
        Ch4_BelvaRelayAndMeteorite = 4,
        Ch5_SampleReturnIgnition = 5,
        Epilogue_Complete = 6
    }

    /// <summary>
    /// 5-Chapter Cinematic Narrative & Mission Director ("SOL 1042: ECHOES OF JEZERO").
    /// Drives chapter transitions, dramatic DONKI Solar Flare & Dust Storm triage,
    /// RIMFAX X-Ray biosignature discovery, Mars Ascent Rocket Ignition & Liftoff VFX,
    /// and a sleek bilingual EN/AR Aerospace Radio Comms subtitle bar.
    /// Press [T] anytime to preview/advance Story Chapters cleanly.
    /// </summary>
    [DefaultExecutionOrder(460)]
    public class MarsStoryDirector : MonoBehaviour
    {
        public static MarsStoryDirector Instance { get; private set; }

        public MarsStoryChapter CurrentChapter { get; private set; } = MarsStoryChapter.Ch1_DustfallArrival;
        public bool IsStoryActive { get; private set; } = false;
        public bool IsRocketLaunching { get; private set; } = false;
        public string ActiveSpeaker { get; private set; } = "HOUSTON CAPCOM";
        public string ActiveDialogue { get; private set; } = string.Empty;

        private Canvas storyCanvas;
        private GameObject commsBanner;
        private Text txtChapterTag;
        private Text txtSpeaker;
        private Text txtDialogue;
        private Text txtDirective;
        private Font uiFont;

        private float chapterTimer = 0f;
        private float launchTimer = 0f;
        private Transform launchRocketTf;
        private Vector3 launchRocketStartPos;
        private ParticleSystem launchFirePs;
        private Light launchLight;

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
            BuildStoryCommsHud();

            if (MarsLocalizationManager.Instance != null)
            {
                MarsLocalizationManager.Instance.OnLanguageChanged += _ => RefreshChapterCopy();
            }

            StartCoroutine(RunShowcaseCaptureIfRequested());
        }

        private IEnumerator RunShowcaseCaptureIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outDir = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-capture-showcase", StringComparison.OrdinalIgnoreCase))
                {
                    outDir = args[i + 1];
                    break;
                }
            }
            if (string.IsNullOrEmpty(outDir)) yield break;

            Directory.CreateDirectory(outDir);
            yield return new WaitForSecondsRealtime(1.5f);

            // Enter live gameplay with sleek HUD + Chapter 1 Story Comms + sunlit Jezero base
            MarsGameShellUI.Instance?.SetState(MarsGameState.Playing);
            BeginCampaign();
            MarsSceneManager.Instance?.SetFullHudVisible(false);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "unity_game_live.png"));

            // Advance to Chapter 2 (Cheyava Falls Biosignature) + Full Telemetry HUD + LIVE URP Render Graph [X] X-Ray Scanner
            yield return new WaitForSecondsRealtime(0.6f);
            MarsSceneManager.Instance?.SetFullHudVisible(true);
            EnterChapter(MarsStoryChapter.Ch2_CheyavaBiosignature);
            Vector3 scanOrigin = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 14f : Vector3.zero;
            MarsXRayScannerRendererFeature.TogglePersistentOverlay(scanOrigin);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "unity_game_xray_story.png"));

            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit(0);
        }

        public void BeginCampaign()
        {
            IsStoryActive = true;
            EnterChapter(MarsStoryChapter.Ch1_DustfallArrival);
        }

        public void AdvanceStoryChapter()
        {
            if (!IsStoryActive)
            {
                BeginCampaign();
                return;
            }
            int next = Mathf.Min((int)MarsStoryChapter.Epilogue_Complete, (int)CurrentChapter + 1);
            EnterChapter((MarsStoryChapter)next);
        }

        public void EnterChapter(MarsStoryChapter chapter)
        {
            CurrentChapter = chapter;
            chapterTimer = 0f;
            if (commsBanner != null) commsBanner.SetActive(true);
            RefreshChapterCopy();

            switch (chapter)
            {
                case MarsStoryChapter.Ch1_DustfallArrival:
                    MarsAudioManager.Instance?.PlayUiClick();
                    break;

                case MarsStoryChapter.Ch2_CheyavaBiosignature:
                    Vector3 origin = MarsAstronautController.Instance != null
                        ? MarsAstronautController.Instance.transform.position
                        : Vector3.zero;
                    MarsXRayScannerRendererFeature.TriggerScanPulse(origin, 0);
                    MarsAudioManager.Instance?.PlayScienceScan(true);
                    break;

                case MarsStoryChapter.Ch3_SolarFlareTriage:
                    // Scripted dramatic DONKI Solar Particle Event + Tau 2.4 Dust Storm!
                    MarsSceneManager.Instance?.SetStormIntensityExternal(2.35f);
                    MarsAudioManager.Instance?.PlayUiClick();
                    MarsSurvivalMissionController.Instance?.ShowAlert("DONKI SEP ALERT: TAU 2.4 DUST STORM — SHELTER AT MEDICAL BAY / HDU!", 5.0f);
                    break;

                case MarsStoryChapter.Ch4_BelvaRelayAndMeteorite:
                    // Storm clears into crisp Martian Golden/Blue Sunset
                    MarsSceneManager.Instance?.SetStormIntensityExternal(0.45f);
                    MarsCelestialOrbitController.Instance?.SetSolHour(17.6f);
                    MarsAudioManager.Instance?.PlayScienceScan(true);
                    break;

                case MarsStoryChapter.Ch5_SampleReturnIgnition:
                    TriggerRocketLaunchSequence();
                    break;

                case MarsStoryChapter.Epilogue_Complete:
                    int score = MarsSurvivalMissionController.Instance != null ? Mathf.Max(1250, MarsSurvivalMissionController.Instance.MissionScore + 500) : 1250;
                    float rad = MarsSurvivalMissionController.Instance != null ? MarsSurvivalMissionController.Instance.CumulativeRadMsv : 0.42f;
                    MarsGameShellUI.Instance?.TriggerMissionEnded(score, rad, 3);
                    break;
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame)
            {
                AdvanceStoryChapter();
            }

            if (IsStoryActive && Time.timeScale > 0.01f)
            {
                chapterTimer += Time.deltaTime;
                EvaluateStoryProgression();
            }

            if (IsRocketLaunching)
            {
                UpdateRocketLaunchSequence();
            }
        }

        private void EvaluateStoryProgression()
        {
            var survival = MarsSurvivalMissionController.Instance;
            if (survival == null) return;

            if (CurrentChapter == MarsStoryChapter.Ch1_DustfallArrival)
            {
                bool boarded = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;
                if (boarded || survival.CollectedSamplesCount >= 1 || chapterTimer > 22f)
                {
                    EnterChapter(MarsStoryChapter.Ch2_CheyavaBiosignature);
                }
            }
            else if (CurrentChapter == MarsStoryChapter.Ch2_CheyavaBiosignature)
            {
                if (survival.CollectedSamplesCount >= 1)
                {
                    EnterChapter(MarsStoryChapter.Ch3_SolarFlareTriage);
                }
            }
            else if (CurrentChapter == MarsStoryChapter.Ch3_SolarFlareTriage)
            {
                bool sheltered = MarsAudioManager.Instance != null && MarsAudioManager.Instance.IsInsideFacility;
                if (sheltered || chapterTimer > 24f)
                {
                    EnterChapter(MarsStoryChapter.Ch4_BelvaRelayAndMeteorite);
                }
            }
            else if (CurrentChapter == MarsStoryChapter.Ch4_BelvaRelayAndMeteorite)
            {
                if (survival.CollectedSamplesCount >= 3)
                {
                    EnterChapter(MarsStoryChapter.Ch5_SampleReturnIgnition);
                }
            }
        }

        public void TriggerRocketLaunchSequence()
        {
            if (IsRocketLaunching) return;
            IsRocketLaunching = true;
            launchTimer = 0f;

            // Locate Mars Ascent Vehicle / Saturn V / Ares rocket on Launch Pad
            GameObject pad = GameObject.Find("MS_Mars_Ascent_Launch_Pad") ?? GameObject.Find("MS_Hero_LaunchPad");
            if (pad != null)
            {
                foreach (Transform t in pad.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("Ares", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Saturn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Rocket", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        launchRocketTf = t;
                        launchRocketStartPos = t.position;
                        break;
                    }
                }
            }

            Vector3 padCenter = launchRocketTf != null
                ? launchRocketTf.position
                : new Vector3(MarsBaseFacilitiesBuilder.POS_ROCKET_PAD.x,
                    MarsVisualRealismPolish.SampleTerrainElevation(MarsBaseFacilitiesBuilder.POS_ROCKET_PAD.x, MarsBaseFacilitiesBuilder.POS_ROCKET_PAD.y) + 1.0f,
                    MarsBaseFacilitiesBuilder.POS_ROCKET_PAD.y);

            // Volumetric ignition exhaust plume + blazing orange-white thrust light
            GameObject fxGo = new GameObject("MS_LaunchIgnitionVFX");
            fxGo.transform.position = padCenter;
            launchFirePs = fxGo.AddComponent<ParticleSystem>();
            var main = launchFirePs.main;
            main.startLifetime = 2.4f;
            main.startSpeed = 14f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 4.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.72f, 0.25f, 0.92f), new Color(0.85f, 0.48f, 0.28f, 0.65f));
            main.maxParticles = 280;
            var emission = launchFirePs.emission;
            emission.rateOverTime = 75f;
            var shape = launchFirePs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 36f;
            shape.radius = 1.8f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var lightGo = new GameObject("MS_LaunchFlareLight");
            lightGo.transform.SetParent(fxGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            launchLight = lightGo.AddComponent<Light>();
            launchLight.type = LightType.Point;
            launchLight.color = new Color(1.0f, 0.66f, 0.24f);
            launchLight.range = 85f;
            launchLight.intensity = 28f;

            MarsSurvivalMissionController.Instance?.ShowAlert("T-0 IGNITION! MARS ASCENT VEHICLE LIFTOFF — JEZERO CORES BOUND FOR EARTH!", 6.0f);
        }

        private void UpdateRocketLaunchSequence()
        {
            launchTimer += Time.deltaTime;
            if (launchRocketTf != null && launchTimer > 1.5f)
            {
                float t = launchTimer - 1.5f;
                float ascentY = 0.65f * t * t * 3.8f; // smooth accelerating vertical ascent
                launchRocketTf.position = launchRocketStartPos + new Vector3(0f, ascentY, 0f);
                if (launchFirePs != null)
                {
                    launchFirePs.transform.position = launchRocketTf.position;
                }
            }

            if (launchLight != null)
            {
                launchLight.intensity = 24f + Mathf.Sin(launchTimer * 32f) * 6f;
            }

            if (launchTimer >= 9.5f)
            {
                IsRocketLaunching = false;
                if (launchRocketTf != null) launchRocketTf.position = launchRocketStartPos;
                if (launchFirePs != null) Destroy(launchFirePs.gameObject);
                EnterChapter(MarsStoryChapter.Epilogue_Complete);
            }
        }

        private void RefreshChapterCopy()
        {
            bool ar = MarsLocalizationManager.Instance != null && MarsLocalizationManager.Instance.IsArabic;
            string tag = "", spk = "", line = "", dir = "";

            switch (CurrentChapter)
            {
                case MarsStoryChapter.Ch1_DustfallArrival:
                    tag = ar ? "الفصل 1/5 · الوصول إلى جيزيرو" : "CHAPTER 1/5 · DUSTFALL ARRIVAL";
                    spk = ar ? "غرفة التحكم — هيوستن (تأخير 18د 42ث)" : "HOUSTON CAPCOM (18m 42s LIGHT DELAY)";
                    line = ar
                        ? "أوربت إكس-1، أهلاً بك في فوهة جيزيرو. أنت خارج نطاق GPS الأرضي — افحص القاعدة واركب الروفر بيرسيفيرانس."
                        : "ORBITX-1, welcome to Octavia E. Butler Base. No GPS, 18-minute comms delay — inspect NASA HDU and board Perseverance.";
                    dir = ar ? "المهمة: اضغط [E] لركوب الروفر أو استكشف القاعدة · [T] للفصل التالي" : "OBJECTIVE: Press [E] to board Rover or explore Base · [T] Next Chapter";
                    break;
                case MarsStoryChapter.Ch2_CheyavaBiosignature:
                    tag = ar ? "الفصل 2/5 · البصمة الحيوية في شيفا فولز" : "CHAPTER 2/5 · CHEYAVA FALLS BIOSIGNATURE";
                    spk = ar ? "د. نادية (EVA-02)" : "DR. NADIA (EVA-02 WINGMAN)";
                    line = ar
                        ? "مسبار RIMFAX رصد طبقة طينية عضوية تحت السطح! فعّل الماسح بالأشعة [X] واجمع عينة شيفا فولز رقم 1."
                        : "RIMFAX subsurface radar just pinged organic mudstone! Trigger the [X] X-Ray Scanner and secure Cheyava Core #1.";
                    dir = ar ? "المهمة: اضغط [X] لمسح التضاريس واجمع العينة #1 · [T] للفصل التالي" : "OBJECTIVE: Press [X] for RIMFAX X-Ray Scan & collect Core #1 · [T] Next";
                    break;
                case MarsStoryChapter.Ch3_SolarFlareTriage:
                    tag = ar ? "الفصل 3/5 · عاصفة شمسية وغبارية (Tau 2.4)" : "CHAPTER 3/5 · DONKI SOLAR FLARE & STORM TRIAGE";
                    spk = ar ? "إنذار الطقس الفضائي — MAVEN / DONKI" : "SPACE WEATHER ALERT — MAVEN / DONKI";
                    line = ar
                        ? "تحذير! عاصفة غبارية وتوهج شمسي يضربان جيزيرو! انخفضت الرؤية وارتفع الإشعاع — احتمِ فوراً داخل الوحدة الطبية أو HDU."
                        : "WARNING! Solar Particle Event & Tau 2.4 Dust Storm striking Jezero! Radiation spiking — shelter immediately in Medical Bay or HDU!";
                    dir = ar ? "المهمة: ادخل قبة الزراعة أو الوحدة الطبية لحماية البدلة · [T] للفصل التالي" : "OBJECTIVE: Shelter inside Medical Bay, HDU, or Agri-Dome · [T] Next";
                    break;
                case MarsStoryChapter.Ch4_BelvaRelayAndMeteorite:
                    tag = ar ? "الفصل 4/5 · برج بيلفا ونيزك كاكاو" : "CHAPTER 4/5 · BELVA RIM RELAY & CACAO METEORITE";
                    spk = ar ? "الروبوت الكشاف (SCOUT-AI-01)" : "AUTONOMOUS SCOUT-AI-01 TELEMETRY";
                    line = ar
                        ? "انقشعت العاصفة مع غروب المريخ الأزرق! مسار A* الآمن (8.1°) مفتوح نحو نيزك كاكاو الحديدي وحافة بيلفا."
                        : "Storm clearing into Martian blue sunset! A* Safest Route (8.1 deg) is green to Cacao Fe-Ni Meteorite and Belva Rim.";
                    dir = ar ? "المهمة: اجمع العينتين المتبقيتين وتأكد من إشارة RF · [T] لإطلاق الصاروخ" : "OBJECTIVE: Collect remaining 2 cores & verify RF LOS · [T] Launch Finale";
                    break;
                case MarsStoryChapter.Ch5_SampleReturnIgnition:
                    tag = ar ? "الفصل 5/5 · إشعال صاروخ عودة العينات" : "CHAPTER 5/5 · MARS SAMPLE RETURN IGNITION";
                    spk = ar ? "قائد المهمة — منصة الإطلاق" : "MISSION COMMANDER — ASCENT PAD";
                    line = ar
                        ? "جميع العينات الثلاث مؤمّنة بالكامل! بدء العد التنازلي لإشعال محركات صاروخ العودة إلى الأرض... T-3، 2، 1، إقلاع!"
                        : "All 3 Jezero cores sealed aboard Mars Ascent Vehicle! Main engine ignition sequence started... T-3, 2, 1 — LIFTOFF!";
                    dir = ar ? "جاري الإقلاع نحو المدار المريخي!" : "ASCENT VEHICLE LIFTOFF IN PROGRESS — BOUND FOR EARTH ORBIT!";
                    break;
                default:
                    tag = ar ? "الخاتمة · اكتملت مهمة سول 1042" : "EPILOGUE · SOL 1042 CAMPAIGN COMPLETE";
                    spk = ar ? "هيوستن — فريق ORBITX" : "HOUSTON — TEAM ORBITX";
                    line = ar
                        ? "إنجاز تاريخي! عينات جيزيرو في طريقها للأرض وقاعدة المريخ تعمل بكفاءة 98.4%."
                        : "Historic triumph! Jezero samples are en route to Earth and Mars Sentinel Base is nominal at 98.4% closed-loop efficiency.";
                    dir = ar ? "الاستكشاف الحر متاح بالكامل" : "Free Explore unlocked — Jezero Crater awaits.";
                    break;
            }

            ActiveSpeaker = spk;
            ActiveDialogue = line;
            if (txtChapterTag != null) txtChapterTag.text = tag;
            if (txtSpeaker != null) txtSpeaker.text = spk;
            if (txtDialogue != null) txtDialogue.text = line;
            if (txtDirective != null) txtDirective.text = dir;
        }

        private void BuildStoryCommsHud()
        {
            GameObject existing = GameObject.Find("MarsStoryCommsCanvas");
            if (existing != null) Destroy(existing);

            GameObject go = new GameObject("MarsStoryCommsCanvas");
            storyCanvas = go.AddComponent<Canvas>();
            storyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            storyCanvas.sortingOrder = 420;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            commsBanner = new GameObject("StoryCommsBanner");
            commsBanner.transform.SetParent(go.transform, false);
            var rt = commsBanner.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 64f);
            rt.sizeDelta = new Vector2(960f, 114f);

            var bg = commsBanner.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.065f, 0.11f, 0.84f);
            var outline = commsBanner.AddComponent<Outline>();
            outline.effectColor = new Color(0.98f, 0.52f, 0.22f, 0.45f);
            outline.effectDistance = new Vector2(1.2f, 1.2f);

            txtChapterTag = MakeLabel(commsBanner.transform, "ChapterTag", 13, FontStyle.Bold,
                new Color(1.0f, 0.62f, 0.28f), new Vector2(-240f, 40f), new Vector2(440f, 22f), TextAnchor.MiddleLeft);
            txtSpeaker = MakeLabel(commsBanner.transform, "Speaker", 13, FontStyle.Bold,
                new Color(0.32f, 0.88f, 0.96f), new Vector2(220f, 40f), new Vector2(480f, 22f), TextAnchor.MiddleRight);
            txtDialogue = MakeLabel(commsBanner.transform, "Dialogue", 16, FontStyle.Normal,
                Color.white, new Vector2(0f, 4f), new Vector2(920f, 46f), TextAnchor.MiddleCenter);
            txtDirective = MakeLabel(commsBanner.transform, "Directive", 13, FontStyle.Bold,
                new Color(0.68f, 0.94f, 0.82f), new Vector2(0f, -38f), new Vector2(920f, 24f), TextAnchor.MiddleCenter);

            commsBanner.SetActive(false);
        }

        private Text MakeLabel(Transform parent, string name, int size, FontStyle style, Color col, Vector2 pos, Vector2 dims, TextAnchor align)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var rt = g.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dims;
            var t = g.AddComponent<Text>();
            t.font = uiFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = col;
            t.alignment = align;
            return t;
        }
    }
}
