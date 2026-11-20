using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    public enum MarsLanguage
    {
        English = 0,
        Arabic = 1
    }

    /// <summary>
    /// Bilingual English + Arabic localization service for Mars Sentinel Unity 6 UI.
    /// Persists preference in PlayerPrefs and provides runtime key lookup + RTL event broadcasts.
    /// </summary>
    public class MarsLocalizationManager : MonoBehaviour
    {
        public static MarsLocalizationManager Instance { get; private set; }

        public MarsLanguage CurrentLanguage { get; private set; } = MarsLanguage.English;
        public bool IsArabic => CurrentLanguage == MarsLanguage.Arabic;
        public event Action<MarsLanguage> OnLanguageChanged;

        private const string PrefKey = "MarsSentinel_Language";

        private static readonly Dictionary<string, (string en, string ar)> Table =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "title_main", ("ORBITX · MARS SENTINEL", "ORBITX · حارس المريخ") },
            { "title_sub", ("JEZERO CRATER DIGITAL TWIN & EVA SURVIVAL", "التوأم الرقمي لفوهة جيزيرو والبقاء على المريخ") },
            { "btn_launch", ("LAUNCH EVA MISSION", "ابدأ مهمة الاستكشاف") },
            { "btn_explore", ("FREE EXPLORE JEZERO", "استكشاف حر لفوهة جيزيرو") },
            { "btn_lang", ("LANGUAGE: EN / عربي", "اللغة: العربية / EN") },
            { "press_any", ("PRESS [ENTER] OR CLICK TO DEPLOY TO JEZERO CRATER", "اضغط ENTER أو انقر للهبوط في فوهة جيزيرو") },
            { "pause_title", ("MISSION PAUSED", "إيقاف المهمة مؤقتاً") },
            { "btn_resume", ("RESUME EVA", "استكمال المهمة") },
            { "btn_restart", ("RESTART MISSION", "إعادة المهمة") },
            { "btn_xray", ("TOGGLE X-RAY SCANNER [X]", "تفعيل ماسح الأشعة [X]") },
            { "btn_title", ("RETURN TO TITLE", "العودة للشاشة الرئيسية") },
            { "lbl_master", ("Master Volume", "مستوى الصوت الرئيسي") },
            { "lbl_ambience", ("Martian Wind & HVAC", "الرياح المريخية والتهوية") },
            { "lbl_sfx", ("Rover & Scan SFX", "مؤثرات الروفر والمسح") },
            { "ended_title", ("JEZERO CORE CAMPAIGN COMPLETE", "اكتملت حملة عينات جيزيرو بنجاح") },
            { "ended_sub", ("ALL 3 SCIENTIFIC CORES SECURED — READY FOR MARS SAMPLE RETURN", "تم جمع العينات الثلاث بنجاح — جاهز لإرسالها للأرض") },
            { "lbl_stats", ("MISSION DEBRIEF TELEMETRY", "ملخص بيانات المهمة") },
            { "lbl_best", ("PERSONAL BEST RECORD (SAVED)", "أفضل رقم قياسي محفوظ") },
            { "hud_o2", ("SUIT O2", "أكسجين البدلة") },
            { "hud_pwr", ("POWER", "الطاقة") },
            { "hud_rad", ("RAD DOSE", "الإشعاع") },
            { "hud_rf", ("RF LINK", "إشارة الاتصال") },
            { "hud_scout", ("AI SCOUT", "الروبوت الكشاف") },
            { "hud_wingman", ("CO-OP EVA-02", "الرائد المرافق") }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            int saved = PlayerPrefs.GetInt(PrefKey, 0);
            CurrentLanguage = saved == 1 ? MarsLanguage.Arabic : MarsLanguage.English;
        }

        public void SetLanguage(MarsLanguage lang)
        {
            if (CurrentLanguage == lang) return;
            CurrentLanguage = lang;
            PlayerPrefs.SetInt(PrefKey, (int)lang);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke(CurrentLanguage);
            Debug.Log($"[MarsLocalizationManager] Language switched to {CurrentLanguage}");
        }

        public void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == MarsLanguage.English ? MarsLanguage.Arabic : MarsLanguage.English);
        }

        public static string Get(string key, string fallback = null)
        {
            MarsLanguage lang = Instance != null ? Instance.CurrentLanguage : MarsLanguage.English;
            if (Table.TryGetValue(key, out var pair))
            {
                return lang == MarsLanguage.Arabic ? pair.ar : pair.en;
            }
            return fallback ?? key;
        }
    }
}
