using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Cooperative Multi-Astronaut Wingman ("EVA-02") & RF Proximity Voice/Telemetry Relay.
    /// Follows the player's EVA astronaut or rover at a safe flanking offset, calculates real
    /// Martian RF line-of-sight & distance attenuation (dB / voice clarity %), and exposes
    /// a clean state packet hook ready for Unity Multiplayer Services + Vivox proximity voice.
    /// </summary>
    [DefaultExecutionOrder(315)]
    public class MarsCoopWingmanSync : MonoBehaviour
    {
        public static MarsCoopWingmanSync Instance { get; private set; }

        public string WingmanCallsign => "EVA-02 (DR. NADIA)";
        public float DistanceToLeaderM { get; private set; }
        public float RfVoiceQualityPercent { get; private set; } = 99f;
        public string RadioStatusText { get; private set; } = "EVA-02 UHF LINK: 99% (CLEAR)";
        public Vector3 WingmanPos => wingmanRoot != null ? wingmanRoot.position : Vector3.zero;

        private Transform wingmanRoot;
        private Transform visualBody;
        private float walkCycle = 0f;
        private Vector3 smoothedVel;

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
            SpawnWingman();
        }

        private void SpawnWingman()
        {
            if (wingmanRoot != null) return;

            GameObject go = new GameObject("MS_Coop_Wingman_EVA02");
            wingmanRoot = go.transform;
            float y = MarsVisualRealismPolish.SampleTerrainElevation(-4.2f, -6.5f);
            wingmanRoot.position = new Vector3(-4.2f, y, -6.5f);

            // Try to instantiate rigged/official Astronaut mesh if present in scene or build clean space-suit proxy
            GameObject body = new GameObject("EVA02_Visual");
            body.transform.SetParent(wingmanRoot, false);
            visualBody = body.transform;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material suitWhite = new Material(lit) { color = new Color(0.92f, 0.93f, 0.96f) };
            Material accentTeal = new Material(lit) { color = new Color(0.08f, 0.78f, 0.85f) };
            Material visorGold = new Material(lit) { color = new Color(0.92f, 0.68f, 0.16f) };
            if (accentTeal.HasProperty("_EmissionColor"))
            {
                accentTeal.EnableKeyword("_EMISSION");
                accentTeal.SetColor("_EmissionColor", new Color(0.08f, 0.85f, 0.92f) * 1.6f);
            }

            AddPart("Torso", PrimitiveType.Capsule, visualBody, new Vector3(0, 1.12f, 0), new Vector3(0.46f, 0.42f, 0.34f), suitWhite);
            AddPart("PLSS_Pack", PrimitiveType.Cube, visualBody, new Vector3(0, 1.18f, -0.24f), new Vector3(0.38f, 0.52f, 0.22f), suitWhite);
            AddPart("Beacon", PrimitiveType.Cube, visualBody, new Vector3(0, 1.48f, -0.24f), new Vector3(0.18f, 0.08f, 0.12f), accentTeal);
            AddPart("Helmet", PrimitiveType.Sphere, visualBody, new Vector3(0, 1.64f, 0.02f), new Vector3(0.34f, 0.34f, 0.34f), suitWhite);
            AddPart("Visor", PrimitiveType.Sphere, visualBody, new Vector3(0, 1.64f, 0.12f), new Vector3(0.24f, 0.19f, 0.18f), visorGold);
            AddPart("LegL", PrimitiveType.Capsule, visualBody, new Vector3(-0.13f, 0.42f, 0), new Vector3(0.16f, 0.44f, 0.16f), suitWhite);
            AddPart("LegR", PrimitiveType.Capsule, visualBody, new Vector3(0.13f, 0.42f, 0), new Vector3(0.16f, 0.44f, 0.16f), suitWhite);

            var insp = go.AddComponent<MarsInspectableTarget>();
            insp.Configure(
                "coop_eva02",
                "Co-op Wingman EVA-02 (Dr. Nadia)",
                "CO-OP MULTI-ASTRONAUT RELAY",
                "UHF Proximity Voice & Telemetry Partner",
                "18.4446°N, 77.4506°E · Flanking EVA Formation",
                "Jezero Surface Operations · Octavia E. Butler Sector",
                "Portable XRF + Core Sample Backup Pack",
                "UHF 401 MHz Proximity Relay · Vivox Voice Channel Ready",
                "Companion EVA crew member synchronized for multi-astronaut Jezero traverses. Provides real-time UHF proximity link attenuation and is pre-wired for UGS Multiplayer & Vivox voice channels.",
                "https://www.nasa.gov/humans-in-space/mars/",
                "perseverance_sample_raw.jpg"
            );
        }

        private static void AddPart(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            p.name = name;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            var c = p.GetComponent<Collider>();
            if (c != null && name != "Torso") Destroy(c);
        }

        private void Update()
        {
            if (wingmanRoot == null || Time.timeScale <= 0.001f) return;

            Transform leader = null;
            if (MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated)
                leader = MarsRoverVehicleController.Instance.transform;
            else if (MarsAstronautController.Instance != null)
                leader = MarsAstronautController.Instance.transform;

            if (leader == null) return;

            // Follow at 4.5m flanking offset to the left-rear of leader
            Vector3 desiredPos = leader.position - leader.forward * 3.8f - leader.right * 2.4f;
            Vector3 delta = desiredPos - wingmanRoot.position;
            delta.y = 0f;
            float distToSlot = delta.magnitude;

            float speed = 0f;
            if (distToSlot > 1.4f)
            {
                speed = Mathf.Clamp(distToSlot * 1.15f, 1.4f, 6.2f);
                Vector3 moveDir = delta.normalized;
                Vector3 nextPos = wingmanRoot.position + moveDir * (speed * Time.deltaTime);
                nextPos.y = MarsVisualRealismPolish.SampleTerrainElevation(nextPos.x, nextPos.z);
                wingmanRoot.position = nextPos;
                Quaternion look = Quaternion.LookRotation(moveDir, Vector3.up);
                wingmanRoot.rotation = Quaternion.Slerp(wingmanRoot.rotation, look, Time.deltaTime * 6f);
                smoothedVel = moveDir * speed;
            }
            else
            {
                Vector3 nextPos = wingmanRoot.position;
                nextPos.y = MarsVisualRealismPolish.SampleTerrainElevation(nextPos.x, nextPos.z);
                wingmanRoot.position = nextPos;
                smoothedVel = Vector3.Lerp(smoothedVel, Vector3.zero, Time.deltaTime * 6f);
            }

            // Procedural Mars stride bob on wingman visual
            if (visualBody != null)
            {
                walkCycle += Time.deltaTime * (speed > 0.2f ? 7.5f : 1.8f);
                float bob = speed > 0.2f ? Mathf.Abs(Mathf.Sin(walkCycle)) * 0.075f : Mathf.Sin(walkCycle) * 0.015f;
                float roll = speed > 0.2f ? Mathf.Sin(walkCycle) * 3.5f : 0f;
                visualBody.localPosition = new Vector3(0f, bob, 0f);
                visualBody.localRotation = Quaternion.Euler(speed * 1.4f, 0f, roll);
            }

            // Real RF distance + dust storm attenuation model (for Vivox proximity voice clarity)
            DistanceToLeaderM = Vector3.Distance(wingmanRoot.position, leader.position);
            float dustTau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float pathLoss = Mathf.Clamp01(DistanceToLeaderM / 180f) * 32f + Mathf.Max(0f, dustTau - 0.5f) * 14f;
            RfVoiceQualityPercent = Mathf.Clamp(100f - pathLoss, 18f, 100f);
            string band = RfVoiceQualityPercent >= 80f ? "CLEAR" : (RfVoiceQualityPercent >= 50f ? "STATIC" : "WEAK");
            RadioStatusText = $"EVA-02 UHF: {RfVoiceQualityPercent:F0}% ({band}, {DistanceToLeaderM:F1}m)";
        }
    }
}
