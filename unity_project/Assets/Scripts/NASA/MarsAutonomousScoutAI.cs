using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Autonomous AI Recon Scout Bot ("SCOUT-AI-01") patrolling Jezero Crater outcrops
    /// while obeying USGS trafficability constraints (Max Slope <= 15 deg) and dynamic obstacle avoidance.
    /// Uses NavMeshAgent if a baked NavMesh is present, or deterministic terrain-slope-aware steering.
    /// </summary>
    [DefaultExecutionOrder(310)]
    public class MarsAutonomousScoutAI : MonoBehaviour
    {
        public static MarsAutonomousScoutAI Instance { get; private set; }

        [Header("Autonomous Patrol Tuning")]
        [SerializeField] private float patrolSpeedMps = 3.2f;
        [SerializeField] private float maxSafeSlopeDeg = 15.0f;
        [SerializeField] private float waypointArrivalDist = 2.2f;

        public string CurrentStatus { get; private set; } = "Autonomous Recon Patrol Active";
        public string ActiveTargetName { get; private set; } = "Cheyava Falls Sector";
        public float CurrentSlopeDeg { get; private set; }
        public Vector3 BotWorldPos => scoutRoot != null ? scoutRoot.position : Vector3.zero;
        public bool UsingNavMesh { get; private set; }

        private Transform scoutRoot;
        private Transform lidarHead;
        private Light scanLight;
        private NavMeshAgent navAgent;
        private readonly List<(string label, Vector2 xz)> patrolNodes = new List<(string, Vector2)>
        {
            ("Cheyava Falls Sector", new Vector2(14f, 6f)),
            ("Habitat North Perimeter", new Vector2(-18f, 16f)),
            ("Agri-Dome Solar Apron", new Vector2(-20f, -10f)),
            ("Belva Rim Outcrop", new Vector2(20f, -14f)),
            ("Landing Pad Recon Loop", new Vector2(28f, 16f))
        };
        private int currentWpIdx = 0;
        private float dwellTimer = 0f;

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
            BuildScoutBot();
        }

        private void BuildScoutBot()
        {
            if (scoutRoot != null) return;

            GameObject go = new GameObject("MS_Autonomous_ScoutAI_01");
            scoutRoot = go.transform;
            Vector2 startXZ = patrolNodes[0].xz;
            float startY = MarsVisualRealismPolish.SampleTerrainElevation(startXZ.x, startXZ.y);
            scoutRoot.position = new Vector3(startXZ.x, startY + 0.42f, startXZ.y);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material chassisMat = new Material(lit) { color = new Color(0.86f, 0.87f, 0.90f) };
            Material darkMat = new Material(lit) { color = new Color(0.18f, 0.20f, 0.24f) };
            Material cyanMat = new Material(lit) { color = new Color(0.10f, 0.92f, 0.96f) };
            if (cyanMat.HasProperty("_EmissionColor"))
            {
                cyanMat.EnableKeyword("_EMISSION");
                cyanMat.SetColor("_EmissionColor", new Color(0.10f, 0.95f, 1.0f) * 2.4f);
            }

            // Compact 4-wheel autonomous recon chassis + rotating LIDAR mast
            CreatePart("Chassis", PrimitiveType.Cube, scoutRoot, new Vector3(0, 0.16f, 0), new Vector3(0.82f, 0.30f, 1.05f), chassisMat);
            CreatePart("SolarDeck", PrimitiveType.Cube, scoutRoot, new Vector3(0, 0.34f, -0.08f), new Vector3(0.90f, 0.04f, 0.78f), darkMat);
            CreatePart("Mast", PrimitiveType.Cylinder, scoutRoot, new Vector3(0, 0.62f, 0.28f), new Vector3(0.08f, 0.30f, 0.08f), darkMat);

            GameObject head = CreatePart("LidarHead", PrimitiveType.Cylinder, scoutRoot, new Vector3(0, 0.96f, 0.28f), new Vector3(0.24f, 0.12f, 0.24f), cyanMat);
            lidarHead = head.transform;

            Vector3[] wPos =
            {
                new Vector3(-0.48f, -0.10f, 0.38f), new Vector3(0.48f, -0.10f, 0.38f),
                new Vector3(-0.48f, -0.10f, -0.38f), new Vector3(0.48f, -0.10f, -0.38f)
            };
            for (int i = 0; i < wPos.Length; i++)
            {
                GameObject w = CreatePart($"Wheel_{i}", PrimitiveType.Cylinder, scoutRoot, wPos[i], new Vector3(0.28f, 0.10f, 0.28f), darkMat);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }

            GameObject lightGo = new GameObject("ScoutScanSpot");
            lightGo.transform.SetParent(lidarHead, false);
            lightGo.transform.localPosition = new Vector3(0, 0, 0.16f);
            lightGo.transform.localRotation = Quaternion.Euler(18f, 0, 0);
            scanLight = lightGo.AddComponent<Light>();
            scanLight.type = LightType.Spot;
            scanLight.color = new Color(0.18f, 0.94f, 1.0f);
            scanLight.intensity = 2.8f;
            scanLight.range = 16f;
            scanLight.spotAngle = 46f;

            // Inspectable target so player can scan the AI Scout Bot
            var insp = go.AddComponent<MarsInspectableTarget>();
            insp.Configure(
                "scout_ai_01",
                "Autonomous Recon Bot (SCOUT-AI-01)",
                "AUTONOMOUS AI NAVIGATION",
                "Slope-Constrained (<=15 deg) Obstacle-Avoiding Patrol",
                "18.4451°N, 77.4514°E · Jezero Perimeter",
                "USGS SIM 3292 HNhu / HNu Delta Margin",
                "LIDAR + Multi-spectral Terrain Hazard Mapping",
                "Max Slope Limit: 15.0 deg · Speed: 3.2 m/s · Autonomous Avoidance",
                "Autonomous Jezero reconnaissance unit running continuous terrain trafficability checks, LIDAR outcrop scans, and obstacle avoidance along the habitat perimeter.",
                "https://mars.nasa.gov/mars2020/",
                "perseverance_sample_raw.jpg"
            );

            // Attach NavMeshAgent if a valid NavMesh exists at spawn
            if (NavMesh.SamplePosition(scoutRoot.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                navAgent = go.AddComponent<NavMeshAgent>();
                navAgent.speed = patrolSpeedMps;
                navAgent.radius = 0.65f;
                navAgent.height = 1.1f;
                navAgent.Warp(hit.position);
                UsingNavMesh = true;
            }
        }

        private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            p.name = name;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = localScale;
            var r = p.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            var c = p.GetComponent<Collider>();
            if (c != null && name != "Chassis") Destroy(c);
            return p;
        }

        private void Update()
        {
            if (scoutRoot == null || Time.timeScale <= 0.001f) return;

            if (lidarHead != null)
            {
                lidarHead.Rotate(Vector3.up, 95f * Time.deltaTime, Space.Self);
            }

            if (dwellTimer > 0f)
            {
                dwellTimer -= Time.deltaTime;
                CurrentStatus = $"Scanning {ActiveTargetName} ({dwellTimer:F1}s)";
                return;
            }

            var targetNode = patrolNodes[currentWpIdx];
            ActiveTargetName = targetNode.label;
            Vector3 targetPos = new Vector3(
                targetNode.xz.x,
                MarsVisualRealismPolish.SampleTerrainElevation(targetNode.xz.x, targetNode.xz.y) + 0.42f,
                targetNode.xz.y);

            if (UsingNavMesh && navAgent != null && navAgent.isOnNavMesh)
            {
                if (!navAgent.hasPath) navAgent.SetDestination(targetPos);
                if (!navAgent.pathPending && navAgent.remainingDistance <= waypointArrivalDist)
                {
                    dwellTimer = 3.0f;
                    currentWpIdx = (currentWpIdx + 1) % patrolNodes.Count;
                }
                CurrentStatus = $"NavMesh Patrol -> {ActiveTargetName}";
                return;
            }

            Vector3 toTarget = targetPos - scoutRoot.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= waypointArrivalDist)
            {
                dwellTimer = 3.0f;
                currentWpIdx = (currentWpIdx + 1) % patrolNodes.Count;
                CurrentStatus = $"LIDAR Sample Complete: {ActiveTargetName}";
                return;
            }

            Vector3 desiredDir = toTarget.normalized;

            // Dynamic forward obstacle & steep-slope (>15 deg) avoidance
            Vector3 probePos = scoutRoot.position + desiredDir * 2.2f;
            float currH = MarsVisualRealismPolish.SampleTerrainElevation(scoutRoot.position.x, scoutRoot.position.z);
            float nextH = MarsVisualRealismPolish.SampleTerrainElevation(probePos.x, probePos.z);
            CurrentSlopeDeg = Mathf.Atan2(Mathf.Abs(nextH - currH), 2.2f) * Mathf.Rad2Deg;

            bool blocked = Physics.SphereCast(scoutRoot.position + Vector3.up * 0.45f, 0.45f, desiredDir, out RaycastHit obsHit, 2.4f)
                           && !obsHit.collider.transform.IsChildOf(scoutRoot);
            if (blocked || CurrentSlopeDeg > maxSafeSlopeDeg)
            {
                desiredDir = Quaternion.Euler(0f, 42f, 0f) * desiredDir;
                CurrentStatus = $"Avoiding Hazard ({CurrentSlopeDeg:F1} deg) -> {ActiveTargetName}";
            }
            else
            {
                CurrentStatus = $"Patrolling -> {ActiveTargetName} ({dist:F0}m)";
            }

            Vector3 nextPos = scoutRoot.position + desiredDir * (patrolSpeedMps * Time.deltaTime);
            float groundY = MarsVisualRealismPolish.SampleTerrainElevation(nextPos.x, nextPos.z);
            nextPos.y = groundY + 0.38f;
            scoutRoot.position = nextPos;

            // Align smoothly to terrain normal + travel heading
            float hR = MarsVisualRealismPolish.SampleTerrainElevation(nextPos.x + 0.6f, nextPos.z);
            float hF = MarsVisualRealismPolish.SampleTerrainElevation(nextPos.x, nextPos.z + 0.6f);
            Vector3 upN = Vector3.Cross(new Vector3(0f, hF - groundY, 0.6f), new Vector3(0.6f, hR - groundY, 0f)).normalized;
            if (upN.y < 0f) upN = -upN;
            Quaternion targetRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(desiredDir, upN).normalized, upN);
            scoutRoot.rotation = Quaternion.Slerp(scoutRoot.rotation, targetRot, Time.deltaTime * 5.0f);
        }
    }
}
