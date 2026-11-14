using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    [Serializable]
    public struct RoverTelemetry
    {
        public int sol;
        public string siteName;
        public double longitudeEastDeg;
        public double latitudeNorthDeg;
        public float elevationMeters;
        public float currentSpeedMps;
        public float headingDeg;
        public float currentSlopeDeg;
        public float slopeWarningThresholdDeg;
        public float distanceTraveledMeters;
        public float batteryPercent;
        public string batteryStatus;
        public string trafficabilityStatus;
    }

    /// <summary>
    /// MarsTelemetryController visualizes NASA traverse routes via LineRenderers,
    /// places sample tube and landmark waypoints, and exposes live rover telemetry.
    /// </summary>
    public class MarsTelemetryController : MonoBehaviour
    {
        public static MarsTelemetryController Instance { get; private set; }

        [Header("Route Line Renderers")]
        [SerializeField] private LineRenderer lineSafest;
        [SerializeField] private LineRenderer lineShortest;
        [SerializeField] private LineRenderer lineScience;
        [SerializeField] private float lineWidth = 0.8f;

        [Header("Colors")]
        [SerializeField] private Color colorSafest = new Color(0.0f, 0.95f, 0.75f, 1.0f);   // Cyan / Green
        [SerializeField] private Color colorShortest = new Color(1.0f, 0.84f, 0.0f, 1.0f);  // Yellow
        [SerializeField] private Color colorScience = new Color(0.9f, 0.25f, 0.98f, 1.0f);  // Magenta

        [Header("Waypoints")]
        [SerializeField] private Transform waypointContainer;
        [SerializeField] private bool showSampleTubes = true;

        [Header("Active Rover Telemetry")]
        [SerializeField] private RoverTelemetry telemetry = new RoverTelemetry
        {
            sol = 100,
            siteName = "Octavia E. Butler Landing",
            longitudeEastDeg = 77.4508,
            latitudeNorthDeg = 18.4447,
            elevationMeters = -2569.9f,
            currentSpeedMps = 0.12f,
            headingDeg = 284.5f,
            currentSlopeDeg = 4.2f,
            slopeWarningThresholdDeg = 15.0f,
            distanceTraveledMeters = 1159.95f,
            batteryPercent = 94.0f,
            batteryStatus = "RTG Nominal (94%)",
            trafficabilityStatus = "Safe (USGS AHi Regolith)"
        };

        public RoverTelemetry CurrentTelemetry => telemetry;
        public event Action<RoverTelemetry> OnTelemetryUpdated;

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
            if (MarsDatasetLoader.Instance != null)
            {
                if (MarsDatasetLoader.Instance.IsLoaded)
                {
                    BuildVisualizations();
                }
                else
                {
                    MarsDatasetLoader.Instance.OnDataLoaded += BuildVisualizations;
                }
            }
        }

        public void BuildVisualizations()
        {
            EnsureLineRenderers();
            PopulateRouteLines();
            SpawnWaypoints();
            OnTelemetryUpdated?.Invoke(telemetry);
        }

        private void EnsureLineRenderers()
        {
            if (lineSafest == null) lineSafest = CreateRouteRenderer("LineRenderer_SafestRoute", colorSafest);
            if (lineShortest == null) lineShortest = CreateRouteRenderer("LineRenderer_ShortestRoute", colorShortest);
            if (lineScience == null) lineScience = CreateRouteRenderer("LineRenderer_ScienceRoute", colorScience);
        }

        private LineRenderer CreateRouteRenderer(string objectName, Color color)
        {
            Transform existing = transform.Find(objectName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) lr = go.AddComponent<LineRenderer>();

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
            Material lineMat = new Material(lineShader);
            lineMat.color = color;
            lr.material = lineMat;

            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            return lr;
        }

        private void PopulateRouteLines()
        {
            var data = MarsDatasetLoader.Instance?.RoutesData;
            if (data == null) return;

            SetLinePoints(lineSafest, data.safest.unityPositions);
            SetLinePoints(lineShortest, data.shortest.unityPositions);
            SetLinePoints(lineScience, data.science.unityPositions);
        }

        private void SetLinePoints(LineRenderer lr, List<Vector3> points)
        {
            if (lr == null || points == null || points.Count == 0) return;

            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                // Elevate slightly above ground to prevent z-fighting
                Vector3 pt = points[i];
                pt.y += 0.25f;
                lr.SetPosition(i, pt);
            }
        }

        private void SpawnWaypoints()
        {
            if (waypointContainer == null)
            {
                Transform existing = transform.Find("WaypointMarkersRoot");
                if (existing != null) waypointContainer = existing;
                else
                {
                    GameObject go = new GameObject("WaypointMarkersRoot");
                    go.transform.SetParent(transform, false);
                    waypointContainer = go.transform;
                }
            }

            // Spawn Landing Site Marker
            CreateBeacon("Octavia_E_Butler_Landing", Vector3.zero, new Color(1.0f, 0.8f, 0.2f, 1.0f), 1.5f);

            // Spawn Science Site Marker (Rochette)
            var sci = MarsDatasetLoader.Instance?.RoutesData?.science_site;
            if (sci != null)
            {
                float uX = (float)(sci.local_east_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_east_m ?? 512.73));
                float uZ = (float)(sci.local_north_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_north_m ?? 945.43));
                CreateBeacon("Science_Site_Rochette", new Vector3(uX, 0f, uZ), colorScience, 1.2f);
            }

            // Spawn Sample Tubes
            if (showSampleTubes && MarsDatasetLoader.Instance != null)
            {
                int count = 0;
                foreach (var tube in MarsDatasetLoader.Instance.SampleTubes)
                {
                    CreateSampleMarker($"SampleTube_{tube.sample_num}_{tube.sample_name}", tube.unityPosition, tube);
                    count++;
                    if (count >= 15) break; // Display primary 15 surface sample tubes
                }
            }
        }

        private void CreateBeacon(string name, Vector3 pos, Color color, float scale)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = name;
            beacon.transform.SetParent(waypointContainer, false);
            beacon.transform.position = pos + new Vector3(0, scale * 1.5f, 0);
            beacon.transform.localScale = new Vector3(scale * 0.4f, scale * 1.5f, scale * 0.4f);

            Collider col = beacon.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = beacon.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = color;
                r.material = mat;
            }
        }

        private void CreateSampleMarker(string name, Vector3 pos, SampleTubeData tube)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(waypointContainer, false);
            marker.transform.position = pos + new Vector3(0, 0.5f, 0);
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            Collider col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = marker.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                Material mat = new Material(s);
                mat.color = tube.rock_type.Contains("Sedimentary") ? Color.cyan : new Color(1f, 0.5f, 0f);
                r.material = mat;
            }
        }

        public void SetRouteVisibility(bool safest, bool shortest, bool science)
        {
            if (lineSafest != null) lineSafest.enabled = safest;
            if (lineShortest != null) lineShortest.enabled = shortest;
            if (lineScience != null) lineScience.enabled = science;
        }

        public void UpdateRoverMotionTelemetry(float speedMps, float headingDeg, float distanceTraveled)
        {
            telemetry.currentSpeedMps = speedMps;
            telemetry.headingDeg = headingDeg;
            telemetry.distanceTraveledMeters = distanceTraveled;
            OnTelemetryUpdated?.Invoke(telemetry);
        }
    }
}
