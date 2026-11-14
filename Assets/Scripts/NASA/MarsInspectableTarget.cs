using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    public enum MarsInspectActionType
    {
        InspectOnly = 0,
        BoardRover = 1,
        EnterFacility = 2
    }

    /// <summary>
    /// Attaches real NASA/USGS planetary science metadata, imagery reference,
    /// and interactive discovery tracking to any rock outcrop, sample tube,
    /// base facility, rover, or spacecraft in the Unity scene.
    /// </summary>
    public class MarsInspectableTarget : MonoBehaviour
    {
        private static readonly HashSet<string> s_DiscoveredIds = new HashSet<string>();
        private static readonly List<MarsInspectableTarget> s_AllTargets = new List<MarsInspectableTarget>();

        public static event Action<int, int> OnDiscoveryCountChanged;

        public static int TotalCount => s_AllTargets.Count;
        public static int DiscoveredCount => s_DiscoveredIds.Count;
        public static IReadOnlyList<MarsInspectableTarget> AllTargets => s_AllTargets;

        public static void ResetDiscoverySession()
        {
            s_DiscoveredIds.Clear();
            s_AllTargets.RemoveAll(t => t == null);
            OnDiscoveryCountChanged?.Invoke(0, s_AllTargets.Count);
        }

        [Header("NASA Science Identity")]
        public string targetId = "mars_target";
        public string title = "Martian Formation";
        public string category = "Geological Outcrop";
        public string subtitle = "Jezero Crater · IAU2000";

        [Header("Scientific Payload")]
        [TextArea(2, 4)] public string coordinatesText = "18.4447°N, 77.4508°E · -2569.9 m MOLA";
        [TextArea(2, 5)] public string geologyText = "USGS SIM 3292 HNhu";
        [TextArea(2, 5)] public string mineralogyText = "Pyroxene, Plagioclase, Olivine";
        [TextArea(2, 5)] public string environmentText = "MEDA P: 715 Pa · RAD: 0.67 mSv/day";
        [TextArea(3, 6)] public string summaryText = "";
        public string sourceUrl = "https://science.nasa.gov/mission/mars-2020-perseverance/";
        public string imageFile = "perseverance_sample_raw.jpg";

        [Header("Interaction")]
        public MarsInspectActionType actionType = MarsInspectActionType.InspectOnly;
        public string actionHint = "Inspect NASA Data";
        public float interactionRadius = 8.0f;

        private bool isHighlighted = false;
        private Renderer[] cachedRenderers;
        private Color[] originalEmissions;

        public bool IsDiscovered => !string.IsNullOrEmpty(targetId) && s_DiscoveredIds.Contains(targetId);

        private void OnEnable()
        {
            if (!s_AllTargets.Contains(this))
            {
                s_AllTargets.Add(this);
                OnDiscoveryCountChanged?.Invoke(s_DiscoveredIds.Count, s_AllTargets.Count);
            }
        }

        private void OnDestroy()
        {
            s_AllTargets.Remove(this);
        }

        public void Configure(
            string id,
            string title,
            string category,
            string subtitle,
            string coords,
            string geology,
            string mineralogy,
            string environment,
            string summary,
            string sourceUrl,
            string imageFile,
            MarsInspectActionType actionType = MarsInspectActionType.InspectOnly,
            string actionHint = "Inspect NASA Data",
            float interactionRadius = 8.0f)
        {
            this.targetId = id;
            this.title = title;
            this.category = category;
            this.subtitle = subtitle;
            this.coordinatesText = coords;
            this.geologyText = geology;
            this.mineralogyText = mineralogy;
            this.environmentText = environment;
            this.summaryText = summary;
            this.sourceUrl = sourceUrl;
            this.imageFile = imageFile;
            this.actionType = actionType;
            this.actionHint = actionHint;
            this.interactionRadius = interactionRadius;
        }

        public bool MarkDiscovered()
        {
            if (string.IsNullOrEmpty(targetId)) return false;
            bool newlyDiscovered = s_DiscoveredIds.Add(targetId);
            if (newlyDiscovered)
            {
                OnDiscoveryCountChanged?.Invoke(s_DiscoveredIds.Count, s_AllTargets.Count);
            }
            return newlyDiscovered;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted) return;
            isHighlighted = highlighted;

            if (cachedRenderers == null)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>(true);
                originalEmissions = new Color[cachedRenderers.Length];
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null && cachedRenderers[i].material != null && cachedRenderers[i].material.HasProperty("_EmissionColor"))
                    {
                        originalEmissions[i] = cachedRenderers[i].material.GetColor("_EmissionColor");
                    }
                }
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer r = cachedRenderers[i];
                if (r == null || r.material == null) continue;
                Material m = r.material;
                if (!m.HasProperty("_EmissionColor")) continue;
                if (highlighted)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", originalEmissions[i] + new Color(0.18f, 0.42f, 0.55f, 1f) * 0.45f);
                }
                else
                {
                    m.SetColor("_EmissionColor", originalEmissions[i]);
                }
            }
        }
    }
}
