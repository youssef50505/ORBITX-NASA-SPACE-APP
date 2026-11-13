using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    [Serializable]
    public class KeplerianOrbiter
    {
        public string missionName;
        public Transform orbiterTransform;
        public float semiMajorAxisMeters;    // Visual showcase orbit semi-major axis (e.g. 1480m)
        public float eccentricity;           // Orbit eccentricity (0.002 to 0.35)
        public float inclinationDeg;          // Orbital inclination relative to equator
        public float raanDeg;                 // Right Ascension of Ascending Node
        public float argPeriapsisDeg;         // Argument of Periapsis
        public float orbitalPeriodSeconds;   // Visual orbit period in seconds (e.g. 60s to 120s)
        public float initialMeanAnomalyDeg;  // Initial phase offset
        public Color trailColor;             // Trajectory line emission color
        public float trailWidth = 1.2f;

        [HideInInspector] public LineRenderer lineRenderer;
        [HideInInspector] public float currentTrueAnomalyDeg;
    }

    /// <summary>
    /// MarsSatelliteConstellation orchestrates the 4 artificial Mars orbiters (Feature 19):
    /// - Mars Reconnaissance Orbiter (MRO)
    /// - Mars Odyssey
    /// - Mars Atmosphere and Volatile EvolutioN (MAVEN)
    /// - Mars Global Surveyor (MGS)
    /// Propagates Keplerian orbital ellipses, solves Kepler's equation via Newton-Raphson,
    /// renders glowing closed-loop LineRenderer trajectories, and locks primary instruments to Nadir.
    /// </summary>
    [ExecuteAlways]
    public class MarsSatelliteConstellation : MonoBehaviour
    {
        public static MarsSatelliteConstellation Instance { get; private set; }

        [Header("Planetary Virtual Center")]
        [SerializeField] private Vector3 marsCenter = new Vector3(0f, -1200f, 0f);

        [Header("Constellation Simulation Controls")]
        [SerializeField] private float timeScale = 1.0f;
        [SerializeField] private bool activeSimulation = true;
        [SerializeField] private int orbitTrailSegments = 90;

        [Header("Orbiting Fleet Members")]
        [SerializeField] private List<KeplerianOrbiter> orbiters = new List<KeplerianOrbiter>();

        private float elapsedSimulationTime = 0f;

        public int OrbiterCount => orbiters != null ? orbiters.Count : 0;
        public IReadOnlyList<KeplerianOrbiter> Orbiters => orbiters;

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
            EnsureDefaultConstellation();
            InitializeOrbitTrails();
        }

        private void Update()
        {
            if (Application.isPlaying && activeSimulation)
            {
                elapsedSimulationTime += Time.deltaTime * timeScale;
            }

            UpdateOrbitalPositions(elapsedSimulationTime);
        }

        public void InitializeOrbitTrails()
        {
            Shader trailShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");

            foreach (var orb in orbiters)
            {
                if (orb.orbiterTransform == null) continue;

                Transform trailChild = orb.orbiterTransform.parent != null
                    ? orb.orbiterTransform.parent.Find($"{orb.missionName}_OrbitTrail")
                    : transform.Find($"{orb.missionName}_OrbitTrail");

                GameObject trailGo;
                if (trailChild == null)
                {
                    trailGo = new GameObject($"{orb.missionName}_OrbitTrail");
                    trailGo.transform.SetParent(transform, false);
                }
                else
                {
                    trailGo = trailChild.gameObject;
                }

                LineRenderer lr = trailGo.GetComponent<LineRenderer>();
                if (lr == null) lr = trailGo.AddComponent<LineRenderer>();

                Material mat = new Material(trailShader);
                mat.name = $"{orb.missionName}_TrailMat";
                mat.color = orb.trailColor;
                lr.material = mat;

                lr.startColor = orb.trailColor;
                lr.endColor = orb.trailColor;
                lr.startWidth = orb.trailWidth;
                lr.endWidth = orb.trailWidth;
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                orb.lineRenderer = lr;
                BakeOrbitTrailPath(orb);
            }
        }

        private void BakeOrbitTrailPath(KeplerianOrbiter orb)
        {
            if (orb.lineRenderer == null) return;

            int count = Mathf.Max(36, orbitTrailSegments);
            orb.lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float nu = (i / (float)count) * 360f;
                Vector3 pt = CalculateOrbitalPosition(orb, nu);
                orb.lineRenderer.SetPosition(i, pt);
            }
        }

        public void UpdateOrbitalPositions(float timeSeconds)
        {
            foreach (var orb in orbiters)
            {
                if (orb.orbiterTransform == null) continue;

                // 1. Mean anomaly progression
                float period = Mathf.Max(10f, orb.orbitalPeriodSeconds);
                float n = 360f / period; // Mean motion in deg/s
                float M = Mathf.Repeat(orb.initialMeanAnomalyDeg + n * timeSeconds, 360f);

                // 2. Solve Kepler's equation for Eccentric Anomaly (E) via Newton-Raphson
                float E = SolveKeplerEccentricAnomaly(M, orb.eccentricity);

                // 3. True anomaly (nu)
                float E_rad = E * Mathf.Deg2Rad;
                float nu_rad = 2.0f * Mathf.Atan2(Mathf.Sqrt(1.0f + orb.eccentricity) * Mathf.Sin(E_rad * 0.5f),
                                                 Mathf.Sqrt(1.0f - orb.eccentricity) * Mathf.Cos(E_rad * 0.5f));
                float nuDeg = nu_rad * Mathf.Rad2Deg;
                orb.currentTrueAnomalyDeg = nuDeg;

                // 4. Position in 3D scene space
                Vector3 pos = CalculateOrbitalPosition(orb, nuDeg);
                orb.orbiterTransform.position = pos;

                // 5. Nadir alignment & flight velocity tangent orientation
                Vector3 nadir = (marsCenter - pos).normalized;
                Vector3 posNext = CalculateOrbitalPosition(orb, nuDeg + 1.0f);
                Vector3 flightTangent = (posNext - pos).normalized;

                if (flightTangent.sqrMagnitude > 0.001f)
                {
                    // Primary instrument points Nadir (down), forward faces flight tangent
                    orb.orbiterTransform.rotation = Quaternion.LookRotation(flightTangent, -nadir);
                }
            }
        }

        private Vector3 CalculateOrbitalPosition(KeplerianOrbiter orb, float trueAnomalyDeg)
        {
            float nuRad = trueAnomalyDeg * Mathf.Deg2Rad;
            float r = orb.semiMajorAxisMeters * (1.0f - orb.eccentricity * orb.eccentricity) / (1.0f + orb.eccentricity * Mathf.Cos(nuRad));

            // Position in 2D orbital plane
            Vector3 inPlane = new Vector3(r * Mathf.Cos(nuRad), 0f, r * Mathf.Sin(nuRad));

            // Rotate by Arg of Periapsis (w), Inclination (i), and RAAN (Omega)
            Quaternion rotOmega = Quaternion.AngleAxis(orb.raanDeg, Vector3.up);
            Quaternion rotInc = Quaternion.AngleAxis(orb.inclinationDeg, Vector3.right);
            Quaternion rotW = Quaternion.AngleAxis(orb.argPeriapsisDeg, Vector3.up);

            Vector3 rotated = rotOmega * rotInc * rotW * inPlane;
            return marsCenter + rotated;
        }

        private float SolveKeplerEccentricAnomaly(float meanAnomalyDeg, float e)
        {
            float M_rad = meanAnomalyDeg * Mathf.Deg2Rad;
            float E_rad = M_rad; // Initial estimate

            for (int i = 0; i < 5; i++)
            {
                float f = E_rad - e * Mathf.Sin(E_rad) - M_rad;
                float fPrime = 1.0f - e * Mathf.Cos(E_rad);
                E_rad -= f / fPrime;
            }

            return E_rad * Mathf.Rad2Deg;
        }

        public void EnsureDefaultConstellation()
        {
            if (orbiters != null && orbiters.Count >= 4) return;

            Transform orbitFleet = transform.Find("MS_Mars_Fleet_Orbit");
            if (orbitFleet == null)
            {
                GameObject ofGo = GameObject.Find("MS_Mars_Fleet_Orbit");
                if (ofGo != null) orbitFleet = ofGo.transform;
            }

            orbiters = new List<KeplerianOrbiter>
            {
                // 1. MRO: Polar mapping orbit, altitude ~280m over Jezero, Sun-synchronous inclination 92.8°
                new KeplerianOrbiter
                {
                    missionName = "MRO",
                    orbiterTransform = FindChildDeep(orbitFleet, "MS_MRO_Root"),
                    semiMajorAxisMeters = 1480f,
                    eccentricity = 0.005f,
                    inclinationDeg = 92.8f,
                    raanDeg = 45f,
                    argPeriapsisDeg = 0f,
                    orbitalPeriodSeconds = 90f,
                    initialMeanAnomalyDeg = 0f,
                    trailColor = new Color(0.0f, 0.90f, 1.0f, 0.75f), // Cyan
                    trailWidth = 1.2f
                },
                // 2. Mars Odyssey: Circular polar science, altitude ~380m, inclination 93.1°
                new KeplerianOrbiter
                {
                    missionName = "Odyssey",
                    orbiterTransform = FindChildDeep(orbitFleet, "MS_Odyssey_Root"),
                    semiMajorAxisMeters = 1580f,
                    eccentricity = 0.002f,
                    inclinationDeg = 93.1f,
                    raanDeg = 135f,
                    argPeriapsisDeg = 30f,
                    orbitalPeriodSeconds = 105f,
                    initialMeanAnomalyDeg = 120f,
                    trailColor = new Color(1.0f, 0.70f, 0.0f, 0.75f), // Amber
                    trailWidth = 1.2f
                },
                // 3. MAVEN: Elliptical orbit, periapsis 240m, apoapsis 960m, inclination 75.0°
                new KeplerianOrbiter
                {
                    missionName = "MAVEN",
                    orbiterTransform = FindChildDeep(orbitFleet, "MS_MAVEN_Root"),
                    semiMajorAxisMeters = 1620f,
                    eccentricity = 0.350f,
                    inclinationDeg = 75.0f,
                    raanDeg = 220f,
                    argPeriapsisDeg = 90f,
                    orbitalPeriodSeconds = 120f,
                    initialMeanAnomalyDeg = 240f,
                    trailColor = new Color(0.0f, 0.90f, 0.46f, 0.75f), // Emerald
                    trailWidth = 1.5f
                },
                // 4. Mars Global Surveyor: Historic mapping orbit, altitude ~340m, inclination 92.8°
                new KeplerianOrbiter
                {
                    missionName = "GlobalSurveyor",
                    orbiterTransform = FindChildDeep(orbitFleet, "MS_GlobalSurveyor_Root"),
                    semiMajorAxisMeters = 1540f,
                    eccentricity = 0.003f,
                    inclinationDeg = 92.8f,
                    raanDeg = 310f,
                    argPeriapsisDeg = 0f,
                    orbitalPeriodSeconds = 98f,
                    initialMeanAnomalyDeg = 180f,
                    trailColor = new Color(0.88f, 0.25f, 0.98f, 0.75f), // Magenta
                    trailWidth = 1.2f
                }
            };
        }

        private Transform FindChildDeep(Transform parent, string name)
        {
            if (parent == null) return GameObject.Find(name)?.transform;
            Transform t = parent.Find(name);
            if (t != null) return t;
            foreach (Transform child in parent)
            {
                Transform res = FindChildDeep(child, name);
                if (res != null) return res;
            }
            return null;
        }
    }
}
