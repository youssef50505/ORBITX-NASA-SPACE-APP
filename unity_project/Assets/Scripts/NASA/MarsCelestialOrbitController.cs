using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MarsSentinel.NASA
{
    public enum SolarLightingPreset
    {
        Sunrise,
        SolarNoon,
        Sunset,
        Twilight,
        Night
    }

    /// <summary>
    /// MarsCelestialOrbitController drives realistic Mars celestial bodies and environmental lighting:
    /// - Feature 17: Phobos and Deimos 3D procedural models, materials, orbital radii, inclinations,
    ///   and period math (Phobos rises West->East, Deimos rises East->West).
    /// - Feature 18: Dynamic solar lighting & Jezero diurnal solar arc matching Jezero Crater coordinates
    ///   (18.4447°N, 77.4508°E), driving Directional Light rotation, color temperature, and atmospheric intensity.
    /// </summary>
    [ExecuteAlways]
    public class MarsCelestialOrbitController : MonoBehaviour
    {
        public static MarsCelestialOrbitController Instance { get; private set; }

        [Header("Jezero Crater Geographical Coordinates")]
        [SerializeField] private double latitudeNorthDeg = 18.4447;   // Jezero Crater Latitude
        [SerializeField] private double longitudeEastDeg = 77.4508;   // Jezero Crater Longitude
        [SerializeField] private float marsObliquityDeg = 25.19f;      // Mars axial tilt
        [SerializeField] private float solarLongitudeLs = 70.0f;       // Mars season (Ls = 70° Northern Summer)

        [Header("Diurnal Solar Cycle State")]
        [Range(0f, 24.6597f)]
        [SerializeField] private float solHour = 12.0f;                // 12.0 = Solar Noon
        [SerializeField] private float timeScale = 1.0f;              // Simulation speed multiplier
        [SerializeField] private bool autoAdvanceTime = true;
        [SerializeField] private float solDurationSeconds = 120.0f;    // Realtime seconds per Martian sol

        [Header("Solar Elevation & Azimuth (Read-Only)")]
        [SerializeField] private float currentSolarElevationDeg;
        [SerializeField] private float currentSolarAzimuthDeg;

        [Header("Lighting & Atmosphere References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private float maxSunIntensity = 1.35f;
        [SerializeField] private Color daySunColor = new Color(1.0f, 0.88f, 0.70f, 1.0f);     // Tawny butterscotch
        [SerializeField] private Color sunsetSunColor = new Color(0.70f, 0.92f, 0.98f, 1.0f);  // Martian blue sunset
        [SerializeField] private Color twilightSunColor = new Color(0.85f, 0.45f, 0.30f, 1.0f);
        [SerializeField] private Color nightAmbientColor = new Color(0.05f, 0.05f, 0.09f, 1.0f);
        [SerializeField] private Color dayAmbientColor = new Color(0.77f, 0.48f, 0.32f, 1.0f);

        [Header("Moons: Phobos & Deimos Configuration")]
        [SerializeField] private Transform phobosRoot;
        [SerializeField] private Transform deimosRoot;
        [SerializeField] private float phobosDisplayDistance = 850.0f;
        [SerializeField] private float deimosDisplayDistance = 1750.0f;

        // Astronomical Constants
        public const float MarsRotationPeriodHours = 24.6597f;
        public const float PhobosOrbitalPeriodHours = 7.6538f;
        public const float DeimosOrbitalPeriodHours = 30.312f;
        public const float PhobosInclinationDeg = 1.08f;
        public const float DeimosInclinationDeg = 1.79f;
        public const float PhobosSemiMajorAxisKm = 9376.0f;
        public const float DeimosSemiMajorAxisKm = 23463.0f;

        // Public Properties
        public float SolHour => solHour;
        public float SolarElevationDeg => currentSolarElevationDeg;
        public float SolarAzimuthDeg => currentSolarAzimuthDeg;
        public double LatitudeNorthDeg => latitudeNorthDeg;
        public double LongitudeEastDeg => longitudeEastDeg;

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
            EnsureSunLightReference();
            EnsureMoonsHierarchy();
            UpdateSolarLighting();
            UpdateMoonsPositions();
        }

        private void Update()
        {
            if (Application.isPlaying && autoAdvanceTime)
            {
                float solDelta = (Time.deltaTime / solDurationSeconds) * 24.6597f * timeScale;
                solHour = (solHour + solDelta) % 24.6597f;
            }

            UpdateSolarLighting();
            UpdateMoonsPositions();
        }

        public void SetSolHour(float hour)
        {
            solHour = Mathf.Repeat(hour, 24.6597f);
            UpdateSolarLighting();
            UpdateMoonsPositions();
        }

        public void ApplyPreset(SolarLightingPreset preset)
        {
            switch (preset)
            {
                case SolarLightingPreset.Sunrise:
                    SetSolHour(6.2f);
                    break;
                case SolarLightingPreset.SolarNoon:
                    SetSolHour(12.0f);
                    break;
                case SolarLightingPreset.Sunset:
                    SetSolHour(17.8f);
                    break;
                case SolarLightingPreset.Twilight:
                    SetSolHour(18.6f);
                    break;
                case SolarLightingPreset.Night:
                    SetSolHour(0.0f);
                    break;
            }
        }

        public void CalculateSolarPosition(float hour, float ls, out float elevationDeg, out float azimuthDeg)
        {
            float phiRad = (float)(latitudeNorthDeg * Mathf.Deg2Rad);
            float epsRad = marsObliquityDeg * Mathf.Deg2Rad;
            float lsRad = ls * Mathf.Deg2Rad;

            // Solar Declination
            float sinDelta = Mathf.Sin(epsRad) * Mathf.Sin(lsRad);
            float deltaRad = Mathf.Asin(Mathf.Clamp(sinDelta, -1.0f, 1.0f));

            // Hour Angle (15° per hour, 0° at noon)
            float hourAngleDeg = (hour - 12.0f) * 15.0f;
            float hRad = hourAngleDeg * Mathf.Deg2Rad;

            // Elevation (Altitude)
            float sinElev = Mathf.Sin(phiRad) * Mathf.Sin(deltaRad) + Mathf.Cos(phiRad) * Mathf.Cos(deltaRad) * Mathf.Cos(hRad);
            elevationDeg = Mathf.Asin(Mathf.Clamp(sinElev, -1.0f, 1.0f)) * Mathf.Rad2Deg;

            // Azimuth (from North clockwise: 0° N, 90° E, 180° S, 270° W)
            float num = -Mathf.Sin(hRad);
            float den = Mathf.Tan(deltaRad) * Mathf.Cos(phiRad) - Mathf.Sin(phiRad) * Mathf.Cos(hRad);
            float azRad = Mathf.Atan2(num, den);
            azimuthDeg = Mathf.Repeat(azRad * Mathf.Rad2Deg, 360.0f);
        }

        private void UpdateSolarLighting()
        {
            EnsureSunLightReference();
            if (sunLight == null) return;

            CalculateSolarPosition(solHour, solarLongitudeLs, out currentSolarElevationDeg, out currentSolarAzimuthDeg);

            // Compute Directional Light direction pointing FROM Sun TO ground
            float elevRad = currentSolarElevationDeg * Mathf.Deg2Rad;
            float azRad = currentSolarAzimuthDeg * Mathf.Deg2Rad;

            Vector3 toSun = new Vector3(
                Mathf.Sin(azRad) * Mathf.Cos(elevRad),
                Mathf.Sin(elevRad),
                Mathf.Cos(azRad) * Mathf.Cos(elevRad)
            );

            sunLight.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);

            // Dynamic Atmospheric Intensity & Color Curve
            float elev = currentSolarElevationDeg;
            if (elev > 20.0f)
            {
                float t = Mathf.InverseLerp(20.0f, 65.0f, elev);
                sunLight.intensity = Mathf.Lerp(1.0f, maxSunIntensity, t);
                sunLight.color = daySunColor;
                RenderSettings.ambientLight = Color.Lerp(dayAmbientColor * 0.8f, dayAmbientColor, t);
            }
            else if (elev > 3.0f)
            {
                float t = Mathf.InverseLerp(3.0f, 20.0f, elev);
                sunLight.intensity = Mathf.Lerp(0.35f, 1.0f, t);
                sunLight.color = Color.Lerp(sunsetSunColor, daySunColor, t);
                RenderSettings.ambientLight = Color.Lerp(dayAmbientColor * 0.5f, dayAmbientColor * 0.8f, t);
            }
            else if (elev > -4.0f)
            {
                // Martian Blue Sunset Transition
                float t = Mathf.InverseLerp(-4.0f, 3.0f, elev);
                sunLight.intensity = Mathf.Lerp(0.02f, 0.35f, t);
                sunLight.color = Color.Lerp(twilightSunColor, sunsetSunColor, t);
                RenderSettings.ambientLight = Color.Lerp(nightAmbientColor * 1.5f, dayAmbientColor * 0.5f, t);
            }
            else
            {
                // Night / Deep Twilight
                sunLight.intensity = 0.0f;
                sunLight.color = Color.black;
                RenderSettings.ambientLight = nightAmbientColor;
            }
        }

        private void UpdateMoonsPositions()
        {
            if (phobosRoot == null || deimosRoot == null) return;

            // Mars rotation angle around pole
            float marsRotRate = 360.0f / MarsRotationPeriodHours;

            // Phobos Orbit (T = 7.6538h, prograde, faster than Mars rotation -> rises in WEST, sets in EAST)
            float phobosOrbRate = 360.0f / PhobosOrbitalPeriodHours;
            float phobosPhase = Mathf.Repeat(phobosOrbRate * solHour, 360.0f);
            Vector3 phobosPos = CalculateTopocentricMoonPosition(phobosPhase, PhobosInclinationDeg, phobosDisplayDistance, marsRotRate * solHour);
            phobosRoot.localPosition = phobosPos;
            phobosRoot.LookAt(transform.position);

            // Deimos Orbit (T = 30.312h, prograde, slower than Mars rotation -> rises in EAST, sets in WEST)
            float deimosOrbRate = 360.0f / DeimosOrbitalPeriodHours;
            float deimosPhase = Mathf.Repeat(deimosOrbRate * solHour, 360.0f);
            Vector3 deimosPos = CalculateTopocentricMoonPosition(deimosPhase, DeimosInclinationDeg, deimosDisplayDistance, marsRotRate * solHour);
            deimosRoot.localPosition = deimosPos;
            deimosRoot.LookAt(transform.position);
        }

        private Vector3 CalculateTopocentricMoonPosition(float orbitalPhaseDeg, float inclinationDeg, float displayRadius, float marsRotationDeg)
        {
            // Apparent hour angle relative to observer's meridian
            float relativeHourAngleDeg = orbitalPhaseDeg - marsRotationDeg;
            float hRad = relativeHourAngleDeg * Mathf.Deg2Rad;
            float incRad = inclinationDeg * Mathf.Deg2Rad;
            float latRad = (float)(latitudeNorthDeg * Mathf.Deg2Rad);

            // Moon declination modulated by inclination
            float declinationRad = incRad * Mathf.Sin(orbitalPhaseDeg * Mathf.Deg2Rad);

            // Elevation and Azimuth
            float sinElev = Mathf.Sin(latRad) * Mathf.Sin(declinationRad) + Mathf.Cos(latRad) * Mathf.Cos(declinationRad) * Mathf.Cos(hRad);
            float elevRad = Mathf.Asin(Mathf.Clamp(sinElev, -1.0f, 1.0f));

            float num = -Mathf.Sin(hRad);
            float den = Mathf.Tan(declinationRad) * Mathf.Cos(latRad) - Mathf.Sin(latRad) * Mathf.Cos(hRad);
            float azRad = Mathf.Atan2(num, den);

            return new Vector3(
                displayRadius * Mathf.Sin(azRad) * Mathf.Cos(elevRad),
                displayRadius * Mathf.Sin(elevRad),
                displayRadius * Mathf.Cos(azRad) * Mathf.Cos(elevRad)
            );
        }

        public void EnsureMoonsHierarchy()
        {
            Transform celestialRoot = transform.Find("MS_Mars_Moons_Root");
            if (celestialRoot == null)
            {
                GameObject rootGo = new GameObject("MS_Mars_Moons_Root");
                rootGo.transform.SetParent(transform, false);
                celestialRoot = rootGo.transform;
            }

            if (phobosRoot == null)
            {
                Transform p = celestialRoot.Find("Phobos");
                if (p == null)
                {
                    GameObject pGo = CreateProceduralMoon("Phobos", celestialRoot, new Vector3(1.23f, 1.0f, 0.82f) * 16.0f, new Color(0.28f, 0.27f, 0.26f), 0.90f);
                    phobosRoot = pGo.transform;
                }
                else
                {
                    phobosRoot = p;
                }
            }

            if (deimosRoot == null)
            {
                Transform d = celestialRoot.Find("Deimos");
                if (d == null)
                {
                    GameObject dGo = CreateProceduralMoon("Deimos", celestialRoot, new Vector3(1.25f, 1.0f, 0.83f) * 7.5f, new Color(0.33f, 0.31f, 0.28f), 0.82f);
                    deimosRoot = dGo.transform;
                }
                else
                {
                    deimosRoot = d;
                }
            }
        }

        private GameObject CreateProceduralMoon(string moonName, Transform parent, Vector3 radiiScale, Color albedoColor, float roughness)
        {
            GameObject go = new GameObject(moonName);
            go.transform.SetParent(parent, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Mesh mesh = GenerateEllipsoidMesh(radiiScale, 28, 28);
            mesh.name = $"{moonName}_ProceduralMesh";
            mf.sharedMesh = mesh;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material mat = new Material(urpLit);
            mat.name = $"{moonName}_Material";
            mat.SetColor("_BaseColor", albedoColor);
            mat.SetFloat("_Roughness", roughness);
            mat.SetFloat("_Metallic", 0.05f);
            mr.sharedMaterial = mat;

            return go;
        }

        private Mesh GenerateEllipsoidMesh(Vector3 radii, int stacks, int slices)
        {
            Mesh mesh = new Mesh();
            int vertCount = (stacks + 1) * (slices + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[stacks * slices * 6];

            int vIdx = 0;
            for (int i = 0; i <= stacks; i++)
            {
                float stackAngle = Mathf.PI / 2 - i * Mathf.PI / stacks;
                float xy = Mathf.Cos(stackAngle);
                float z = Mathf.Sin(stackAngle);

                for (int j = 0; j <= slices; j++)
                {
                    float sliceAngle = j * 2 * Mathf.PI / slices;
                    float x = xy * Mathf.Cos(sliceAngle);
                    float y = xy * Mathf.Sin(sliceAngle);

                    // Add pseudo-asteroid surface noise
                    float noise = 1.0f + 0.08f * Mathf.Sin(x * 4f) * Mathf.Cos(y * 4f + z * 3f);
                    Vector3 baseUnit = new Vector3(x, y, z) * noise;

                    vertices[vIdx] = Vector3.Scale(baseUnit, radii);
                    normals[vIdx] = baseUnit.normalized;
                    uvs[vIdx] = new Vector2((float)j / slices, (float)i / stacks);
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int i = 0; i < stacks; i++)
            {
                int k1 = i * (slices + 1);
                int k2 = k1 + slices + 1;

                for (int j = 0; j < slices; j++, k1++, k2++)
                {
                    if (i != 0)
                    {
                        triangles[tIdx++] = k1;
                        triangles[tIdx++] = k2;
                        triangles[tIdx++] = k1 + 1;
                    }

                    if (i != (stacks - 1))
                    {
                        triangles[tIdx++] = k1 + 1;
                        triangles[tIdx++] = k2;
                        triangles[tIdx++] = k2 + 1;
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private void EnsureSunLightReference()
        {
            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }
        }
    }
}
