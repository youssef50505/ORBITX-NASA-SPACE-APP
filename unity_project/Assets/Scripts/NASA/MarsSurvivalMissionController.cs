using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Real playable Mars EVA Survival & Science Mission Loop:
    /// - Live Suit Oxygen (O2 %), Suit/Rover Power (%), Radiation Trauma (%), and RF Link (%)
    /// - Dynamic resource drain on EVA/sprinting/storms and recovery inside Agri Dome / NASA HDU / Medical Bay
    /// - 3 collectible Jezero science core beacons with particle bursts, 3D objective compass, scoring, and MedEvac respawn.
    /// </summary>
    [DefaultExecutionOrder(260)]
    public class MarsSurvivalMissionController : MonoBehaviour
    {
        public static MarsSurvivalMissionController Instance { get; private set; }

        public float OxygenPercent { get; private set; } = 100f;
        public float PowerPercent { get; private set; } = 96f;
        public float RadiationTraumaPercent { get; private set; } = 4f;
        public float CumulativeRadMsv { get; private set; } = 0.12f;
        public int MissionScore { get; private set; } = 0;
        public int CollectedSamplesCount { get; private set; } = 0;
        public int TotalMissionSamples => sampleSites.Count;
        public bool IsMedEvacActive { get; private set; } = false;
        public bool IsCampaignComplete { get; private set; } = false;
        public string ActiveAlertBanner { get; private set; } = string.Empty;
        public float AlertBannerTimer { get; private set; } = 0f;

        public Vector3 ActiveObjectiveWorldPos { get; private set; }
        public string ActiveObjectiveLabel { get; private set; } = "Collect Jezero Core Samples (0/3)";
        public float ActiveObjectiveDistanceM { get; private set; }
        public float ActiveObjectiveBearingDeg { get; private set; }

        private sealed class MissionSampleSite
        {
            public string id;
            public string displayName;
            public string mineralNote;
            public Vector2 xz;
            public bool collected;
            public GameObject beaconRoot;
            public Transform crystalOrb;
        }

        private readonly List<MissionSampleSite> sampleSites = new List<MissionSampleSite>();
        private float medEvacTimer = 0f;
        private bool samplesSpawned = false;

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
            SpawnMissionSamples();
        }

        private void SpawnMissionSamples()
        {
            if (samplesSpawned) return;
            samplesSpawned = true;

            var defs = new (string id, string name, string mineral, Vector2 xz)[]
            {
                ("sample_cheyava", "Cheyava Falls Core #1", "Organics + Vivianite Leopard Spots", new Vector2(12.5f, 8.0f)),
                ("sample_cacao", "Cacao Iron-Nickel Meteorite #2", "Exogenic Fe-Ni Kamacite Fragment", new Vector2(-14.5f, 6.5f)),
                ("sample_belva", "Belva Rim Stratigraphy #3", "Deltaic Smectite Clay & Carbonate", new Vector2(22.0f, -12.0f))
            };

            GameObject container = new GameObject("MS_Mission_SampleBeacons");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material ringMat = new Material(litShader);
            ringMat.color = new Color(0.12f, 0.90f, 0.85f, 1f);
            if (ringMat.HasProperty("_EmissionColor"))
            {
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", new Color(0.12f, 0.95f, 0.90f) * 2.2f);
            }

            Material coreMat = new Material(litShader);
            coreMat.color = new Color(0.95f, 0.72f, 0.22f, 1f);
            if (coreMat.HasProperty("_EmissionColor"))
            {
                coreMat.EnableKeyword("_EMISSION");
                coreMat.SetColor("_EmissionColor", new Color(1.0f, 0.78f, 0.25f) * 2.4f);
            }

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                float y = MarsVisualRealismPolish.SampleTerrainElevation(d.xz.x, d.xz.y);
                GameObject siteGo = new GameObject("SampleBeacon_" + d.id);
                siteGo.transform.SetParent(container.transform, false);
                siteGo.transform.position = new Vector3(d.xz.x, y, d.xz.y);

                GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ped.name = "SamplePedestal";
                ped.transform.SetParent(siteGo.transform, false);
                ped.transform.localPosition = new Vector3(0f, 0.14f, 0f);
                ped.transform.localScale = new Vector3(0.9f, 0.14f, 0.9f);
                ped.GetComponent<Renderer>().sharedMaterial = ringMat;
                Destroy(ped.GetComponent<Collider>());

                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                orb.name = "SampleCoreTube";
                orb.transform.SetParent(siteGo.transform, false);
                orb.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                orb.transform.localScale = new Vector3(0.26f, 0.42f, 0.26f);
                orb.GetComponent<Renderer>().sharedMaterial = coreMat;
                Destroy(orb.GetComponent<Collider>());

                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "BeaconBeam";
                beam.transform.SetParent(siteGo.transform, false);
                beam.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                beam.transform.localScale = new Vector3(0.06f, 4.5f, 0.06f);
                beam.GetComponent<Renderer>().sharedMaterial = ringMat;
                Destroy(beam.GetComponent<Collider>());

                sampleSites.Add(new MissionSampleSite
                {
                    id = d.id,
                    displayName = d.name,
                    mineralNote = d.mineral,
                    xz = d.xz,
                    collected = false,
                    beaconRoot = siteGo,
                    crystalOrb = orb.transform
                });
            }
        }

        private void Update()
        {
            if (AlertBannerTimer > 0f)
            {
                AlertBannerTimer -= Time.deltaTime;
                if (AlertBannerTimer <= 0f) ActiveAlertBanner = string.Empty;
            }

            if (IsMedEvacActive)
            {
                medEvacTimer -= Time.deltaTime;
                if (medEvacTimer <= 0f)
                {
                    CompleteMedEvacRespawn();
                }
                return;
            }

            // Skip survival drain during automated showcase demos
            bool demoRunning = MarsSceneManager.Instance != null && MarsSceneManager.Instance.IsPresentationDirectorActive;
            bool exploreActive = MarsVisualRealismPolish.Instance == null || MarsVisualRealismPolish.Instance.IsExploreModeActive;
            if (demoRunning || !exploreActive)
            {
                AnimateSampleBeacons();
                UpdateObjectiveTracker();
                return;
            }

            float dt = Time.deltaTime;
            bool inRover = MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated;
            bool sheltered = MarsAudioManager.Instance != null && MarsAudioManager.Instance.IsInsideFacility;
            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float speed = inRover
                ? Mathf.Abs(MarsRoverVehicleController.Instance.CurrentSpeedMps)
                : (MarsAstronautController.Instance != null ? MarsAstronautController.Instance.CurrentSpeedMps : 0f);
            bool sprinting = !inRover && speed > 5.2f;

            // 1. Oxygen Simulation
            if (sheltered)
            {
                OxygenPercent = Mathf.MoveTowards(OxygenPercent, 100f, 14f * dt);
            }
            else if (inRover)
            {
                // Pressurized cabin stabilizes and slowly tops up suit O2
                OxygenPercent = Mathf.MoveTowards(OxygenPercent, 100f, 3.5f * dt);
            }
            else
            {
                float drain = sprinting ? 0.85f : (speed > 0.3f ? 0.42f : 0.16f);
                if (tau > 1.2f) drain *= 1.35f; // Heavy dust strains scrubber
                OxygenPercent = Mathf.Max(0f, OxygenPercent - drain * dt);
            }

            // 2. Solar & Battery Power Simulation
            float solarFlux = MarsCtxNavSolarEngine.Instance != null ? MarsCtxNavSolarEngine.Instance.CurrentSurfaceIrradianceWm2 : 360f;
            if (sheltered)
            {
                PowerPercent = Mathf.MoveTowards(PowerPercent, 100f, 16f * dt);
            }
            else
            {
                float solarGain = Mathf.Clamp(solarFlux / 480f, 0f, 1f) * 0.55f;
                float powerUse = inRover ? (0.15f + speed * 0.08f) : (sprinting ? 0.45f : 0.14f);
                PowerPercent = Mathf.Clamp(PowerPercent + (solarGain - powerUse) * dt, 0f, 100f);
            }

            // 3. Radiation & Dust Storm Triage
            if (sheltered)
            {
                RadiationTraumaPercent = Mathf.MoveTowards(RadiationTraumaPercent, 0f, 10f * dt);
            }
            else
            {
                float stormSpike = tau > 1.2f ? (tau - 1.0f) * 1.45f : 0.06f;
                if (inRover) stormSpike *= 0.45f; // Rover hull attenuates 55%
                RadiationTraumaPercent = Mathf.Clamp(RadiationTraumaPercent + stormSpike * dt, 0f, 100f);
                CumulativeRadMsv += (0.002f + stormSpike * 0.006f) * dt;
            }

            // Check Emergency MedEvac Trigger
            if (OxygenPercent <= 0.1f || RadiationTraumaPercent >= 99.9f)
            {
                TriggerMedEvac(OxygenPercent <= 0.1f ? "SUIT O2 DEPLETED — EMERGENCY MEDEVAC TO NASA HDU" : "CRITICAL RADIATION EXPOSURE — EMERGENCY MEDEVAC");
                return;
            }

            // Proximity sample collection & [E]/[X] pickup
            AnimateSampleBeacons();
            CheckSampleCollection();
            UpdateObjectiveTracker();
        }

        private void AnimateSampleBeacons()
        {
            for (int i = 0; i < sampleSites.Count; i++)
            {
                var s = sampleSites[i];
                if (s.collected || s.crystalOrb == null) continue;
                s.crystalOrb.Rotate(0f, 75f * Time.deltaTime, 0f, Space.Self);
                s.crystalOrb.localPosition = new Vector3(0f, 1.05f + Mathf.Sin(Time.time * 2.8f + i) * 0.16f, 0f);
            }
        }

        private void CheckSampleCollection()
        {
            Vector3 playerPos = GetPlayerPos();
            var kb = Keyboard.current;
            bool pressedInteract = kb != null && (kb.eKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame);

            for (int i = 0; i < sampleSites.Count; i++)
            {
                var s = sampleSites[i];
                if (s.collected || s.beaconRoot == null) continue;
                float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), s.xz);
                if (dist <= 3.8f || (dist <= 6.2f && pressedInteract))
                {
                    s.collected = true;
                    CollectedSamplesCount++;
                    MissionScore += 250;
                    s.beaconRoot.SetActive(false);
                    MarsCinematicVfxAndPost.Instance?.EmitScienceBurst(s.beaconRoot.transform.position, 18);
                    MarsAudioManager.Instance?.PlayUiClick();
                    ShowAlert($"CORE COLLECTED ({CollectedSamplesCount}/{sampleSites.Count}): {s.displayName} (+250 PTS)", 4.2f);
                    MarsSceneManager.Instance?.RefreshLiveExplorationHud();
                }
            }

            // Complete campaign once all 3 samples are collected AND player returns safely inside shelter
            bool sheltered = MarsAudioManager.Instance != null && MarsAudioManager.Instance.IsInsideFacility;
            if (!IsCampaignComplete && CollectedSamplesCount >= sampleSites.Count && sheltered)
            {
                IsCampaignComplete = true;
                MissionScore += 500;
                MarsCinematicVfxAndPost.Instance?.EmitScienceBurst(playerPos, 28);
                ShowAlert($"JEZERO MISSION COMPLETE! SCORE: {MissionScore} (RANK {GetMissionRank()}) · FREE EXPLORE ACTIVE", 6.5f);
            }
        }

        private void UpdateObjectiveTracker()
        {
            Vector3 p = GetPlayerPos();

            // Priority 1: Low O2 or Radiation -> Shelter at Medical Bay / HDU
            if (OxygenPercent < 32f || RadiationTraumaPercent > 60f)
            {
                Vector2 shelter = RadiationTraumaPercent > 60f ? MarsBaseFacilitiesBuilder.POS_MEDICAL_BAY : MarsBaseFacilitiesBuilder.POS_HABITAT_HDU;
                float sy = MarsVisualRealismPolish.SampleTerrainElevation(shelter.x, shelter.y);
                ActiveObjectiveWorldPos = new Vector3(shelter.x, sy + 2.5f, shelter.y);
                ActiveObjectiveLabel = RadiationTraumaPercent > 60f ? "URGENT: Shelter in Medical Bay" : "LOW O2: Recharge at NASA HDU / Agri Dome";
            }
            else
            {
                // Priority 2: Nearest uncollected mission sample
                MissionSampleSite nearest = null;
                float bestD = float.MaxValue;
                for (int i = 0; i < sampleSites.Count; i++)
                {
                    if (sampleSites[i].collected) continue;
                    float d = Vector2.Distance(new Vector2(p.x, p.z), sampleSites[i].xz);
                    if (d < bestD) { bestD = d; nearest = sampleSites[i]; }
                }

                if (nearest != null)
                {
                    float y = MarsVisualRealismPolish.SampleTerrainElevation(nearest.xz.x, nearest.xz.y);
                    ActiveObjectiveWorldPos = new Vector3(nearest.xz.x, y + 1.8f, nearest.xz.y);
                    ActiveObjectiveLabel = $"Collect {nearest.displayName} ({CollectedSamplesCount}/{sampleSites.Count})";
                }
                else if (!IsCampaignComplete)
                {
                    Vector2 hab = MarsBaseFacilitiesBuilder.POS_HABITAT_HDU;
                    float hy = MarsVisualRealismPolish.SampleTerrainElevation(hab.x, hab.y);
                    ActiveObjectiveWorldPos = new Vector3(hab.x, hy + 2.4f, hab.y);
                    ActiveObjectiveLabel = "Return Core Samples to NASA HDU / Agri Dome";
                }
                else
                {
                    Vector2 pad = MarsBaseFacilitiesBuilder.POS_ROCKET_PAD;
                    float py = MarsVisualRealismPolish.SampleTerrainElevation(pad.x, pad.y);
                    ActiveObjectiveWorldPos = new Vector3(pad.x, py + 4.0f, pad.y);
                    ActiveObjectiveLabel = $"Mission Complete ({MissionScore} PTS · Rank {GetMissionRank()}) · Explore Jezero";
                }
            }

            Vector3 delta = ActiveObjectiveWorldPos - p;
            ActiveObjectiveDistanceM = new Vector2(delta.x, delta.z).magnitude;
            ActiveObjectiveBearingDeg = (Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + 360f) % 360f;
        }

        private void TriggerMedEvac(string reason)
        {
            IsMedEvacActive = true;
            medEvacTimer = 2.8f;
            ShowAlert(reason, 3.0f);
        }

        private void CompleteMedEvacRespawn()
        {
            IsMedEvacActive = false;
            OxygenPercent = 100f;
            PowerPercent = 95f;
            RadiationTraumaPercent = 5f;
            MarsAstronautController.Instance?.TeleportToFacility("Habitat");
            ShowAlert("VITALS STABILIZED AT NASA HDU · READY FOR EVA", 3.5f);
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        public void ShowAlert(string text, float seconds = 3.5f)
        {
            ActiveAlertBanner = text;
            AlertBannerTimer = seconds;
        }

        public string GetMissionRank()
        {
            if (MissionScore >= 1200) return "S";
            if (MissionScore >= 900) return "A";
            if (MissionScore >= 500) return "B";
            return "C";
        }

        private static Vector3 GetPlayerPos()
        {
            if (MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated)
                return MarsRoverVehicleController.Instance.transform.position;
            if (MarsAstronautController.Instance != null)
                return MarsAstronautController.Instance.transform.position;
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }
    }
}
